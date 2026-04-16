using System.Collections.Generic;
using UnityEngine;

namespace PokemonAdventure.Grid
{
    // ==========================================================================
    // Grid Utility
    // Pure-static math and geometry helpers for the grid system.
    // No state. No MonoBehaviour. Safe to call from any context.
    // ==========================================================================

    public static class GridUtility
    {
        // ── Coordinate Conversion ─────────────────────────────────────────────

        /// <summary>
        /// Converts a 2D grid position to a world-space Vector3 (XZ plane).
        /// The returned position is the centre of the cell.
        /// </summary>
        public static Vector3 GridToWorld(Vector2Int gridPos, float cellSize, Vector3 gridOrigin)
        {
            return gridOrigin + new Vector3(
                gridPos.x * cellSize + cellSize * 0.5f,
                0f,
                gridPos.y * cellSize + cellSize * 0.5f
            );
        }

        /// <summary>
        /// Converts a world-space position to a 2D grid coordinate.
        /// Does NOT clamp — caller must verify bounds via WorldGridManager.IsInBounds.
        /// </summary>
        public static Vector2Int WorldToGrid(Vector3 worldPos, float cellSize, Vector3 gridOrigin)
        {
            var local = worldPos - gridOrigin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.z / cellSize)
            );
        }

        // ── Distance Metrics ──────────────────────────────────────────────────

        /// <summary>
        /// Manhattan (taxicab) distance. Use when diagonal moves are NOT allowed.
        /// Cost = |dx| + |dy|
        /// </summary>
        public static int ManhattanDistance(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>
        /// Chebyshev distance. Use when diagonal moves cost the same as cardinal.
        /// Cost = max(|dx|, |dy|)
        /// </summary>
        public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        /// <summary>True Euclidean distance (float). Use for circular radius checks.</summary>
        public static float EuclideanDistance(Vector2Int a, Vector2Int b)
            => Vector2Int.Distance(a, b);

        // ── Cell Selection ────────────────────────────────────────────────────

        /// <summary>
        /// Returns all grid positions within Manhattan distance ≤ radius.
        /// Produces a diamond/rhombus shape.
        /// </summary>
        public static List<Vector2Int> GetCellsInManhattanRange(Vector2Int center, int radius)
        {
            var result = new List<Vector2Int>();
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius)
                    result.Add(new Vector2Int(center.x + dx, center.y + dy));
            }
            return result;
        }

        /// <summary>
        /// Returns all grid positions within Euclidean radius (circle shape).
        /// Used for combat zone generation and circular AoE skills.
        /// </summary>
        public static List<Vector2Int> GetCellsInCircle(Vector2Int center, float radius)
        {
            var result = new List<Vector2Int>();
            int r = Mathf.CeilToInt(radius);
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                var candidate = new Vector2Int(center.x + dx, center.y + dy);
                if (EuclideanDistance(center, candidate) <= radius)
                    result.Add(candidate);
            }
            return result;
        }

        /// <summary>
        /// Returns all grid positions in a line from start toward direction,
        /// up to maxLength cells long.
        /// </summary>
        public static List<Vector2Int> GetCellsInLine(Vector2Int start, Vector2Int direction, int maxLength)
        {
            var result = new List<Vector2Int>();
            var dir = new Vector2Int(
                (int)Mathf.Sign(direction.x),
                (int)Mathf.Sign(direction.y)
            );
            var current = start + dir;
            for (int i = 0; i < maxLength; i++, current += dir)
                result.Add(current);
            return result;
        }

        // ── Neighbours ────────────────────────────────────────────────────────

        /// <summary>Returns the 4 orthogonal neighbours (N, S, E, W).</summary>
        public static Vector2Int[] GetNeighbours4(Vector2Int pos) => new[]
        {
            pos + Vector2Int.up,
            pos + Vector2Int.down,
            pos + Vector2Int.left,
            pos + Vector2Int.right
        };

        /// <summary>Returns all 8 neighbours including diagonals.</summary>
        public static Vector2Int[] GetNeighbours8(Vector2Int pos) => new[]
        {
            pos + new Vector2Int(-1,  1), pos + Vector2Int.up,    pos + new Vector2Int(1,  1),
            pos + Vector2Int.left,                                  pos + Vector2Int.right,
            pos + new Vector2Int(-1, -1), pos + Vector2Int.down,  pos + new Vector2Int(1, -1)
        };

        // ── Line of Sight ─────────────────────────────────────────────────────

        /// <summary>
        /// Bresenham line-of-sight check between two grid positions.
        /// Returns true if no blocking cells interrupt the line.
        /// Requires a cell-lookup delegate (e.g. WorldGridManager.GetCell).
        /// </summary>
        public static bool HasLineOfSight(
            Vector2Int from,
            Vector2Int to,
            System.Func<Vector2Int, GridCell> getCell)
        {
            // Bresenham's line algorithm
            int x  = from.x, y  = from.y;
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int err = dx - dy;

            while (x != to.x || y != to.y)
            {
                var cell = getCell(new Vector2Int(x, y));
                // Skip the origin cell itself
                if ((x != from.x || y != from.y) && (cell == null || !cell.IsWalkable))
                    return false;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx)  { err += dx; y += sy; }
            }
            return true;
        }
    }
}
