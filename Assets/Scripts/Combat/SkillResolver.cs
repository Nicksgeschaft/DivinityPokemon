using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Skill Resolver
    // Iterates a skill's Effects list and applies each outcome to the target.
    //
    // Call SkillResolver.Resolve()        — per unit target, for all unit effects.
    // Call SkillResolver.ResolveGridEffects() — once per skill use, for surfaces.
    //
    // Damage pipeline (per Damage effect):
    //   1. Raw damage   — IDamageFormula (swappable strategy)
    //   2. Modifiers    — type effectiveness, STAB, status, terrain (pipeline)
    //   3. Armor → HP   — physical or special armor first, overflow hits HP
    //   4. Status hook  — applied after damage (separate ApplyStatus effects run
    //                     in their own iteration pass)
    // ==========================================================================

    public static class SkillResolver
    {
        private static IDamageFormula _formula = new DefaultDamageFormula();

        public static void SetFormula(IDamageFormula formula) =>
            _formula = formula ?? new DefaultDamageFormula();

        // ── Main Entry Point (unit targets) ───────────────────────────────────

        /// <summary>
        /// Resolves all unit-targeted effects of <paramref name="skill"/> from
        /// <paramref name="caster"/> to <paramref name="target"/>.
        /// Returns the first DamageResult produced (null for non-damaging skills).
        /// ApplyGridSurface effects are intentionally skipped here — call
        /// <see cref="ResolveGridEffects"/> separately.
        /// </summary>
        public static DamageResult Resolve(
            SkillDefinition skill,
            IUnit           caster,
            IUnit           target)
        {
            if (skill == null || caster == null || target == null) return null;
            if (!target.IsAlive) return null;

            if (!RollAccuracy(skill, caster))
                return CreateMissResult(skill, caster, target);

            DamageResult firstDamageResult = null;

            foreach (var effect in skill.Effects)
            {
                if (effect == null) continue;
                if (effect.EffectType == SkillEffectType.ApplyGridSurface) continue;

                var effectTarget = effect.Target == EffectTarget.Caster ? caster : target;
                if (effectTarget == null || !effectTarget.IsAlive) continue;

                switch (effect.EffectType)
                {
                    case SkillEffectType.Damage:
                        var dmg = ResolveDamageEffect(skill, effect, caster, effectTarget);
                        firstDamageResult ??= dmg;
                        break;

                    case SkillEffectType.Heal:
                        ResolveHealEffect(effect, effectTarget);
                        break;

                    case SkillEffectType.Shield:
                        ResolveShieldEffect(effect, effectTarget);
                        break;

                    case SkillEffectType.ApplyStatus:
                        ResolveStatusEffect(effect, caster, effectTarget);
                        break;

                    case SkillEffectType.StatModify:
                        ResolveStatModifyEffect(effect, effectTarget);
                        break;
                }
            }

            return firstDamageResult;
        }

        // ── Grid Surface Effects ──────────────────────────────────────────────

        /// <summary>
        /// Applies all ApplyGridSurface effects to <paramref name="hitCells"/>.
        /// Call once per skill use after gathering all hit cells.
        /// </summary>
        public static void ResolveGridEffects(
            SkillDefinition     skill,
            IEnumerable<GridCell> hitCells)
        {
            if (skill == null || hitCells == null) return;

            foreach (var effect in skill.Effects)
            {
                if (effect == null) continue;
                if (effect.EffectType != SkillEffectType.ApplyGridSurface) continue;

                ApplyGridSurface(effect, hitCells);
            }
        }

        // ── Per-Effect Resolvers ──────────────────────────────────────────────

        private static DamageResult ResolveDamageEffect(
            SkillDefinition skill,
            SkillEffect     effect,
            IUnit           caster,
            IUnit           target)
        {
            // 1. Raw damage
            float raw = _formula.Calculate(skill, effect, caster);

            // 2. Modifier pipeline
            var pipeline = new DamageModifierPipeline();

            float typeMultiplier = TypeEffectivenessTable.GetDualTypeMultiplier(
                skill.SkillType,
                target is BaseUnit tu  ? GetPrimaryType(tu)   : PokemonType.Normal,
                target is BaseUnit tu2 ? GetSecondaryType(tu2): PokemonType.None);
            pipeline.Add("TypeEffectiveness", typeMultiplier);

            float stab = GetSTABFor(skill, caster);
            pipeline.Add("STAB", stab);

            pipeline.Add("AttackerBurn",
                (skill.Category == SkillCategory.Physical &&
                 caster.RuntimeState.HasStatus(StatusEffectType.Burn))
                    ? 0.5f : 1.0f);

            float finalDamage = Mathf.Max(0f, pipeline.Apply(raw));

            // 3 + 4. Armor application + HP overflow
            var (armorAbsorbed, hpDamage) = ApplyDamageToTarget(effect, target, finalDamage);

            bool wasKill = !target.IsAlive;

            // Build result
            var result = new DamageResult
            {
                AttackerUnitId   = caster.UnitId,
                DefenderUnitId   = target.UnitId,
                SkillId          = skill.SkillId,
                RawDamage        = raw,
                TypeMultiplier   = typeMultiplier,
                STABMultiplier   = stab,
                OtherModifiers   = pipeline.CombinedMultiplier / Mathf.Max(0.001f, typeMultiplier * stab),
                FinalDamage      = finalDamage,
                ArmorAbsorbed    = armorAbsorbed,
                HPDamage         = hpDamage,
                Effectiveness    = ClassifyEffectiveness(typeMultiplier),
                WasKill          = wasKill,
                AppliedModifiers = pipeline.Modifiers
            };

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

            Debug.Log($"[SkillResolver] {caster.DisplayName} → {skill.SkillName} → " +
                      $"{target.DisplayName}: {result}");

            return result;
        }

        private static void ResolveHealEffect(SkillEffect effect, IUnit target)
        {
            if (effect.Power <= 0) return;
            var state = target.RuntimeState;
            float maxHP = target.Stats.MaxHP;
            state.CurrentHP = Mathf.Clamp(state.CurrentHP + effect.Power, 0f, maxHP);
            Debug.Log($"[SkillResolver] Heal {target.DisplayName} for {effect.Power} HP.");
        }

        private static void ResolveShieldEffect(SkillEffect effect, IUnit target)
        {
            if (effect.Power <= 0) return;
            var state = target.RuntimeState;
            if (effect.DamageCategory == DamageType.Physical)
            {
                float max = target.Stats.MaxPhysicalArmor;
                state.CurrentPhysicalArmor = Mathf.Clamp(state.CurrentPhysicalArmor + effect.Power, 0f, max);
            }
            else
            {
                float max = target.Stats.MaxSpecialArmor;
                state.CurrentSpecialArmor = Mathf.Clamp(state.CurrentSpecialArmor + effect.Power, 0f, max);
            }
            Debug.Log($"[SkillResolver] Shield {target.DisplayName} +{effect.Power} " +
                      $"{effect.DamageCategory} armor.");
        }

        private static void ResolveStatusEffect(SkillEffect effect, IUnit caster, IUnit target)
        {
            if (effect.StatusType == StatusEffectType.None) return;
            if (effect.ApplyChance <= 0) return;
            if (Random.Range(0, 100) >= effect.ApplyChance) return;

            target.ApplyStatusEffect(new StatusEffectInstance
            {
                EffectType     = effect.StatusType,
                RemainingTurns = effect.StatusDuration,
                Magnitude      = effect.StatusMagnitude,
                SourceUnitId   = caster.UnitId
            });
        }

        private static void ResolveStatModifyEffect(SkillEffect effect, IUnit target)
        {
            if (effect.StatModifiers == null || effect.StatModifiers.Count == 0) return;
            // TODO: Route through a StatModifierStack on RuntimeUnitState once that system exists.
            foreach (var mod in effect.StatModifiers)
                Debug.Log($"[SkillResolver] StatModify {target.DisplayName}: " +
                          $"{mod.Stat} {(mod.IsPercent ? "+" + mod.Value + "%" : "+" + mod.Value)} " +
                          $"for {mod.Duration} turns (TODO: apply).");
        }

        private static void ApplyGridSurface(SkillEffect effect, IEnumerable<GridCell> cells)
        {
            foreach (var cell in cells)
            {
                if (cell == null) continue;
                cell.CurrentSurface  = effect.SurfaceToApply;
                cell.SurfaceDuration = effect.SurfaceDuration;

                if (effect.SurfacePrefab != null)
                    Object.Instantiate(
                        effect.SurfacePrefab,
                        new Vector3(cell.GridPosition.x, 0f, cell.GridPosition.y),
                        Quaternion.identity);
            }
        }

        // ── Internal Helpers ──────────────────────────────────────────────────

        private static bool RollAccuracy(SkillDefinition skill, IUnit caster)
        {
            if (skill.Accuracy >= 100) return true;
            return Random.Range(0, 100) < skill.Accuracy;
        }

        private static (float armorAbsorbed, float hpDamage) ApplyDamageToTarget(
            SkillEffect effect,
            IUnit       target,
            float       damage)
        {
            float hpBefore   = target.RuntimeState.CurrentHP;
            float physBefore = target.RuntimeState.CurrentPhysicalArmor;
            float specBefore = target.RuntimeState.CurrentSpecialArmor;

            target.TakeDamage(damage, effect.DamageCategory, null);

            float armorAbsorbed = (physBefore - target.RuntimeState.CurrentPhysicalArmor)
                                + (specBefore - target.RuntimeState.CurrentSpecialArmor);
            float hpDamage      = hpBefore - target.RuntimeState.CurrentHP;

            return (armorAbsorbed, hpDamage);
        }

        private static float GetSTABFor(SkillDefinition skill, IUnit caster)
        {
            if (caster is not BaseUnit unit) return 1.0f;
            return TypeEffectivenessTable.GetSTABMultiplier(
                skill.SkillType, GetPrimaryType(unit), GetSecondaryType(unit));
        }

        private static DamageResult CreateMissResult(SkillDefinition skill, IUnit caster, IUnit target)
        {
            Debug.Log($"[SkillResolver] {caster.DisplayName}'s {skill.SkillName} missed!");
            return new DamageResult
            {
                AttackerUnitId = caster.UnitId, DefenderUnitId = target.UnitId,
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

        private static PokemonType GetPrimaryType(BaseUnit unit)
        {
            if (unit is Units.PlayerUnit pu && pu.Definition != null) return pu.Definition.PrimaryType;
            if (unit is AI.EnemyUnit eu && eu.Archetype != null)       return PokemonType.Normal;
            return PokemonType.Normal;
        }

        private static PokemonType GetSecondaryType(BaseUnit unit)
        {
            if (unit is Units.PlayerUnit pu && pu.Definition != null) return pu.Definition.SecondaryType;
            return PokemonType.None;
        }
    }
}
