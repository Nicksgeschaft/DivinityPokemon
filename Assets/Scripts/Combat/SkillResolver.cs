using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Skill Resolver
    // Executes a skill from caster to target, running the full damage pipeline:
    //
    //   1. Raw Damage     — IDamageFormula (swappable strategy)
    //   2. Modifier Pipeline
    //        a. Type effectiveness (TypeEffectivenessTable)
    //        b. STAB (separate from chart, see design notes)
    //        c. Status modifiers (burn = reduced attack, etc.)
    //        d. Surface modifiers (surface amplifications)
    //        e. Custom / skill-specific modifiers (TODO hook)
    //   3. Armor Application — Physical → PhysicalArmor; Special → SpecialArmor
    //   4. HP Overflow      — remaining damage after armor hits HP
    //   5. Status Hook      — apply skill's status effect with chance roll
    //
    // DESIGN NOTE:
    //   Defense / SpecialDefense are NOT used as divisors here.
    //   They scale armor bar maximums in UnitStats. The armor bars themselves
    //   act as the damage mitigation layer (step 3).
    //
    // DESIGN NOTE:
    //   Status skills (BasePower == 0) skip steps 1–4 and go directly to step 5.
    // ==========================================================================

    public static class SkillResolver
    {
        // Formula is static and swappable at runtime (e.g. from a game mode config)
        private static IDamageFormula _formula = new DefaultDamageFormula();

        public static void SetFormula(IDamageFormula formula) =>
            _formula = formula ?? new DefaultDamageFormula();

        // ── Main Entry Point ──────────────────────────────────────────────────

        /// <summary>
        /// Resolves a skill from caster → target. Applies damage and status.
        /// Returns a DamageResult (null for pure status skills that deal no damage).
        /// </summary>
        public static DamageResult Resolve(
            SkillDefinition skill,
            IUnit caster,
            IUnit target)
        {
            if (skill == null || caster == null || target == null) return null;
            if (!target.IsAlive) return null;

            // ── Accuracy check ────────────────────────────────────────────────
            if (!RollAccuracy(skill, caster)) return CreateMissResult(skill, caster, target);

            // ── Status-only skill (no damage) ─────────────────────────────────
            if (skill.Category == SkillCategory.Status || skill.BasePower <= 0)
            {
                ApplyStatusIfRolled(skill, caster, target);
                return null; // No damage result for pure status moves
            }

            // ── 1. Raw Damage ─────────────────────────────────────────────────
            float raw = _formula.Calculate(skill, caster);

            // ── 2. Modifier Pipeline ──────────────────────────────────────────
            var pipeline = new DamageModifierPipeline();

            // a) Type effectiveness
            float typeMultiplier = TypeEffectivenessTable.GetDualTypeMultiplier(
                skill.SkillType,
                target is BaseUnit bu
                    ? GetPrimaryType(bu)
                    : PokemonType.Normal,
                target is BaseUnit bu2
                    ? GetSecondaryType(bu2)
                    : PokemonType.None);
            pipeline.Add("TypeEffectiveness", typeMultiplier);

            // b) STAB — separate modifier, never baked into the chart
            float stab = GetSTABFor(skill, caster);
            pipeline.Add("STAB", stab);

            // c) Attacker status modifiers
            pipeline.Add("AttackerBurn",
                (skill.Category == SkillCategory.Physical &&
                 caster.RuntimeState.HasStatus(StatusEffectType.Burn))
                    ? 0.5f : 1.0f);

            // d) Surface modifier (placeholder — SurfaceEffectResolver fills this)
            // TODO: pipeline.Add("Surface", SurfaceEffectResolver.GetDamageModifier(skill, target));

            float finalDamage = pipeline.Apply(raw);

            // Clamp to positive
            finalDamage = Mathf.Max(0f, finalDamage);

            // ── 3 + 4. Armor Application + HP Overflow ────────────────────────
            var (armorAbsorbed, hpDamage) = ApplyDamageToTarget(skill, target, finalDamage);

            bool wasKill = !target.IsAlive;

            // ── 5. Status Application Hook ────────────────────────────────────
            ApplyStatusIfRolled(skill, caster, target);

            // ── Build Result ──────────────────────────────────────────────────
            var result = new DamageResult
            {
                AttackerUnitId   = caster.UnitId,
                DefenderUnitId   = target.UnitId,
                SkillId          = skill.SkillId,
                RawDamage        = raw,
                TypeMultiplier   = typeMultiplier,
                STABMultiplier   = stab,
                OtherModifiers   = pipeline.CombinedMultiplier / (typeMultiplier * stab),
                FinalDamage      = finalDamage,
                ArmorAbsorbed    = armorAbsorbed,
                HPDamage         = hpDamage,
                Effectiveness    = ClassifyEffectiveness(typeMultiplier),
                WasKill          = wasKill,
                AppliedModifiers = pipeline.Modifiers
            };

            // Publish for VFX, UI, logging
            GameEventBus.Publish(new DamageDealtEvent
            {
                AttackerUnitId = caster.UnitId,
                DefenderUnitId = target.UnitId,
                SkillId        = skill.SkillId,
                FinalDamage    = finalDamage,
                ArmorAbsorbed  = armorAbsorbed,
                HPDamage       = hpDamage,
                Effectiveness  = result.Effectiveness
            });

            Debug.Log($"[SkillResolver] {caster.DisplayName} used {skill.SkillName} on " +
                      $"{target.DisplayName}: {result}");

            return result;
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private static bool RollAccuracy(SkillDefinition skill, IUnit caster)
        {
            if (skill.Accuracy >= 100) return true;

            // TODO: Apply accuracy/evasion stage modifiers from RuntimeUnitState
            return Random.Range(0, 100) < skill.Accuracy;
        }

        private static (float armorAbsorbed, float hpDamage) ApplyDamageToTarget(
            SkillDefinition skill,
            IUnit target,
            float damage)
        {
            // Record state before damage for result computation
            float hpBefore    = target.RuntimeState.CurrentHP;
            float physBefore  = target.RuntimeState.CurrentPhysicalArmor;
            float specBefore  = target.RuntimeState.CurrentSpecialArmor;

            // Route through IUnit.TakeDamage which handles armor → HP flow
            target.TakeDamage(damage, skill.DamageType, null);

            float hpAfter   = target.RuntimeState.CurrentHP;
            float physAfter = target.RuntimeState.CurrentPhysicalArmor;
            float specAfter = target.RuntimeState.CurrentSpecialArmor;

            float armorAbsorbed = (physBefore - physAfter) + (specBefore - specAfter);
            float hpDamage      = hpBefore - hpAfter;

            return (armorAbsorbed, hpDamage);
        }

        private static void ApplyStatusIfRolled(SkillDefinition skill, IUnit caster, IUnit target)
        {
            if (skill.AppliedStatus == StatusEffectType.None) return;
            if (skill.StatusApplyChance <= 0) return;

            bool rolls = Random.Range(0, 100) < skill.StatusApplyChance;
            if (!rolls) return;

            var effect = new StatusEffectInstance
            {
                EffectType     = skill.AppliedStatus,
                RemainingTurns = skill.StatusDuration,
                Magnitude      = skill.StatusMagnitude,
                SourceUnitId   = caster.UnitId
            };
            target.ApplyStatusEffect(effect);
        }

        private static float GetSTABFor(SkillDefinition skill, IUnit caster)
        {
            if (caster is not BaseUnit unit) return 1.0f;
            return TypeEffectivenessTable.GetSTABMultiplier(
                skill.SkillType,
                GetPrimaryType(unit),
                GetSecondaryType(unit));
        }

        private static DamageResult CreateMissResult(SkillDefinition skill, IUnit caster, IUnit target)
        {
            Debug.Log($"[SkillResolver] {caster.DisplayName}'s {skill.SkillName} missed!");
            return new DamageResult
            {
                AttackerUnitId = caster.UnitId,
                DefenderUnitId = target.UnitId,
                SkillId        = skill.SkillId,
                FinalDamage    = 0, ArmorAbsorbed = 0, HPDamage = 0,
                Effectiveness  = EffectivenessCategory.Normal
            };
        }

        private static EffectivenessCategory ClassifyEffectiveness(float m)
        {
            if (m == 0f)    return EffectivenessCategory.Immune;
            if (m <= 0.25f) return EffectivenessCategory.QuarterEffective;
            if (m < 1f)     return EffectivenessCategory.HalfEffective;
            if (m >= 4f)    return EffectivenessCategory.DoubleSuper;
            if (m > 1f)     return EffectivenessCategory.SuperEffective;
            return EffectivenessCategory.Normal;
        }

        // TODO: Replace with a proper unit type registry once PokemonDefinition /
        //       EnemyArchetypeDefinition is reliably surfaced on every BaseUnit.
        //       Add PrimaryType / SecondaryType as virtual properties to BaseUnit
        //       that each subclass overrides. For now, fall back to Normal.
        private static PokemonType GetPrimaryType(BaseUnit unit)
        {
            if (unit is Units.PlayerUnit pu && pu.Definition != null)
                return pu.Definition.PrimaryType;
            if (unit is AI.EnemyUnit eu && eu.Archetype != null)
                return eu.Archetype.BaseStats != null ? PokemonType.Normal : PokemonType.Normal;
            return PokemonType.Normal;
        }

        private static PokemonType GetSecondaryType(BaseUnit unit)
        {
            if (unit is Units.PlayerUnit pu && pu.Definition != null)
                return pu.Definition.SecondaryType;
            return PokemonType.None;
        }
    }
}
