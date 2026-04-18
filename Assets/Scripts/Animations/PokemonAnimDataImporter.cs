using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PokemonAnimDataImporter
{
    [MenuItem("Tools/Pokemon/Import AnimData From Selected Folder")]
    public static void ImportFromSelectedFolder()
    {
        var selected = Selection.activeObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Fehlt", "Bitte einen Pok\u00e9mon-Ordner ausw\u00e4hlen.", "OK");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(selected);

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Fehlt", "Bitte einen Ordner ausw\u00e4hlen, nicht eine Datei.", "OK");
            return;
        }

        ImportFolder(folderPath);
    }

    [MenuItem("Tools/Pokemon/Import All AnimData")]
    public static void ImportAll()
    {
        string root  = "Assets/Art/Pokemon/Characters";
        string[] guids = AssetDatabase.FindAssets("AnimData", new[] { root });

        int count = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("AnimData.xml", StringComparison.OrdinalIgnoreCase)) continue;
            string folder = Path.GetDirectoryName(path).Replace("\\", "/");
            ImportFolderSilent(folder);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Fertig", $"AnimData f\u00fcr {count} Pok\u00e9mon importiert.", "OK");
    }

    /// <summary>Silent variant used by ImportAll — no dialog on success.</summary>
    private static void ImportFolderSilent(string folderPath) =>
        ImportFolder(folderPath, silent: true);

    public static void ImportFolder(string folderPath, bool silent = false)
    {
        string animDataPath = FindAnimDataPath(folderPath);
        if (string.IsNullOrEmpty(animDataPath))
        {
            EditorUtility.DisplayDialog("Nicht gefunden", "Keine AnimData XML im gew�hlten Ordner gefunden.", "OK");
            return;
        }

        XDocument doc;
        try
        {
            string fullPath = Path.GetFullPath(animDataPath);
            doc = XDocument.Load(fullPath);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("XML Fehler", $"AnimData konnte nicht gelesen werden:\n{ex.Message}", "OK");
            return;
        }

        var root = doc.Element("AnimData");
        if (root == null)
        {
            EditorUtility.DisplayDialog("XML Fehler", "Root <AnimData> fehlt.", "OK");
            return;
        }

        int shadowSize = ReadInt(root.Element("ShadowSize"), 1);
        var animElements = root.Element("Anims")?.Elements("Anim").ToList();

        if (animElements == null || animElements.Count == 0)
        {
            if (!silent)
                EditorUtility.DisplayDialog("Leer", "Keine <Anim>-Eintr\u00e4ge gefunden.", "OK");
            return;
        }

        string generatedFolder = Path.Combine(folderPath, "Generated").Replace("\\", "/");
        EnsureFolderExists(folderPath, generatedFolder);

        string setName = new DirectoryInfo(folderPath).Name + "_AnimationSet";
        string assetPath = $"{generatedFolder}/{setName}.asset";

        var set = AssetDatabase.LoadAssetAtPath<PokemonAnimationSet>(assetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<PokemonAnimationSet>();
            AssetDatabase.CreateAsset(set, assetPath);
        }

        set.shadowSize = shadowSize;
        set.animations.Clear();

        foreach (var animEl in animElements)
        {
            string name = ReadString(animEl.Element("Name"));
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var def = new PokemonAnimationDefinition
            {
                name = name,
                id = MapNameToAnimId(name),
                index = ReadInt(animEl.Element("Index"), -1),
                frameWidth = ReadInt(animEl.Element("FrameWidth"), 0),
                frameHeight = ReadInt(animEl.Element("FrameHeight"), 0),
                rushFrame = ReadInt(animEl.Element("RushFrame"), -1),
                hitFrame = ReadInt(animEl.Element("HitFrame"), -1),
                returnFrame = ReadInt(animEl.Element("ReturnFrame"), -1),
                copyOf = ReadString(animEl.Element("CopyOf")),
                loop = GuessLoop(name),
                durations = animEl.Element("Durations")?
                    .Elements("Duration")
                    .Select(x => ReadInt(x, 1))
                    .ToList() ?? new List<int>()
            };

            if (!string.IsNullOrWhiteSpace(def.copyOf))
            {
                def.bodyFrames   = Array.Empty<Sprite>();
                def.shadowFrames = Array.Empty<Sprite>();
            }
            else
            {
                string animPath   = FindFileIgnoreCase(folderPath, $"{name}-Anim");
                string shadowPath = FindFileIgnoreCase(folderPath, $"{name}-Shadow");

                // Slice sprite sheets based on frame dimensions from AnimData
                if (def.frameWidth > 0 && def.frameHeight > 0)
                {
                    SliceSpriteSheet(animPath,   def.frameWidth, def.frameHeight);
                    SliceSpriteSheet(shadowPath, def.frameWidth, def.frameHeight);
                }

                def.bodyFrames   = LoadSprites(animPath);
                def.shadowFrames = LoadSprites(shadowPath);
            }

            set.animations.Add(def);
        }

        EditorUtility.SetDirty(set);
        if (!silent)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        set.RebuildCache();

        if (!silent)
        {
            EditorUtility.DisplayDialog(
                "Fertig",
                $"AnimData importiert.\nAsset: {assetPath}\nAnimationen: {set.animations.Count}",
                "OK");
        }
    }

    // ── Sprite Sheet Slicing ──────────────────────────────────────────────

    /// <summary>
    /// Configures the TextureImporter for a PMD sprite sheet and re-imports it.
    /// Layout: rows = 8 directions (top→bottom), columns = frames (left→right).
    /// Sprite index = row * cols + col.
    /// </summary>
    private static void SliceSpriteSheet(string assetPath, int frameW, int frameH)
    {
        if (string.IsNullOrEmpty(assetPath) || frameW <= 0 || frameH <= 0) return;

        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null) return;

        ti.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        if (texW == 0 || texH == 0) return;

        int cols          = Mathf.Max(1, texW / frameW);
        int rows          = Mathf.Max(1, texH / frameH);
        int expectedCount = rows * cols;

        // Read current sprite count via ISpriteEditorDataProvider (replaces ti.spritesheet)
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(ti);
        dataProvider.InitSpriteEditorDataProvider();
        int currentCount = dataProvider.GetSpriteRects()?.Length ?? 0;

        // Only re-slice if settings changed (avoids unnecessary reimports)
        bool needsUpdate =
            ti.textureType      != TextureImporterType.Sprite  ||
            ti.spriteImportMode != SpriteImportMode.Multiple   ||
            ti.filterMode       != FilterMode.Point            ||
            ti.mipmapEnabled    != false                       ||
            currentCount        != expectedCount;

        if (!needsUpdate) return;

        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Multiple;
        ti.filterMode          = FilterMode.Point;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled       = false;
        ti.alphaIsTransparency = true;

        // Re-init after changing import mode so the provider sees Multiple mode
        dataProvider.InitSpriteEditorDataProvider();

        var rects = new SpriteRect[expectedCount];
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            int i = row * cols + col;
            rects[i] = new SpriteRect
            {
                name      = $"frame_{i}",
                // Unity rects have Y=0 at bottom; PNG row 0 is at the top.
                rect      = new Rect(col * frameW, texH - (row + 1) * frameH, frameW, frameH),
                pivot     = new Vector2(0.5f, 0f),
                alignment = SpriteAlignment.Custom
            };
        }

        dataProvider.SetSpriteRects(rects);
        dataProvider.Apply();
        ti.SaveAndReimport();
    }

    private static string FindAnimDataPath(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("AnimData", new[] { folderPath });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(fileName, "AnimData", StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    private static string FindFileIgnoreCase(string folderPath, string fileNameWithoutExtension)
    {
        string[] guids = AssetDatabase.FindAssets(fileNameWithoutExtension, new[] { folderPath });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string candidate = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(candidate, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    private static Sprite[] LoadSprites(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return Array.Empty<Sprite>();

        var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        return assets
            .OfType<Sprite>()
            .OrderBy(s => ExtractTrailingNumber(s.name))
            .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ExtractTrailingNumber(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int i = text.Length - 1;
        while (i >= 0 && char.IsDigit(text[i]))
            i--;

        string number = text.Substring(i + 1);
        return int.TryParse(number, out int value) ? value : 0;
    }

    private static void EnsureFolderExists(string rootFolder, string targetFolder)
    {
        if (AssetDatabase.IsValidFolder(targetFolder))
            return;

        string parent = rootFolder;
        string folderName = Path.GetFileName(targetFolder);

        if (!AssetDatabase.IsValidFolder(targetFolder))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static int ReadInt(XElement element, int fallback)
    {
        if (element == null)
            return fallback;

        return int.TryParse(element.Value, out int value) ? value : fallback;
    }

    private static string ReadString(XElement element)
    {
        return element?.Value?.Trim() ?? string.Empty;
    }

    private static bool GuessLoop(string name)
    {
        switch (name)
        {
            case "Idle":
            case "Walk":
            case "Sleep":
            case "EventSleep":
            case "Float":
            case "DeepBreath":
            case "Eat":
            case "Laying":
            case "LookUp":
            case "Sit":
            case "Head":
            case "Nod":
            case "Pose":
                return true;

            default:
                return false;
        }
    }

    private static PokemonAnimId MapNameToAnimId(string name)
    {
        return name switch
        {
            "Walk" => PokemonAnimId.Walk,
            "Attack" => PokemonAnimId.Attack,
            "Kick" => PokemonAnimId.Kick,
            "Shoot" => PokemonAnimId.Shoot,
            "Strike" => PokemonAnimId.Strike,
            "Sleep" => PokemonAnimId.Sleep,
            "Hurt" => PokemonAnimId.Hurt,
            "Idle" => PokemonAnimId.Idle,
            "Swing" => PokemonAnimId.Swing,
            "Double" => PokemonAnimId.Double,
            "Hop" => PokemonAnimId.Hop,
            "Charge" => PokemonAnimId.Charge,
            "Rotate" => PokemonAnimId.Rotate,
            "EventSleep" => PokemonAnimId.EventSleep,
            "Wake" => PokemonAnimId.Wake,
            "Eat" => PokemonAnimId.Eat,
            "Tumble" => PokemonAnimId.Tumble,
            "Pose" => PokemonAnimId.Pose,
            "Pull" => PokemonAnimId.Pull,
            "Pain" => PokemonAnimId.Pain,
            "Float" => PokemonAnimId.Float,
            "DeepBreath" => PokemonAnimId.DeepBreath,
            "Nod" => PokemonAnimId.Nod,
            "Sit" => PokemonAnimId.Sit,
            "LookUp" => PokemonAnimId.LookUp,
            "Sink" => PokemonAnimId.Sink,
            "Trip" => PokemonAnimId.Trip,
            "Laying" => PokemonAnimId.Laying,
            "LeapForth" => PokemonAnimId.LeapForth,
            "Head" => PokemonAnimId.Head,
            "Cringe" => PokemonAnimId.Cringe,
            "LostBalance" => PokemonAnimId.LostBalance,
            "TumbleBack" => PokemonAnimId.TumbleBack,
            "Faint" => PokemonAnimId.Faint,
            "HitGround" => PokemonAnimId.HitGround,
            _ => PokemonAnimId.None
        };
    }
}