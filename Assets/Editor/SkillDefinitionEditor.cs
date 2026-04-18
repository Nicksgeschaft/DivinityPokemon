#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Data;

namespace PokemonAdventure.Editor
{
    // ==========================================================================
    // Skill Definition — Custom Inspector
    // Provides an organized, color-coded inspector for SkillDefinition assets.
    //
    // Effects section shows only fields relevant to the selected SkillEffectType,
    // with a colored header bar per effect type for quick visual scanning.
    // ==========================================================================

    [CustomEditor(typeof(SkillDefinition))]
    public class SkillDefinitionEditor : UnityEditor.Editor
    {
        // ── Effect type colour palette ────────────────────────────────────────

        private static readonly Dictionary<SkillEffectType, Color> EffectColors = new()
        {
            { SkillEffectType.Damage,           new Color(0.85f, 0.25f, 0.25f, 1f) },
            { SkillEffectType.Heal,             new Color(0.20f, 0.78f, 0.35f, 1f) },
            { SkillEffectType.Shield,           new Color(0.30f, 0.55f, 0.90f, 1f) },
            { SkillEffectType.ApplyStatus,      new Color(0.72f, 0.30f, 0.88f, 1f) },
            { SkillEffectType.StatModify,       new Color(0.90f, 0.62f, 0.18f, 1f) },
            { SkillEffectType.ApplyGridSurface, new Color(0.85f, 0.78f, 0.15f, 1f) },
        };

        private static readonly string[] EffectTypeLabels =
        {
            "Damage", "Heal", "Shield", "Apply Status", "Stat Modify", "Apply Grid Surface"
        };

        // ── Section foldouts ─────────────────────────────────────────────────

        private bool _foldIdentity  = true;
        private bool _foldType      = true;
        private bool _foldAPRange   = true;
        private bool _foldVisuals   = true;
        private bool _foldEffects   = true;
        private bool _foldOther     = false;

        // Effect fold states — indexed by position; resized as list grows.
        private readonly List<bool> _effectFolds = new();

        // ── Cached serialized properties ──────────────────────────────────────

        private SerializedProperty _pEffects;

        private void OnEnable()
        {
            _pEffects = serializedObject.FindProperty("Effects");
        }

        // ── Main draw ─────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var skill = (SkillDefinition)target;

            DrawHeader(skill);

            EditorGUILayout.Space(6);

            _foldIdentity = DrawFoldout(_foldIdentity, "Identity",    () => DrawIdentity(skill));
            _foldType     = DrawFoldout(_foldType,     "Type & Category", DrawTypeCategory);
            _foldAPRange  = DrawFoldout(_foldAPRange,  "AP, Cooldown & Range", DrawAPCooldownRange);
            _foldVisuals  = DrawFoldout(_foldVisuals,  "Visuals & Audio", DrawVisuals);
            _foldEffects  = DrawFoldout(_foldEffects,  "Effects",     DrawEffects);
            _foldOther    = DrawFoldout(_foldOther,    "Other",       DrawOther);

            serializedObject.ApplyModifiedProperties();
        }

        // ── Header ────────────────────────────────────────────────────────────

        private void DrawHeader(SkillDefinition skill)
        {
            EditorGUILayout.BeginHorizontal("box");

            // Icon preview
            var iconProp = serializedObject.FindProperty("SkillIcon");
            var iconObj  = iconProp.objectReferenceValue as Sprite;
            if (iconObj != null)
            {
                var preview = AssetPreview.GetAssetPreview(iconObj);
                if (preview != null)
                    GUILayout.Label(preview, GUILayout.Width(60), GUILayout.Height(60));
                else
                    GUILayout.Box("...", GUILayout.Width(60), GUILayout.Height(60));
            }
            else
            {
                GUILayout.Box("No Icon", GUILayout.Width(60), GUILayout.Height(60));
            }

            GUILayout.Space(6);

            EditorGUILayout.BeginVertical();

            // Skill name in large text
            var nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                wordWrap = false
            };
            GUILayout.Label(string.IsNullOrEmpty(skill.SkillName) ? "(Unnamed)" : skill.SkillName, nameStyle);

            // Type + category badge line
            var typeColor = TypeToColor(skill.SkillType);
            var oldColor  = GUI.contentColor;
            GUI.contentColor = typeColor;
            GUILayout.Label($"  {skill.SkillType}  ·  {skill.Category}", EditorStyles.miniLabel);
            GUI.contentColor = oldColor;

            // Quick stats line
            string cd = skill.Cooldown > 0 ? $"{skill.Cooldown}T cd" : "No cd";
            GUILayout.Label(
                $"AP {skill.APCost}  |  Range {skill.Range}  |  {cd}  |  Acc {skill.Accuracy}%  |  Effects: {skill.Effects.Count}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // ── Sections ──────────────────────────────────────────────────────────

        private void DrawIdentity(SkillDefinition skill)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillId"),   new GUIContent("Skill ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Description"));

            if (string.IsNullOrEmpty(skill.SkillId) || skill.SkillId == "skill_unnamed")
                EditorGUILayout.HelpBox("Skill ID is not set. Set a unique machine-readable ID.", MessageType.Warning);
        }

        private void DrawTypeCategory()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillType"), new GUIContent("Pokémon Type"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Category"),  new GUIContent("Category"));
        }

        private void DrawAPCooldownRange()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("APCost"),   new GUIContent("AP Cost"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Cooldown"), new GUIContent("Cooldown (turns)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Accuracy"), new GUIContent("Accuracy (%)"));
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Range"),     new GUIContent("Cast Range (cells)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Targeting"), new GUIContent("Targeting"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AreaShape"), new GUIContent("AoE Shape"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AoERadius"), new GUIContent("AoE Radius (cells)"));
        }

        private void DrawVisuals()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillIcon"),         new GUIContent("Skill Icon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillBarBackground"), new GUIContent("Skill Bar Background"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("CastAnimation"),     new GUIContent("Cast Animation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("VFXPrefab"),         new GUIContent("VFX Prefab (impact)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SoundEffect"),       new GUIContent("Sound Effect"));
        }

        private void DrawOther()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("UsableOutsideCombat"),       new GUIContent("Usable Outside Combat"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OverworldEffectDescription"), new GUIContent("Overworld Description"));
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("StatRequirements"), new GUIContent("Stat Requirements"), true);
        }

        // ── Effects section ───────────────────────────────────────────────────

        private void DrawEffects()
        {
            if (_pEffects.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No effects defined — this skill does nothing in combat.",
                    MessageType.Warning);
            }

            // Sync fold state list length
            while (_effectFolds.Count < _pEffects.arraySize) _effectFolds.Add(true);

            int toRemove = -1;
            int moveUp   = -1;
            int moveDown = -1;

            for (int i = 0; i < _pEffects.arraySize; i++)
            {
                var element        = _pEffects.GetArrayElementAtIndex(i);
                var effectTypeProp = element.FindPropertyRelative("EffectType");
                var effectType     = (SkillEffectType)effectTypeProp.enumValueIndex;

                var color       = EffectColors.TryGetValue(effectType, out var c) ? c : Color.gray;
                var dimColor    = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.25f);

                // Outer box
                var bgStyle           = new GUIStyle("box");
                EditorGUILayout.BeginVertical(bgStyle);

                // Top color strip
                var stripRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.Height(3), GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(stripRect, color);

                // Header row
                EditorGUILayout.BeginHorizontal();

                // Expand/collapse arrow
                _effectFolds[i] = EditorGUILayout.Foldout(_effectFolds[i], GUIContent.none, true,
                    GUIStyle.none);

                // Colored type label
                var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                    { normal = { textColor = color } };
                GUILayout.Label($"  {EffectTypeLabels[(int)effectType]}", labelStyle);

                // Show quick summary when collapsed
                if (!_effectFolds[i])
                {
                    var summary = EffectSummary(effectType, element);
                    GUILayout.Label(summary, EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                // Reorder buttons
                GUI.enabled = i > 0;
                if (GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(20))) moveUp = i;
                GUI.enabled = i < _pEffects.arraySize - 1;
                if (GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(20))) moveDown = i;
                GUI.enabled = true;

                // Remove
                var removeBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.80f, 0.25f, 0.25f, 1f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) toRemove = i;
                GUI.backgroundColor = removeBg;

                EditorGUILayout.EndHorizontal();

                // Effect body (only when expanded)
                if (_effectFolds[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Space(2);

                    // Effect type selector
                    EditorGUILayout.PropertyField(effectTypeProp, new GUIContent("Effect Type"));
                    effectType = (SkillEffectType)effectTypeProp.enumValueIndex;

                    // Target
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("Target"));

                    EditorGUILayout.Space(2);
                    DrawEffectBody(effectType, element);

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }

            // Deferred mutations (avoid modifying list mid-loop)
            if (toRemove >= 0)
            {
                _pEffects.DeleteArrayElementAtIndex(toRemove);
                if (toRemove < _effectFolds.Count) _effectFolds.RemoveAt(toRemove);
            }
            else if (moveUp >= 0)
            {
                _pEffects.MoveArrayElement(moveUp, moveUp - 1);
                Swap(_effectFolds, moveUp, moveUp - 1);
            }
            else if (moveDown >= 0)
            {
                _pEffects.MoveArrayElement(moveDown, moveDown + 1);
                Swap(_effectFolds, moveDown, moveDown + 1);
            }

            // Add Effect button
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var addBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.25f, 0.60f, 0.30f, 1f);
            if (GUILayout.Button("+ Add Effect", GUILayout.Width(130)))
            {
                int idx = _pEffects.arraySize;
                _pEffects.arraySize++;
                _effectFolds.Add(true);

                var newEl = _pEffects.GetArrayElementAtIndex(idx);
                newEl.FindPropertyRelative("EffectType").enumValueIndex   = 0;
                newEl.FindPropertyRelative("Target").enumValueIndex       = 0;
                newEl.FindPropertyRelative("Power").intValue              = 40;
                newEl.FindPropertyRelative("DamageCategory").enumValueIndex = 0;
                newEl.FindPropertyRelative("ApplyChance").intValue        = 100;
                newEl.FindPropertyRelative("StatusDuration").intValue     = 2;
                newEl.FindPropertyRelative("SurfaceDuration").intValue    = 3;
            }
            GUI.backgroundColor = addBg;

            EditorGUILayout.EndHorizontal();
        }

        // ── Per-effect body ───────────────────────────────────────────────────

        private void DrawEffectBody(SkillEffectType type, SerializedProperty el)
        {
            switch (type)
            {
                case SkillEffectType.Damage:
                    Prop(el, "Power",          "Power");
                    Prop(el, "DamageCategory", "Damage Type");
                    if (el.FindPropertyRelative("Power").intValue == 0)
                        EditorGUILayout.HelpBox("Power is 0 — no damage will be dealt.", MessageType.Warning);
                    break;

                case SkillEffectType.Heal:
                    Prop(el, "Power", "Heal Amount (HP)");
                    if (el.FindPropertyRelative("Power").intValue == 0)
                        EditorGUILayout.HelpBox("Heal amount is 0 — nothing will be restored.", MessageType.Warning);
                    break;

                case SkillEffectType.Shield:
                    Prop(el, "Power",          "Armor Amount");
                    Prop(el, "DamageCategory", "Armor Type (Physical / Special)");
                    break;

                case SkillEffectType.ApplyStatus:
                    Prop(el, "StatusType",      "Status");
                    Prop(el, "ApplyChance",     "Apply Chance (%)");
                    Prop(el, "StatusDuration",  "Duration (turns)");
                    Prop(el, "StatusMagnitude", "Magnitude");
                    var sType = (StatusEffectType)el.FindPropertyRelative("StatusType").enumValueIndex;
                    if (sType == StatusEffectType.None)
                        EditorGUILayout.HelpBox("Status type is None — nothing will be applied.", MessageType.Warning);
                    break;

                case SkillEffectType.StatModify:
                    EditorGUILayout.PropertyField(el.FindPropertyRelative("StatModifiers"),
                        new GUIContent("Stat Modifiers"), true);
                    if (el.FindPropertyRelative("StatModifiers").arraySize == 0)
                        EditorGUILayout.HelpBox("No stat modifiers added — this effect does nothing.", MessageType.Warning);
                    break;

                case SkillEffectType.ApplyGridSurface:
                    Prop(el, "SurfaceToApply",  "Surface Type");
                    Prop(el, "SurfaceDuration", "Duration (turns, 0 = permanent)");
                    Prop(el, "SurfacePrefab",   "Visual Prefab");
                    break;
            }
        }

        // ── Collapsed summary line ────────────────────────────────────────────

        private static string EffectSummary(SkillEffectType type, SerializedProperty el)
        {
            return type switch
            {
                SkillEffectType.Damage =>
                    $"Pwr {el.FindPropertyRelative("Power").intValue} · " +
                    $"{(DamageType)el.FindPropertyRelative("DamageCategory").enumValueIndex}",
                SkillEffectType.Heal =>
                    $"+{el.FindPropertyRelative("Power").intValue} HP",
                SkillEffectType.Shield =>
                    $"+{el.FindPropertyRelative("Power").intValue} " +
                    $"{(DamageType)el.FindPropertyRelative("DamageCategory").enumValueIndex} armor",
                SkillEffectType.ApplyStatus =>
                    $"{(StatusEffectType)el.FindPropertyRelative("StatusType").enumValueIndex} " +
                    $"({el.FindPropertyRelative("ApplyChance").intValue}%)" +
                    $" {el.FindPropertyRelative("StatusDuration").intValue}T",
                SkillEffectType.StatModify =>
                    $"{el.FindPropertyRelative("StatModifiers").arraySize} modifier(s)",
                SkillEffectType.ApplyGridSurface =>
                    $"{(SurfaceType)el.FindPropertyRelative("SurfaceToApply").enumValueIndex} " +
                    $"{el.FindPropertyRelative("SurfaceDuration").intValue}T",
                _ => string.Empty
            };
        }

        // ── Generic foldout section ───────────────────────────────────────────

        private static bool DrawFoldout(bool isOpen, string label, System.Action drawContents)
        {
            EditorGUILayout.BeginVertical("box");

            isOpen = EditorGUILayout.Foldout(isOpen, label, true, EditorStyles.foldoutHeader);
            if (isOpen)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(2);
                drawContents();
                EditorGUILayout.Space(2);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
            return isOpen;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void Prop(SerializedProperty parent, string field, string label) =>
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(field), new GUIContent(label));

        private static void Swap<T>(List<T> list, int a, int b)
        {
            if (a < 0 || b < 0 || a >= list.Count || b >= list.Count) return;
            (list[a], list[b]) = (list[b], list[a]);
        }

        private static Color TypeToColor(PokemonType type) => type switch
        {
            PokemonType.Fire     => new Color(1.0f, 0.5f, 0.1f),
            PokemonType.Water    => new Color(0.3f, 0.6f, 1.0f),
            PokemonType.Electric => new Color(1.0f, 0.9f, 0.1f),
            PokemonType.Grass    => new Color(0.3f, 0.8f, 0.3f),
            PokemonType.Ice      => new Color(0.5f, 0.9f, 1.0f),
            PokemonType.Fighting => new Color(0.8f, 0.3f, 0.2f),
            PokemonType.Poison   => new Color(0.7f, 0.3f, 0.9f),
            PokemonType.Ground   => new Color(0.8f, 0.7f, 0.3f),
            PokemonType.Flying   => new Color(0.5f, 0.7f, 1.0f),
            PokemonType.Psychic  => new Color(1.0f, 0.3f, 0.6f),
            PokemonType.Bug      => new Color(0.6f, 0.8f, 0.1f),
            PokemonType.Rock     => new Color(0.6f, 0.5f, 0.3f),
            PokemonType.Ghost    => new Color(0.4f, 0.3f, 0.6f),
            PokemonType.Dragon   => new Color(0.4f, 0.2f, 0.9f),
            PokemonType.Dark     => new Color(0.4f, 0.3f, 0.3f),
            PokemonType.Steel    => new Color(0.6f, 0.7f, 0.8f),
            PokemonType.Fairy    => new Color(1.0f, 0.5f, 0.8f),
            _                   => new Color(0.7f, 0.7f, 0.7f),
        };
    }
}
#endif
