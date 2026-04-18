using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Grid;

namespace PokemonAdventure.Movement
{
    // ==========================================================================
    // Pathfinding Base — A* Implementation
    // Finds the shortest walkable path on the WorldGrid between two cells.
    // Designed to be called by GridMovementHandler; no MonoBehaviour dependency.
    //
    // Pluggable: replace with NavMesh, JPS, or HPA* without touching callers.
    //
    // TODO: Add jump-point pruning for performance on large open grids.
    // TODO: Add elevation cost modifier for height-based movement.
    // TODO: Cache paths for frequently-used routes.
    // ==========================================================================

    public static class PathfindingBase
    {
        // ── A* Entry Point ────────────────────────────────────────────────────

        /// <summary>
        /// Finds the shortest path from startCell to goalCell using A*.
        /// Returns an ordered list of cells (start EXCLUDED, goal INCLUDED).
        /// Returns null if no path exists.
        /// </summary>
        public static List<GridCell> FindPath(
            GridCell startCell,
            GridCell goalCell,
            WorldGridManager grid,
            bool allowDiagonals = false)
        {
            if (startCell == null || goalCell == null) return null;
            if (!goalCell.IsWalkable)                  return null;
            if (startCell == goalCell)                 return new List<GridCell>();

            var openSet   = new List<GridCell> { startCell };
            var closedSet = new HashSet<GridCell>();

            _pathStart = startCell.GridPosition;

            ResetPathData(startCell, goalCell, grid);

            startCell.GCost = 0;
            startCell.HCost = Heuristic(startCell.GridPosition, goalCell.GridPosition);
            startCell.Parent = null;

            while (openSet.Count > 0)
            {
                var current = GetLowestFCost(openSet);

                if (current == goalCell)
                    return ReconstructPath(goalCell);

                openSet.Remove(current);
                closedSet.Add(current);

                var neighbours = grid.GetNeighbours(current.GridPosition, allowDiagonals);
                foreach (var neighbour in neighbours)
                {
                    if (!neighbour.IsWalkable || closedSet.Contains(neighbour))
                        continue;

                    // TODO: Add surface-based edge cost here
                    float tentativeG = current.GCost + 1f;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                    else if (tentativeG >= neighbour.GCost)
                        continue;

                    neighbour.Parent = current;
                    neighbour.GCost  = tentativeG;
                    neighbour.HCost  = Heuristic(neighbour.GridPosition, goalCell.GridPosition);
                }
            }

            // No path found
            return null;
        }

        // ── Reachability Map ──────────────────────────────────────────────────

        /// <summary>
        /// Returns all cells reachable from origin within maxSteps using flood fill.
        /// Used to build the movement range highlight without full pathfinding per cell.
        /// </summary>
        public static HashSet<Vector2Int> GetReachableCells(
            Vector2Int origin,
            int maxSteps,
            WorldGridManager grid)
        {
            var reachable = new HashSet<Vector2Int>();
            var queue     = new Queue<(Vector2Int pos, int stepsUsed)>();
            queue.Enqueue((origin, 0));

            while (queue.Count > 0)
            {
                var (pos, steps) = queue.Dequeue();
                if (!reachable.Add(pos)) continue;
                if (steps >= maxSteps)  continue;

                foreach (var neighbour in GridUtility.GetNeighbours4(pos))
                {
                    var cell = grid.GetCell(neighbour);
                    if (cell != null && cell.IsPassable && !reachable.Contains(neighbour))
                        queue.Enqueue((neighbour, steps + 1));
                }
            }

            reachable.Remove(origin); // Don't include the unit's own cell
            return reachable;
        }

        // ── Direct Interleaved Path ───────────────────────────────────────────

        /// <summary>
        /// Builds a direct zigzag path from <paramref name="from"/> to <paramref name="to"/>
        /// using 4-directional steps only. At each step the axis with more remaining distance
        /// is chosen, producing an interleaved R-U-R-U pattern instead of L-shaped routing.
        /// Returns null if any cell on the direct line is blocked — caller falls back to A*.
        /// </summary>
        public static List<GridCell> BuildDirectPath(
            Vector2Int from,
            Vector2Int to,
            WorldGridManager grid)
        {
            var path = new List<GridCell>();
            int x = from.x, y = from.y;
            int sx = to.x > x ? 1 : -1;
            int sy = to.y > y ? 1 : -1;

            while (x != to.x || y != to.y)
            {
                int remX = Mathf.Abs(to.x - x);
                int remY = Mathf.Abs(to.y - y);

                // Step in the axis with more remaining distance; prefer X on ties
                if (remX >= remY && remX > 0)
                    x += sx;
                else
                    y += sy;

                var cell = grid.GetCell(new Vector2Int(x, y));
                if (cell == null || !cell.IsWalkable)
                    return null; // blocked — let caller use A* instead

                path.Add(cell);
            }

            return path;
        }

        // ── Internal Helpers ──────────────────────────────────────────────────

        // Stored at the start of each FindPath call for tie-breaking (single-threaded).
        private static Vector2Int _pathStart;

        private static float Heuristic(Vector2Int current, Vector2Int goal)
        {
            float h = GridUtility.ManhattanDistance(current, goal);

            // Cross-product tie-breaking: nodes on the direct line from _pathStart to goal
            // get penalty 0; nodes that deviate get a small proportional penalty.
            // This makes A* prefer zigzag paths over L-shaped ones when costs are equal.
            int dx1 = current.x - goal.x;
            int dy1 = current.y - goal.y;
            int dx2 = _pathStart.x - goal.x;
            int dy2 = _pathStart.y - goal.y;
            float cross = Mathf.Abs(dx1 * dy2 - dy1 * dx2);
            return h + cross * 0.001f;
        }

        private static GridCell GetLowestFCost(List<GridCell> list)
        {
            var best = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].FCost < best.FCost ||
                   (Mathf.Approximately(list[i].FCost, best.FCost) && list[i].HCost < best.HCost))
                    best = list[i];
            }
            return best;
        }

        private static List<GridCell> ReconstructPath(GridCell goal)
        {
            var path    = new List<GridCell>();
            var current = goal;
            while (current.Parent != null)
            {
                path.Add(current);
                current = current.Parent;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Resets GCost, HCost, Parent on all cells near the start to avoid
        /// stale data from a previous pathfinding call.
        /// A full grid reset is expensive — only reset cells in the vicinity.
        /// </summary>
        private static void ResetPathData(GridCell start, GridCell goal, WorldGridManager grid)
        {
            int radius = Mathf.CeilToInt(GridUtility.ManhattanDistance(
                start.GridPosition, goal.GridPosition) * 1.5f);
            radius = Mathf.Clamp(radius, 10, 100);

            foreach (var cell in grid.GetCellsInCircle(start.GridPosition, radius))
            {
                cell.GCost  = float.MaxValue;
                cell.HCost  = 0;
                cell.Parent = null;
            }
        }
    }
}
