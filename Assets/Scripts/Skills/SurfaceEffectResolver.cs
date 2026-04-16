using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.Units;

namespace PokemonAdventure.Skills
{
    // ==========================================================================
    // Surface Effect Resolver
    // Handles all surface/terrain interactions:
    //   - OnEnter:  unit steps onto a new surface type
    //   - OnTick:   world tick while standing on a surface
    //   - Combine:  two surface types meet → produce a result surface + explosion
    //
    // DESIGN:
    //   - All numeric data lives in SurfaceEffectDefinition (data-driven).
    //   - Combination rules live in SurfaceCombination[] (data-driven).
    //   - This class contains ONLY routing and resolution logic.
    //   - New surfaces are added by creating new SurfaceEffectDefinition assets,
    //     not by modifying this class.
    //
    // TODO: Wire OnTick to the world TickSystem and to CombatTurnFlow.OnTurnStart.
    // TODO: Create a SurfaceDatabase ScriptableObject to hold definitions + combos.
    // ==========================================================================

    public static class SurfaceEffectResolver
    {
        // ── On-Enter ──────────────────────────────────────────────────────────

        /// <summary>
        /// Called when a unit moves onto a new cell with a different surface type.
        /// Applies on-enter status and publishes events.
        /// </summary>
        public static void OnUnitEnterSurface(
            IUnit unit,
            GridCell cell,
            IReadOnlyList<SurfaceEffectDefinition> definitions)
        {
            if (unit == null || cell == null || cell.CurrentSurface == SurfaceType.Normal) return;

            var def = FindDefinition(cell.CurrentSurface, definitions);
            if (def == null) return;

            // Apply on-enter status if defined
            if (def.AppliedStatusOnEnter != StatusEffectType.None &&
                def.AppliedStatusDuration > 0)
            {
                var effect = new StatusEffectInstance
                {
                    EffectType     = def.AppliedStatusOnEnter,
                    RemainingTurns = def.AppliedStatusDuration,
                    Magnitude      = def.AppliedStatusMagnitude,
                    SourceUnitId   = "surface"
                };
                unit.ApplyStatusEffect(effect);
            }

            // TODO: Check unit type immunity (e.g. Fire-type immune to FireSurface)
        }

        // ── On-Tick ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called each turn (in combat) or each world tick (overworld) for every unit
        /// standing on a non-Normal surface.
        /// </summary>
        public static void OnSurfaceTick(
            IUnit unit,
            GridCell cell,
            IReadOnlyList<SurfaceEffectDefinition> definitions)
        {
            if (unit == null || cell == null || cell.CurrentSurface == SurfaceType.Normal) return;

            var def = FindDefinition(cell.CurrentSurface, definitions);
            if (def == null || def.DamagePerTick <= 0f) return;

            unit.TakeDamage(def.DamagePerTick, def.TickDamageType, null);
        }

        // ── Movement Cost ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the surface movement cost multiplier for a given cell.
        /// Used by MovementCostCalculator.
        /// </summary>
        public static float GetMovementCostMultiplier(
            SurfaceType surface,
            IReadOnlyList<SurfaceEffectDefinition> definitions)
        {
            var def = FindDefinition(surface, definitions);
            return def?.MovementCostMultiplier ?? 1.0f;
        }

        // ── Surface Combination ───────────────────────────────────────────────

        /// <summary>
        /// Attempts to combine two surfaces on a given cell.
        /// Example: FireSurface + WaterSurface → Normal + Wet on units.
        ///          FireSurface + OilSurface   → FireSurface + explosion damage.
        ///
        /// Applies the combination result to the cell and any units standing on it.
        /// Returns the resulting surface type, or SurfaceType.None if no combination matches.
        /// </summary>
        public static SurfaceType TryCombine(
            GridCell cell,
            SurfaceType incomingSurface,
            IReadOnlyList<SurfaceCombination> combinations,
            IReadOnlyList<SurfaceEffectDefinition> definitions,
            IUnit triggeringUnit = null)
        {
            if (cell == null) return SurfaceType.None;

            var combo = FindCombination(cell.CurrentSurface, incomingSurface, combinations);
            if (combo == null) return SurfaceType.None;

            // Apply result surface to cell
            cell.CurrentSurface = combo.Value.ResultSurface;

            // Deal combination damage (e.g. explosion) to units on cell
            if (combo.Value.ResultDamage > 0f && cell.OccupyingUnit != null)
                cell.OccupyingUnit.TakeDamage(combo.Value.ResultDamage, DamageType.True, null);

            // Apply result status to occupying unit
            if (combo.Value.ResultStatusOnUnitsInZone != StatusEffectType.None &&
                cell.OccupyingUnit != null)
            {
                cell.OccupyingUnit.ApplyStatusEffect(new StatusEffectInstance
                {
                    EffectType     = combo.Value.ResultStatusOnUnitsInZone,
                    RemainingTurns = combo.Value.ResultStatusDuration,
                    Magnitude      = 1f,
                    SourceUnitId   = "surface_combo"
                });
            }

            Debug.Log($"[SurfaceEffectResolver] {cell.GridPosition}: " +
                      $"{cell.CurrentSurface} + {incomingSurface} → {combo.Value.ResultSurface}");

            return combo.Value.ResultSurface;
        }

        // ── Lookup Helpers ────────────────────────────────────────────────────

        private static SurfaceEffectDefinition FindDefinition(
            SurfaceType surface,
            IReadOnlyList<SurfaceEffectDefinition> definitions)
        {
            if (definitions == null) return null;
            foreach (var def in definitions)
                if (def.SurfaceType == surface) return def;
            return null;
        }

        private static SurfaceCombination? FindCombination(
            SurfaceType existing,
            SurfaceType incoming,
            IReadOnlyList<SurfaceCombination> combinations)
        {
            if (combinations == null) return null;
            foreach (var combo in combinations)
            {
                // Order-independent match
                if ((combo.SourceA == existing && combo.SourceB == incoming) ||
                    (combo.SourceA == incoming && combo.SourceB == existing))
                    return combo;
            }
            return null;
        }
    }
}
