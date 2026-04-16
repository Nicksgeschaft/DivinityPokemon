using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.ScriptableObjects
{
    // ==========================================================================
    // Skill Definition (ScriptableObject)
    // Complete design-time specification for one skill (move/ability).
    // Create via: Assets → Create → PokemonAdventure → SkillDefinition
    // ==========================================================================

    [CreateAssetMenu(
        menuName = "PokemonAdventure/SkillDefinition",
        fileName = "New SkillDefinition",
        order    = 2)]
    public class SkillDefinition : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Header("Identity")]
        public string SkillId   = "skill_unnamed"; // Unique machine-readable ID
        public string SkillName = "Unknown Skill";

        [TextArea(2, 4)]
        public string Description;

        // ── Type & Category ───────────────────────────────────────────────────

        [Header("Type & Category")]
        public PokemonType   SkillType     = PokemonType.Normal;
        public SkillCategory Category      = SkillCategory.Physical;
        public DamageType    DamageType    = DamageType.Physical;

        // ── Power & Cost ──────────────────────────────────────────────────────

        [Header("Power & Cost")]
        [Tooltip("Base damage power. 0 for status-only skills.")]
        [Min(0)] public int BasePower = 0;

        [Tooltip("AP cost to use this skill in combat.")]
        [Range(0, 6)] public int APCost = 2;

        [Tooltip("Turns before this skill can be used again. 0 = no cooldown.")]
        [Min(0)] public int Cooldown = 0;

        [Tooltip("Accuracy as a percentage (0–100). 100 = always hits.")]
        [Range(0, 100)] public int Accuracy = 100;

        // ── Range & Targeting ─────────────────────────────────────────────────

        [Header("Range & Targeting")]
        [Tooltip("Maximum range in grid cells from the caster.")]
        [Min(1)] public int Range = 1;

        public TargetingType Targeting = TargetingType.SingleEnemy;
        public AoEShape      AreaShape = AoEShape.Single;

        [Tooltip("Radius of AoE effect in cells (if AreaShape != Single).")]
        [Min(0)] public int AoERadius = 0;

        // ── Status Application ────────────────────────────────────────────────

        [Header("Status Effect")]
        [Tooltip("Status effect applied on hit. None = no effect.")]
        public StatusEffectType AppliedStatus = StatusEffectType.None;

        [Tooltip("Chance (0–100) to apply the status on hit.")]
        [Range(0, 100)] public int StatusApplyChance = 100;

        [Tooltip("Duration in turns for the applied status.")]
        [Min(1)] public int StatusDuration = 2;

        [Tooltip("Magnitude of the applied status (e.g. poison damage per tick).")]
        public float StatusMagnitude;

        // ── Prerequisites ─────────────────────────────────────────────────────

        [Header("Prerequisites")]
        [Tooltip("Minimum stat values required to use this skill (e.g. certain skills need high SpecialAttack).")]
        public List<StatRequirement> StatRequirements = new();

        // ── Overworld Availability ────────────────────────────────────────────

        [Header("Overworld")]
        [Tooltip("Can this skill be used outside of combat (e.g. Cut, Surf, Flash)?")]
        public bool UsableOutsideCombat;

        [Tooltip("Description of the overworld effect if UsableOutsideCombat is true.")]
        public string OverworldEffectDescription;

        // ── Visuals / Audio ───────────────────────────────────────────────────

        [Header("Visuals & Audio")]
        public GameObject VFXPrefab;       // Spawned at impact point
        public AudioClip  SoundEffect;
        public Sprite     SkillIcon;
    }

    // ==========================================================================
    // Stat Requirement
    // A minimum value for a specific stat needed to unlock/use a skill.
    // ==========================================================================

    [System.Serializable]
    public class StatRequirement
    {
        public StatType Stat;
        [Min(0)] public float MinimumValue;
    }
}
