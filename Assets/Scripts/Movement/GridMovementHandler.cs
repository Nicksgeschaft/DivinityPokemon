using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.Units;

namespace PokemonAdventure.Movement
{
    // ==========================================================================
    // Grid Movement Handler
    // Validates and executes movement requests for units on the combat grid.
    // Separates: pathfinding (PathfindingBase), cost calculation (MovementCostCalculator),
    // and actual unit movement execution (here).
    //
    // Usage:
    //   var request = GridMovementHandler.BuildRequest(unit, targetCell, gridManager);
    //   if (request.IsValid)
    //       StartCoroutine(GridMovementHandler.ExecuteMovement(request, gridManager));
    // ==========================================================================

    public static class GridMovementHandler
    {
        // ── Request Building ──────────────────────────────────────────────────

        /// <summary>
        /// Validates a movement intent and builds a MovementRequest.
        /// Does NOT execute the movement. Call ExecuteMovement() separately.
        /// </summary>
        public static MovementRequest BuildRequest(
            BaseUnit unit,
            Vector2Int targetCell,
            WorldGridManager gridManager)
        {
            if (unit == null)
                return new MovementRequest(unit, targetCell, "Unit is null.");

            if (!unit.IsAlive)
                return new MovementRequest(unit, targetCell, "Unit is dead.");

            var state = unit.RuntimeState;

            // Status: Rooted
            if (state.HasStatus(Data.StatusEffectType.Rooted))
                return new MovementRequest(unit, targetCell, "Unit is rooted.");

            // Validate target cell
            var target = gridManager.GetCell(targetCell);
            if (target == null)
                return new MovementRequest(unit, targetCell, "Target cell is out of bounds.");

            if (!target.IsPassable)
                return new MovementRequest(unit, targetCell, "Target cell is not passable.");

            // Direct interleaved path first (R-U-R-U zigzag); A* fallback if blocked
            var startCell  = gridManager.GetCell(state.GridPosition);
            var directPath = PathfindingBase.BuildDirectPath(state.GridPosition, targetCell, gridManager);
            if (directPath == null)
                Debug.LogWarning($"[Movement] Direct path blocked {state.GridPosition}→{targetCell}, using A*");
            var path = directPath ?? PathfindingBase.FindPath(startCell, target, gridManager);
            if (path == null)
                return new MovementRequest(unit, targetCell, "No path to target.");

            // AP cost
            int apCost = MovementCostCalculator.CalculatePathCost(path, state);
            if (!state.CanAfford(apCost))
                return new MovementRequest(unit, targetCell,
                    $"Not enough AP (need {apCost}, have {state.CurrentAP}).");

            return new MovementRequest(unit, targetCell, path, apCost);
        }

        // ── Movement Execution ────────────────────────────────────────────────

        /// <summary>
        /// Executes a validated MovementRequest, animating the unit along the path.
        /// Must be called as a coroutine (StartCoroutine).
        /// </summary>
        public static IEnumerator ExecuteMovement(
            MovementRequest request,
            WorldGridManager gridManager,
            float moveSpeed = 8f)
        {
            if (request == null || !request.IsValid) yield break;

            var unit  = request.Unit;
            var state = unit.RuntimeState;

            // AP is spent by the caller (CombatMovementController) via ActionPointController
            // so APChangedEvent fires and the UI updates correctly.
            state.HasMovedThisTurn = true;

            GameEventBus.Publish(new MovementStartedEvent
            {
                UnitId    = unit.UnitId,
                FromCell  = state.GridPosition,
                ToCell    = request.TargetCell
            });

            Vector2Int prevCell = state.GridPosition;

            foreach (var cell in request.Path)
            {
                // Animate unit moving to next cell world position
                var targetPos = gridManager.GetWorldPosition(cell.GridPosition);
                yield return MoveToPosition(unit.transform, targetPos, moveSpeed);

                // Update grid occupancy one step at a time
                gridManager.GetCell(prevCell)?.ClearOccupant();
                unit.PlaceOnCell(cell.GridPosition);

                // Store partial position in case of interruption (TODO: mid-move reactions)
                state.PendingMovementTarget = cell.GridPosition;

                prevCell = cell.GridPosition;
            }

            state.PendingMovementTarget = null;

            GameEventBus.Publish(new MovementCompletedEvent
            {
                UnitId    = unit.UnitId,
                FromCell  = request.Unit.RuntimeState.GridPosition, // Already updated
                ToCell    = request.TargetCell,
                APSpent   = request.APCost
            });
        }

        // ── Animation Helper ──────────────────────────────────────────────────

        private static IEnumerator MoveToPosition(Transform t, Vector3 target, float speed)
        {
            while (Vector3.Distance(t.position, target) > 0.01f)
            {
                var dir = (target - t.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                    t.rotation = Quaternion.RotateTowards(
                        t.rotation,
                        Quaternion.LookRotation(dir, Vector3.up),
                        720f * Time.deltaTime);

                t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
                yield return null;
            }
            t.position = target;
        }

        // ── Range Visualisation ───────────────────────────────────────────────

        /// <summary>
        /// Returns all cells within a unit's current AP-based movement range.
        /// Used by the UI to highlight valid destinations before a move is committed.
        /// </summary>
        public static HashSet<Vector2Int> GetMovementRange(BaseUnit unit, WorldGridManager gridManager)
        {
            bool isWild = unit.Faction == Data.UnitFaction.Hostile;
            int rangeInCells = MovementCostCalculator.GetMovementRangeInCells(unit.RuntimeState, unit.Stats, isWild);
            return PathfindingBase.GetReachableCells(
                unit.RuntimeState.GridPosition,
                rangeInCells,
                gridManager);
        }
    }
}
