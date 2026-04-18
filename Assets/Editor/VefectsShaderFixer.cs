#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PokemonAdventure.Editor
{
    // ==========================================================================
    // Vefects Shader Fixer
    // Removes "ShaderGraphShader"="true" from Vefects ASE shaders.
    //
    // In URP 17 (Unity 6) this tag causes Unity to use the ShaderGraph
    // material inspector, which hides all properties on non-ShaderGraph shaders.
    //
    // Run via: Tools → Fix Vefects Shaders
    // ==========================================================================

    public static class VefectsShaderFixer
    {
        [MenuItem("Tools/Fix Vefects Shaders (URP 17 / Unity 6)")]
        public static void FixAll()
        {
            string folder = "Assets/Vefects";

            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader", new[] { folder });

            int fixedCount  = 0;
            int skippedCount = 0;

            foreach (string guid in shaderGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fullPath  = Path.GetFullPath(assetPath);

                if (!fullPath.EndsWith(".shader")) continue;

                string source  = File.ReadAllText(fullPath);
                string patched = source;

                // Remove ShaderGraphShader="true" tag (with any whitespace variation)
                patched = System.Text.RegularExpressions.Regex.Replace(
                    patched,
                    @"\s*""ShaderGraphShader""\s*=\s*""true""",
                    "");

                // Change UniversalMaterialType from "Lit" to "Unlit" for particle shaders
                // (Particle/VFX shaders should never use the Lit inspector path)
                patched = patched.Replace(
                    "\"UniversalMaterialType\"=\"Lit\"",
                    "\"UniversalMaterialType\"=\"Unlit\"");

                if (patched == source)
                {
                    skippedCount++;
                    continue;
                }

                File.WriteAllText(fullPath, patched);
                fixedCount++;
                Debug.Log($"[VefectsShaderFixer] Fixed: {assetPath}");
            }

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Vefects Shader Fix",
                $"Done!\n\nFixed:   {fixedCount} shader(s)\nSkipped: {skippedCount} (already clean)\n\n" +
                "Materials should now show their properties correctly.",
                "OK");
        }
    }
}
#endif
