using System;
using UnityEngine;
using PokemonAdventure.Data;

namespace PokemonAdventure.Grid
{
    // ==========================================================================
    // Grid Cell
    // Data model for a single tile in the world grid. Separates static
    // (walkability, type) from runtime (occupancy, surface) data.
    // The world outside combat looks normal but every position maps to a GridCell.
    // ==========================================================================

    [Serializable]
    public class GridCell
    {
        // ── Static / Design-time Data ─────────────────────────────────────────

        /// <summary>Column (x) and row (y) in the grid.</summary>
        public Vector2Int GridPosition;

        /// <summary>Whether units can stand on and path through this cell.</summary>
        public bool IsWalkable = true;

        /// <summary>Height level for elevation mechanics (0 = ground floor).</summary>
        public int ElevationLevel;

        // ── Runtime State ─────────────────────────────────────────────────────

        /// <summary>Is there a unit currently occupying this cell?</summary>
        public bool IsOccupied;

        /// <summary>
        /// Direct reference to the occupying unit. NonSerialized because Unity
        /// cannot serialize arbitrary object graphs; use UnitId for persistence.
        /// </summary>
        [NonSerialized] public Units.BaseUnit OccupyingUnit;

        /// <summary>Current terrain surface (may change via skills/explosions).</summary>
        public SurfaceType CurrentSurface = SurfaceType.Normal;

        /// <summary>
        /// Turns the current surface lasts. 0 = permanent until another skill overwrites it.
        /// Set in level design for preset surfaces; decremented by TurnManager each round.
        /// </summary>
        public int SurfaceDuration;

        /// <summary>Set to true when this cell falls inside the active combat zone.</summary>
        [NonSerialized] public bool IsInCombatZone;

        // ── A* Pathfinding Scratch Data (reset each pathfinding pass) ─────────

        [NonSerialized] public float GCost;    // Cost accumulated from start
        [NonSerialized] public float HCost;    // Heuristic estimate to goal
        [NonSerialized] public GridCell Parent; // Previous cell in path

        public float FCost => GCost + HCost;

        // ── Construction ──────────────────────────────────────────────────────

        public GridCell(Vector2Int gridPosition, bool isWalkable = true)
        {
            GridPosition = gridPosition;
            IsWalkable   = isWalkable;
        }

        // ── Occupancy ─────────────────────────────────────────────────────────

        public void SetOccupied(Units.BaseUnit unit)
        {
            OccupyingUnit = unit;
            IsOccupied    = unit != null;
        }

        public void ClearOccupant()
        {
            OccupyingUnit = null;
            IsOccupied    = false;
        }

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>True if this cell can be entered by a moving unit.</summary>
        public bool IsPassable => IsWalkable && !IsOccupied;

        // ── Debug ─────────────────────────────────────────────────────────────

        public override string ToString() =>
            $"GridCell({GridPosition.x},{GridPosition.y})" +
            $" Walk:{IsWalkable} Occ:{IsOccupied} Surf:{CurrentSurface} Elev:{ElevationLevel}";
    }
}
