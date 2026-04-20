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

        [Tooltip("Local offset applied on top of the target world position when spawning VFX. " +
                 "Use Y to raise the effect above the ground.")]
        public Vector3 VFXOffset = new Vector3(0f, 0.5f, 0f);

        public AudioClip SoundEffect;

        [Tooltip("How many seconds the sound plays. 0 = play the full clip.")]
        [Min(0f)] public float SoundEffectDuration = 0f;

        [Tooltip("Animation the caster plays when this skill is used.")]
        public PokemonAnimId CastAnimation = PokemonAnimId.Attack;

        [Tooltip("Icon shown in the skill bar slot.")]
        public Sprite SkillIcon;

        [Tooltip("Background sprite for this skill's slot in the skill bar. " +
                 "Overrides the automatic type-colour background when set.")]
        public Sprite SkillBarBackground;

        // ── Quick Damage ──────────────────────────────────────────────────────

        [Header("Quick Damage")]
        [Tooltip("Strength of this move — like Pokémon base power (e.g. 40 = weak, 80 = medium, 120 = strong). " +
                 "Used when the Effects list is empty. Formula: Strength × (AttackStat / 10). 0 = no damage.")]
        [Min(0)] public int BaseDamage = 0;

        [Tooltip("Physical = uses Attack stat and hits PhysArmor first.\nSpecial = uses Sp.Atk and hits SpecArmor first.")]
        public DamageType BaseDamageType = DamageType.Physical;

        // ── Effects ───────────────────────────────────────────────────────────

        [Header("Effects")]
        [Tooltip("Full effect list. Processed in order. BaseDamage is ignored when this is non-empty.")]
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
