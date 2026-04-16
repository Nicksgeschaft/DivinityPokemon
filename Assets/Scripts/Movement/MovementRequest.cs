using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Units;
using PokemonAdventure.Grid;

namespace PokemonAdventure.Movement
{
    // ==========================================================================
    // Movement Request
    // Immutable value object that describes one intended movement.
    // Created by a UnitController or AI, validated and executed by
    // GridMovementHandler. Decouples intent from execution.
    // ==========================================================================

    public sealed class MovementRequest
    {
        public BaseUnit    Unit          { get; }
        public Vector2Int  TargetCell    { get; }
        public List<GridCell> Path       { get; }
        public int         APCost        { get; }

        /// <summary>True if the request has been validated as legal (path is clear, AP available).</summary>
        public bool IsValid { get; }

        /// <summary>Human-readable reason if IsValid == false.</summary>
        public string InvalidReason { get; }

        // ── Valid request ─────────────────────────────────────────────────────

        public MovementRequest(BaseUnit unit, Vector2Int target, List<GridCell> path, int apCost)
        {
            Unit          = unit;
            TargetCell    = target;
            Path          = path;
            APCost        = apCost;
            IsValid       = true;
            InvalidReason = string.Empty;
        }

        // ── Invalid request ───────────────────────────────────────────────────

        public MovementRequest(BaseUnit unit, Vector2Int target, string invalidReason)
        {
            Unit          = unit;
            TargetCell    = target;
            Path          = null;
            APCost        = 0;
            IsValid       = false;
            InvalidReason = invalidReason;
        }

        public override string ToString() =>
            IsValid
                ? $"MoveRequest [{Unit?.DisplayName} → ({TargetCell.x},{TargetCell.y}) " +
                  $"APCost={APCost} Steps={Path?.Count}]"
                : $"MoveRequest [INVALID: {InvalidReason}]";
    }
}
