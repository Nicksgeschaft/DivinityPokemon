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

        [Tooltip("2D sprite for UI panels and status bars.")]
        public Sprite PortraitSprite;

        // ── Audio ─────────────────────────────────────────────────────────────

        [Header("Audio")]
        [Tooltip("Pokémon cry sound clip.")]
        public AudioClip CryClip;
    }

    // ==========================================================================
    // Learnable Skill Entry
    // Links a SkillDefinition to the level at which it is learned.
    // ==========================================================================

    [System.Serializable]
    public class LearnableSkill
    {
        [Min(1)] public int Level;
        public SkillDefinition Skill;
    }
}
