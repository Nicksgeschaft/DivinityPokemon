using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Damage Result
    // Immutable record of a single damage resolution pass.
    // Carries all intermediate values for logging, VFX decisions, and UI.
    //
    // Created by SkillResolver.Resolve(). Passed to DamageDealtEvent.
    // ==========================================================================

    public sealed class DamageResult
    {
        // ── Input ─────────────────────────────────────────────────────────────

        public string AttackerUnitId   { get; set; }
        public string DefenderUnitId   { get; set; }
        public string SkillId          { get; set; }

        // ── Pipeline Stages ───────────────────────────────────────────────────

        /// <summary>Damage before any multipliers.</summary>
        public float RawDamage          { get; set; }

        /// <summary>Type chart multiplier (0, 0.25, 0.5, 1, 2, or 4).</summary>
        public float TypeMultiplier     { get; set; }

        /// <summary>STAB bonus (1.5 if applicable, else 1.0).</summary>
        public float STABMultiplier     { get; set; }

        /// <summary>All other modifiers combined (status, terrain, buffs, etc.).</summary>
        public float OtherModifiers     { get; set; }

        /// <summary>Final damage after all modifiers: Raw × Type × STAB × Other.</summary>
        public float FinalDamage        { get; set; }

        // ── Armor / HP Split ──────────────────────────────────────────────────

        /// <summary>How much FinalDamage was absorbed by the armor bar.</summary>
        public float ArmorAbsorbed      { get; set; }

        /// <summary>How much overflowed past armor and hit HP directly.</summary>
        public float HPDamage           { get; set; }

        // ── Metadata ──────────────────────────────────────────────────────────

        public EffectivenessCategory Effectiveness { get; set; }
        public bool                  WasKill       { get; set; }
        public bool                  IsMiss        { get; set; }

        /// <summary>Named modifiers for debug display. See DamageModifierPipeline.</summary>
        public IReadOnlyList<DamageModifier> AppliedModifiers { get; set; }

        public override string ToString() =>
            $"DamageResult [Raw={RawDamage:F1} Type={TypeMultiplier}x STAB={STABMultiplier}x " +
            $"Final={FinalDamage:F1} Armor={ArmorAbsorbed:F1} HP={HPDamage:F1} {Effectiveness}]";
    }

    // ==========================================================================
    // Effectiveness Category
    // Used by VFX and UI to display "Super effective!" etc.
    // ==========================================================================

    public enum EffectivenessCategory
    {
        Immune,             // Multiplier == 0
        QuarterEffective,   // Multiplier == 0.25
        HalfEffective,      // Multiplier == 0.5
        Normal,             // Multiplier == 1
        SuperEffective,     // Multiplier == 2
        DoubleSuper         // Multiplier == 4 (dual-type weakness)
    }

    // ==========================================================================
    // Damage Modifier Pipeline
    // Chainable, named multiplier list. Accumulates factors step by step and
    // applies them to raw damage. Named entries make debug logs readable.
    // ==========================================================================

    public sealed class DamageModifierPipeline
    {
        private readonly List<DamageModifier> _modifiers = new();

        public void Add(string name, float multiplier)
        {
            if (multiplier != 1.0f)
                _modifiers.Add(new DamageModifier { Name = name, Multiplier = multiplier });
        }

        public float Apply(float rawDamage)
        {
            float result = rawDamage;
            foreach (var mod in _modifiers)
                result *= mod.Multiplier;
            return result;
        }

        public IReadOnlyList<DamageModifier> Modifiers => _modifiers;

        public float CombinedMultiplier
        {
            get
            {
                float m = 1f;
                foreach (var mod in _modifiers) m *= mod.Multiplier;
                return m;
            }
        }
    }

    public struct DamageModifier
    {
        public string Name;
        public float  Multiplier;
        public override string ToString() => $"{Name}:{Multiplier:F2}×";
    }

    // ==========================================================================
    // IDamageFormula
    // Strategy interface for raw damage calculation.
    // Swap implementations to change the damage formula project-wide.
    // The DEFAULT formula is intentionally simple and tunable via ScalingFactor.
    // ==========================================================================

    public interface IDamageFormula
    {
        /// <summary>
        /// Calculates raw damage from skill + effect + caster stats, BEFORE type modifiers.
        /// </summary>
        float Calculate(
            ScriptableObjects.SkillDefinition skill,
            ScriptableObjects.SkillEffect effect,
            Units.IUnit caster);
    }

    /// <summary>
    /// Default formula: Power + floor(AttackStat / 2).
    /// Keeps damage numbers small and readable (2–6 range at low levels).
    /// Defense scales armor bars, not damage reduction here.
    /// </summary>
    public sealed class DefaultDamageFormula : IDamageFormula
    {
        public float Calculate(
            ScriptableObjects.SkillDefinition skill,
            ScriptableObjects.SkillEffect effect,
            Units.IUnit caster)
        {
            if (effect.Power <= 0) return 0f;

            float stat = effect.DamageCategory == DamageType.Physical
                ? caster.Stats.EffectiveAttack
                : caster.Stats.EffectiveSpecialAttack;

            return effect.Power + Mathf.Floor(stat / 2f);
        }
    }
}
