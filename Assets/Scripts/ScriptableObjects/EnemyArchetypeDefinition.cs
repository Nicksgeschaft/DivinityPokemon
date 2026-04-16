using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.ScriptableObjects
{
    // ==========================================================================
    // Enemy Archetype Definition (ScriptableObject)
    // Defines an enemy type used by EnemyUnit and AIController.
    // One asset per enemy archetype (e.g. "Rattata Scout", "Onix Tank").
    // Create via: Assets → Create → PokemonAdventure → EnemyArchetypeDefinition
    // ==========================================================================

    [CreateAssetMenu(
        menuName = "PokemonAdventure/EnemyArchetypeDefinition",
        fileName = "New EnemyArchetype",
        order    = 4)]
    public class EnemyArchetypeDefinition : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Header("Identity")]
        public string EnemyName = "Unknown Enemy";
        public UnitRole Role    = UnitRole.Attacker;

        [TextArea(2, 4)]
        public string BehaviourDescription;

        // ── Base Stats ────────────────────────────────────────────────────────

        [Header("Base Stats")]
        public UnitStats BaseStats = new();

        // ── Vision ────────────────────────────────────────────────────────────

        [Header("Vision & Stealth")]
        [Tooltip("Sight range in grid cells (triggers combat when Friendly unit enters range).")]
        [Range(1, 20)] public int SightRange = 6;

        [Tooltip("Whether this enemy uses stealth behaviour in the overworld.")]
        public bool IsAmbusher;

        [Tooltip("Stealth level when IsAmbusher is true.")]
        [Min(0)] public int StealthLevel;

        // ── Skills ────────────────────────────────────────────────────────────

        [Header("Skills")]
        public List<SkillDefinition> StartingSkills = new();

        // ── AI Behaviour ──────────────────────────────────────────────────────

        [Header("AI Behaviour")]
        [Tooltip("Primary target selection strategy.")]
        public AITargetPriority TargetPriority = AITargetPriority.LowestHP;

        [Tooltip("Primary skill usage preference.")]
        public AISkillPreference SkillPreference = AISkillPreference.HighestDamage;

        [Tooltip("How aggressively this enemy closes distance.")]
        public AIMovementStyle MovementStyle = AIMovementStyle.Aggressive;

        [Tooltip("Threshold (0-1 fraction of MaxHP) at which the AI considers fleeing. 0 = never flees.")]
        [Range(0f, 1f)] public float FleeThreshold;

        // ── Visuals ───────────────────────────────────────────────────────────

        [Header("Visuals")]
        public GameObject UnitPrefab;
        public Sprite     PortraitSprite;

        // ── Loot ─────────────────────────────────────────────────────────────

        [Header("Loot")]
        [Tooltip("Items this enemy may drop on defeat.")]
        public List<LootEntry> LootTable = new();
    }

    // ── Supporting Types ──────────────────────────────────────────────────────

    [System.Serializable]
    public class LootEntry
    {
        public ItemDefinition Item;
        [Range(0f, 1f)] public float DropChance = 0.25f;
        [Min(1)] public int Quantity = 1;
    }

    // ── AI Behaviour Enums ────────────────────────────────────────────────────

    public enum AITargetPriority
    {
        LowestHP,           // Focus the most damaged unit
        HighestHP,          // Focus the tankiest unit
        HighestThreat,      // Focus the unit that deals the most damage
        LowestArmor,        // Focus the least-armored unit
        Nearest,            // Focus the geographically closest unit
        Random              // Choose randomly each turn
    }

    public enum AISkillPreference
    {
        HighestDamage,      // Always use the highest base-power skill available
        MatchType,          // Prefer STAB (same-type attack bonus)
        ApplyStatus,        // Prefer skills that apply debuffs
        AoEFirst,           // Prefer area skills when multiple targets are clustered
        Balanced            // Mix of damage and utility
    }

    public enum AIMovementStyle
    {
        Aggressive,         // Always move toward the nearest enemy
        Defensive,          // Maintain distance, prefer ranged skills
        Flanking,           // Attempt to approach from behind / sides
        Stationary,         // Does not voluntarily move (turret-style)
        Patrol              // Follows a patrol route outside combat
    }
}
