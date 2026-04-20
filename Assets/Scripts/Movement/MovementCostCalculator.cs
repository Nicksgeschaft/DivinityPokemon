using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.Units;

namespace PokemonAdventure.Movement
{
    // ==========================================================================
    // Movement Cost Calculator
    // Pure functions for computing AP cost of grid movement.
    // Separated from pathfinding so cost rules can be changed independently.
    //
    // DESIGN INTENT:
    //   Base cost = 1 AP per cell.
    //   Surface modifiers stack multiplicatively.
    //   A unit's initiative tier can grant cheaper movement (planned).
    //   Status effects (Rooted, Slow) apply additional cost.
    // ==========================================================================

    public static class MovementCostCalculator
    {
        // ── AP Costs ──────────────────────────────────────────────────────────

        /// <summary>Base AP cost per grid cell moved (default: 1).</summary>
        public const int BaseAPCostPerCell = 1;

        /// <summary>Maximum cells movable per AP spent at minimum move tier.</summary>
        public const int CellsPerAPDefault = 1;

        // ── Main Entry Point ──────────────────────────────────────────────────

        /// <summary>
        /// Calculate total AP cost to move along the given path.
        /// Takes surface modifiers and unit status effects into account.
        /// </summary>
        public static int CalculatePathCost(
            System.Collections.Generic.List<GridCell> path,
            RuntimeUnitState unitState)
        {
            if (path == null || path.Count == 0) return 0;

            float totalCost = 0f;

            foreach (var cell in path)
            {
                float cellCost = BaseAPCostPerCell;

                // Surface modifier
                cellCost *= GetSurfaceCostMultiplier(cell.CurrentSurface);

                // Status modifiers
                if (unitState.HasStatus(StatusEffectType.Rooted))
                    return int.MaxValue; // Cannot move at all

                if (unitState.HasStatus(StatusEffectType.Paralysis))
                    cellCost *= 2f;

                totalCost += cellCost;
            }

            return Mathf.CeilToInt(totalCost);
        }

        /// <summary>
        /// Calculate AP cost from a unit's current position to a target cell
        /// using Manhattan distance (used for quick range checks without full pathfinding).
        /// </summary>
        public static int EstimateAPCost(Vector2Int from, Vector2Int to, RuntimeUnitState unitState)
        {
            if (unitState.HasStatus(StatusEffectType.Rooted))
                return int.MaxValue;

            int dist = GridUtility.ManhattanDistance(from, to);
            return dist * BaseAPCostPerCell;
        }

        /// <summary>
        /// Returns how many cells a unit can move this turn.
        /// Capped by the Initiative tier AND the unit's remaining AP.
        /// isWild = true uses the tighter wild Pokémon table.
        /// </summary>
        public static int GetMovementRangeInCells(RuntimeUnitState unitState, UnitStats stats, bool isWild = false)
        {
            if (unitState.HasStatus(StatusEffectType.Rooted)) return 0;

            int tier  = GetMovementTier(stats.EffectiveInitiative, isWild);
            int ap    = unitState.CurrentAP * CellsPerAPDefault;
            return Mathf.Min(tier, ap);
        }

        /// <summary>
        /// Movement tier from Initiative.
        /// Player table  : 0–29→1, 30–59→2, 60–89→3, 90–119→4, 120+→5
        /// Wild Pokémon  : 0–39→1, 40–69→2, 70–99→3, 100–129→4, 130+→5
        /// </summary>
        public static int GetMovementTier(float initiative, bool isWild)
        {
            if (isWild)
            {
                if (initiative <  40) return 1;
                if (initiative <  70) return 2;
                if (initiative < 100) return 3;
                if (initiative < 130) return 4;
                return 5;
            }
            else
            {
                if (initiative <  30) return 1;
                if (initiative <  60) return 2;
                if (initiative <  90) return 3;
                if (initiative < 120) return 4;
                return 5;
            }
        }

        // ── Surface Cost Lookup ───────────────────────────────────────────────

        private static float GetSurfaceCostMultiplier(SurfaceType surface) => surface switch
        {
            SurfaceType.MudSurface  => 2.0f,    // Very slow
            SurfaceType.IceSurface  => 0.5f,    // Slippery — cheaper but risky (fall chance)
            SurfaceType.OilSurface  => 1.5f,    // Harder to traverse
            SurfaceType.WaterSurface => 1.5f,   // Wading costs more
            _                        => 1.0f    // Normal cost
        };
    }
}
