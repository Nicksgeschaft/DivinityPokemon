using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.ScriptableObjects
{
    // ==========================================================================
    // Pokemon Definition (ScriptableObject)
    // Design-time data for a playable Pokémon character. One asset per species.
    // Instanced data (level, current HP, etc.) lives in RuntimeUnitState.
    //
    // Create via: Assets → Create → PokemonAdventure → PokemonDefinition
    // ==========================================================================

    [CreateAssetMenu(
        menuName = "PokemonAdventure/PokemonDefinition",
        fileName = "New PokemonDefinition",
        order    = 1)]
    public class PokemonDefinition : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Header("Identity")]
        public string PokemonName = "Unknown";
        public int    NationalDexNumber;

        [TextArea(2, 4)]
        public string Description;

        // ── Type ──────────────────────────────────────────────────────────────

        [Header("Type")]
        public PokemonType PrimaryType   = PokemonType.Normal;
        [Tooltip("Set to None if this is a single-type Pokémon.")]
        public PokemonType SecondaryType = PokemonType.None;

        // ── Role / Archetype ──────────────────────────────────────────────────

        [Header("Role")]
        public UnitRole ArchetypeRole = UnitRole.None;

        [Tooltip("Free-form archetype tags for filtering and AI logic (e.g. 'tank', 'glass-cannon').")]
        public List<string> ArchetypeTags = new();

        // ── Base Stats ────────────────────────────────────────────────────────

        [Header("Base Stats")]
        [Tooltip("These are the species base stats, NOT the actual unit stats at runtime.")]
        public UnitStats BaseStats = new();

        // ── Starting Skills ───────────────────────────────────────────────────

        [Header("Starting Skills")]
        [Tooltip("Skills this Pokémon knows at level 1.")]
        public List<SkillDefinition> StartingSkills = new();

        [Tooltip("Skills that can be learned on level-up or via TM (for future learnable-skills system).")]
        public List<LearnableSkill> LearnableSkills = new();

        // ── Visuals ───────────────────────────────────────────────────────────

        [Header("Visuals")]
        [Tooltip("Prefab spawned in the overworld and combat. Must have BaseUnit component.")]
        public GameObject UnitPrefab;

        [Tooltip("PMD-style 8-directional sprite animation set for overworld and combat.")]
        public PokemonAnimationSet AnimationSet;

        [Header("Portrait Emotions")]
        [Tooltip("One sprite per emotion. OnValidate auto-adds missing slots — just drag sprites into each.")]
        public List<EmotionPortrait> EmotionPortraits = new();

        // ── Portrait API ──────────────────────────────────────────────────────

        public Sprite GetPortrait(PortraitEmotion emotion)
        {
            foreach (var entry in EmotionPortraits)
                if (entry.Emotion == emotion && entry.Portrait != null)
                    return entry.Portrait;

            // Fallback: Normal portrait
            foreach (var entry in EmotionPortraits)
                if (entry.Emotion == PortraitEmotion.Normal && entry.Portrait != null)
                    return entry.Portrait;

            return null;
        }

        // ── Editor Helpers ────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            var all = (PortraitEmotion[])System.Enum.GetValues(typeof(PortraitEmotion));
            foreach (var emotion in all)
            {
                bool exists = false;
                foreach (var e in EmotionPortraits)
                    if (e.Emotion == emotion) { exists = true; break; }

                if (!exists)
                    EmotionPortraits.Add(new EmotionPortrait { Emotion = emotion });
            }
        }
#endif

        // ── Audio ─────────────────────────────────────────────────────────────

        [Header("Audio")]
        [Tooltip("Pokémon cry sound clip.")]
        public AudioClip CryClip;
    }

    // ==========================================================================
    // Learnable Skill Entry
    // ==========================================================================

    [System.Serializable]
    public class LearnableSkill
    {
        [Min(1)] public int Level;
        public SkillDefinition Skill;
    }

    // ==========================================================================
    // Portrait Emotion
    // 20 discrete emotional states shown in the bottom HUD portrait.
    // Index order matches the source asset naming convention.
    // ==========================================================================

    public enum PortraitEmotion
    {
        Normal       =  0,
        Happy        =  1,
        Pain         =  2,
        Angry        =  3,
        Worried      =  4,
        Sad          =  5,
        Crying       =  6,
        Shouting     =  7,
        Teary        =  8,
        Determined   =  9,
        Joyous       = 10,
        Inspired     = 11,
        Surprised    = 12,
        Dizzy        = 13,  // used for KO state
        HardShouting = 14,
        Relieved     = 15,
        Sigh         = 16,
        Stunned      = 17,
        WorriedAlt   = 18,
        FeelGood     = 19,
    }

    [System.Serializable]
    public class EmotionPortrait
    {
        public PortraitEmotion Emotion;
        public Sprite          Portrait;
    }
}
