using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.Skills
{
    // ==========================================================================
    // Status Effect Resolver
    // Processes per-turn status effects for a unit. Called at turn start/end
    // and on world ticks (for out-of-combat ticking).
    //
    // DESIGN PRINCIPLES:
    //   - All numeric values (damage%, AP loss, etc.) are READ from
    //     StatusEffectDefinition, NOT hardcoded here.
    //   - This class only routes effects to the right handler; it does not
    //     contain balance values.
    //   - Armor-gating: effects that check armor first vs hitting HP directly
    //     are flagged in StatusEffectDefinition.ArmorCheckType.
    //   - Effects are applied via IUnit interface, keeping this class
    //     independent of concrete unit types.
    //
    // ADDING A NEW STATUS EFFECT:
    //   1. Add entry to StatusEffectType enum.
    //   2. Create a StatusEffectDefinition with the desired values.
    //   3. Add a case to ProcessSingleEffect() below.
    //   4. No other files need changing.
    // ==========================================================================

    public static class StatusEffectResolver
    {
        // ── In-Combat Tick ────────────────────────────────────────────────────

        /// <summary>
        /// Process all active status effects for a unit at the start of their turn.
        /// This is the primary combat tick point.
        /// </summary>
        public static void ProcessCombatTurnStart(IUnit unit, IReadOnlyList<StatusEffectDefinition> definitions)
        {
            if (unit == null || !unit.IsAlive) return;

            var effects = unit.RuntimeState.ActiveStatusEffects;

            // Iterate backwards so removal during iteration is safe
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                var def    = FindDefinition(effect.EffectType, definitions);
                if (def == null) continue;

                ProcessSingleEffect(effect, def, unit, isCombatTick: true);
            }
        }

        // ── Out-of-Combat Tick ────────────────────────────────────────────────

        /// <summary>
        /// Process status effects on the world tick (outside combat).
        /// Only processes effects relevant outside combat (e.g. poison on journey).
        /// </summary>
        public static void ProcessWorldTick(IUnit unit, IReadOnlyList<StatusEffectDefinition> definitions)
        {
            if (unit == null || !unit.IsAlive) return;

            var effects = unit.RuntimeState.ActiveStatusEffects;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                var def    = FindDefinition(effect.EffectType, definitions);
                if (def == null) continue;

                // Out-of-combat: skip control-blocking effects (they only matter in combat)
                if (def.BlocksPlayerControl || def.BlocksMovement || def.BlocksSkillUse) continue;

                ProcessSingleEffect(effect, def, unit, isCombatTick: false);
            }
        }

        // ── Core Dispatcher ───────────────────────────────────────────────────

        private static void ProcessSingleEffect(
            StatusEffectInstance effect,
            StatusEffectDefinition def,
            IUnit unit,
            bool isCombatTick)
        {
            switch (effect.EffectType)
            {
                // ── Damage-over-time effects ───────────────────────────────────

                case StatusEffectType.Burn:
                case StatusEffectType.Poison:
                case StatusEffectType.BadPoison:
                case StatusEffectType.Cursed:
                    ApplyPerTurnDamage(effect, def, unit);
                    break;

                // ── AP drain ──────────────────────────────────────────────────

                case StatusEffectType.Stunned:
                    if (isCombatTick && def.BlocksAPGeneration)
                    {
                        // Stunned units don't gain AP this turn; handled in OnTurnStart
                        // by checking HasStatus(Stunned) before GainTurnAP().
                        // TODO: Hook into RuntimeUnitState.GainTurnAP() with a guard check.
                    }
                    break;

                // ── Control-blocking effects ──────────────────────────────────

                case StatusEffectType.Sleep:
                case StatusEffectType.Freeze:
                case StatusEffectType.Paralysis:
                case StatusEffectType.Confusion:
                    // These are checked by UnitController before accepting input.
                    // No per-tick damage — just flags already on RuntimeUnitState.
                    // TODO: Add wake-up probability check for Sleep here.
                    break;

                // ── Passive condition effects ─────────────────────────────────

                case StatusEffectType.Wet:
                case StatusEffectType.Oiled:
                case StatusEffectType.Blessed:
                case StatusEffectType.Taunt:
                case StatusEffectType.Rooted:
                case StatusEffectType.Silenced:
                case StatusEffectType.Blind:
                    // These are read when resolving attacks or movement, not ticked here.
                    break;

                // ── Flinch (single-turn) ──────────────────────────────────────

                case StatusEffectType.Flinch:
                    // Flinch expires naturally via TickStatusEffects() in BaseUnit.OnTurnStart.
                    break;

                default:
                    Debug.LogWarning($"[StatusEffectResolver] Unhandled effect type: {effect.EffectType}");
                    break;
            }
        }

        // ── Per-Turn Damage ───────────────────────────────────────────────────

        private static void ApplyPerTurnDamage(
            StatusEffectInstance effect,
            StatusEffectDefinition def,
            IUnit unit)
        {
            if (def.DamagePerTurn <= 0f) return;

            float damage;

            if (effect.EffectType == StatusEffectType.BadPoison)
            {
                // Escalating damage: Magnitude tracks turns afflicted, increases each tick
                // Magnitude used as "stacks" counter here (not hardcoded formula)
                effect.Magnitude += 1f;
                damage = def.DamagePerTurn * effect.Magnitude;
            }
            else
            {
                // Fixed magnitude — value set in StatusEffectDefinition, not hardcoded
                damage = def.DamagePerTurn * (effect.Magnitude > 0 ? effect.Magnitude : 1f);
            }

            // Armor gating: route through the appropriate damage type
            // (defined per-effect in StatusEffectDefinition.ArmorCheckType)
            unit.TakeDamage(damage, def.ArmorCheckType, null);

            GameEventBus.Publish(new UnitStatusAppliedEvent
            {
                UnitId     = unit.UnitId,
                EffectType = effect.EffectType
            });
        }

        // ── Lookup ────────────────────────────────────────────────────────────

        private static StatusEffectDefinition FindDefinition(
            StatusEffectType type,
            IReadOnlyList<StatusEffectDefinition> definitions)
        {
            if (definitions == null) return null;
            foreach (var def in definitions)
                if (def.EffectType == type) return def;

            // Graceful fallback: no definition means no per-tick processing
            return null;
        }
    }
}
