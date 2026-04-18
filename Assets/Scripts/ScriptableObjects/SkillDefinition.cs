using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.ScriptableObjects
{
    // ==========================================================================
    // Skill Definition (ScriptableObject)
    // Complete design-time specification for one skill (move/ability).
    //
    // Effects list drives all runtime outcomes — add as many SkillEffects as
    // needed per skill (e.g. damage + apply status + surface in one skill).
    //
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
        [Tooltip("Unique machine-readable ID used by cooldown tracking and registry lookups.")]
        public string SkillId   = "skill_unnamed";
        public string SkillName = "Unknown Skill";

        [TextArea(2, 4)]
        public string Description;

        // ── Type & Category ───────────────────────────────────────────────────

        [Header("Type & Category")]
        [Tooltip("Pokémon type for STAB and type-effectiveness calculations.")]
        public PokemonType   SkillType = PokemonType.Normal;

        [Tooltip("Display classification. Actual resolution is driven by the Effects list.")]
        public SkillCategory Category  = SkillCategory.Physical;

        // ── AP & Cooldown ─────────────────────────────────────────────────────

        [Header("AP & Cooldown")]
        [Tooltip("Action Point cost to use this skill in combat.")]
        [Range(0, 6)] public int APCost = 2;

        [Tooltip("Turns before this skill can be used again. 0 = no cooldown.")]
        [Min(0)] public int Cooldown = 0;

        [Tooltip("Accuracy as a percentage (0–100). 100 = always hits.")]
        [Range(0, 100)] public int Accuracy = 100;

        // ── Range & Targeting ─────────────────────────────────────────────────

        [Header("Range & Targeting")]
        [Tooltip("Maximum distance from the caster in grid cells.")]
        [Min(1)] public int Range = 1;

        public TargetingType Targeting = TargetingType.SingleEnemy;
        public AoEShape      AreaShape = AoEShape.Single;

        [Tooltip("Radius of AoE effect in cells. Ignored when AreaShape == Single.")]
        [Min(0)] public int AoERadius = 0;

        // ── Prerequisites ─────────────────────────────────────────────────────

        [Header("Prerequisites")]
        [Tooltip("Minimum stat values required to use this skill.")]
        public List<StatRequirement> StatRequirements = new();

        [Header("Overworld")]
        [Tooltip("Can this skill be used outside of combat (e.g. Cut, Surf, Flash)?")]
        public bool UsableOutsideCombat;
        public string OverworldEffectDescription;

        // ── Visuals / Audio ───────────────────────────────────────────────────

        [Header("Visuals & Audio")]
        [Tooltip("Spawned at the impact point on skill resolution.")]
        public GameObject VFXPrefab;
        public AudioClip  SoundEffect;

        [Tooltip("Animation the caster plays when this skill is used.")]
        public PokemonAnimId CastAnimation = PokemonAnimId.Attack;

        [Tooltip("Icon shown in the skill bar slot.")]
        public Sprite SkillIcon;

        [Tooltip("Background sprite for this skill's slot in the skill bar. " +
                 "Overrides the automatic type-colour background when set.")]
        public Sprite SkillBarBackground;

        // ── Effects ───────────────────────────────────────────────────────────

        [Header("Effects")]
        [Tooltip("All outcomes applied when this skill resolves. " +
                 "Processed in list order; add multiple effects for combo moves.")]
        public List<SkillEffect> Effects = new();
    }

    // ==========================================================================
    // Stat Requirement
    // ==========================================================================

    [System.Serializable]
    public class StatRequirement
    {
        public StatType Stat;
        [Min(0)] public float MinimumValue;
    }
}
