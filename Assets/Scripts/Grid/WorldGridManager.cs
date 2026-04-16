using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;

namespace PokemonAdventure.Grid
{
    // ==========================================================================
    // World Grid Manager
    // Central grid data authority. The world appears normal at runtime, but every
    // position internally corresponds to a GridCell.
    //
    // Responsibilities:
    //   - Maintain the cell array.
    //   - Provide cell access / world-to-grid conversion.
    //   - Bake walkability from scene geometry on demand.
    //   - Supply cells for combat zone definition (circular or rectangular).
    //
    // Registered with ServiceLocator by GameBootstrapper.
    // ==========================================================================

    public class WorldGridManager : MonoBehaviour
    {
        [Header("Grid Dimensions")]
        [SerializeField] private int   _gridWidth  = 60;
        [SerializeField] private int   _gridHeight = 60;
        [SerializeField] private float _cellSize   = 1f;
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;

        [Header("Walkability Baking")]
        [SerializeField] private LayerMask _obstacleLayer;
        [SerializeField] private float     _obstacleSphereRadius = 0.4f;
        [Tooltip("If true, baking runs automatically on Awake.")]
        [SerializeField] private bool _autoBakeOnAwake;

        [Header("Debug")]
        [SerializeField] private bool  _drawGizmosInEditor = true;
        [SerializeField] private Color _walkableColor  = new Color(0f, 1f, 0f, 0.08f);
        [SerializeField] private Color _blockedColor   = new Color(1f, 0f, 0f, 0.20f);
        [SerializeField] private bool  _onlyDrawSelected = true;

        // ── Internal ──────────────────────────────────────────────────────────

        private GridCell[,] _grid;
        private bool _initialized;

        // ── Public Properties ─────────────────────────────────────────────────

        public int    GridWidth  => _gridWidth;
        public int    GridHeight => _gridHeight;
        public float  CellSize   => _cellSize;
        public Vector3 GridOrigin => _gridOrigin;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            InitializeGrid();
            if (_autoBakeOnAwake)
                BakeWalkability();
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitializeGrid()
        {
            _grid = new GridCell[_gridWidth, _gridHeight];
            for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < _gridHeight; y++)
                _grid[x, y] = new GridCell(new Vector2Int(x, y));

            _initialized = true;
            Debug.Log($"[WorldGridManager] Grid {_gridWidth}×{_gridHeight} initialised " +
                      $"(cellSize={_cellSize}, origin={_gridOrigin}).");
        }

        /// <summary>
        /// Performs a physics overlap check on each cell to mark blocked tiles.
        /// Call after all static geometry is in place (or on bake button press).
        /// </summary>
        public void BakeWalkability()
        {
            if (!_initialized) return;
            int blocked = 0;
            for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < _gridHeight; y++)
            {
                var worldPos = GridUtility.GridToWorld(new Vector2Int(x, y), _cellSize, _gridOrigin);
                bool hit = Physics.CheckSphere(worldPos, _obstacleSphereRadius, _obstacleLayer);
                _grid[x, y].IsWalkable = !hit;
                if (hit) blocked++;
            }
            Debug.Log($"[WorldGridManager] Bake complete: {blocked} cells blocked.");
        }

        // ── Cell Access ───────────────────────────────────────────────────────

        public GridCell GetCell(int x, int y)
        {
            if (!IsInBounds(x, y)) return null;
            return _grid[x, y];
        }

        public GridCell GetCell(Vector2Int pos) => GetCell(pos.x, pos.y);

        public GridCell GetCellAtWorldPosition(Vector3 worldPos)
        {
            var gridPos = GridUtility.WorldToGrid(worldPos, _cellSize, _gridOrigin);
            return GetCell(gridPos);
        }

        // ── Coordinate Conversion ─────────────────────────────────────────────

        public Vector3 GetWorldPosition(Vector2Int gridPos) =>
            GridUtility.GridToWorld(gridPos, _cellSize, _gridOrigin);

        public Vector2Int GetGridPosition(Vector3 worldPos) =>
            GridUtility.WorldToGrid(worldPos, _cellSize, _gridOrigin);

        // ── Bounds ────────────────────────────────────────────────────────────

        public bool IsInBounds(int x, int y) =>
            x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight;

        public bool IsInBounds(Vector2Int pos) => IsInBounds(pos.x, pos.y);

        // ── Surface Modification ──────────────────────────────────────────────

        public void SetSurface(Vector2Int pos, SurfaceType surface)
        {
            var cell = GetCell(pos);
            if (cell != null) cell.CurrentSurface = surface;
        }

        public void SetWalkable(Vector2Int pos, bool walkable)
        {
            var cell = GetCell(pos);
            if (cell != null) cell.IsWalkable = walkable;
        }

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>Returns all valid cells within a circular radius (Euclidean).</summary>
        public List<GridCell> GetCellsInCircle(Vector2Int center, float radius)
        {
            var result = new List<GridCell>();
            foreach (var pos in GridUtility.GetCellsInCircle(center, radius))
            {
                var cell = GetCell(pos);
                if (cell != null) result.Add(cell);
            }
            return result;
        }

        /// <summary>Returns walkable, unoccupied cells in a circular radius.</summary>
        public List<GridCell> GetPassableCellsInCircle(Vector2Int center, float radius)
        {
            var cells = GetCellsInCircle(center, radius);
            cells.RemoveAll(c => !c.IsPassable);
            return cells;
        }

        /// <summary>Returns the 4 or 8 neighbouring cells (within bounds).</summary>
        public List<GridCell> GetNeighbours(Vector2Int pos, bool includeDiagonals = false)
        {
            var result    = new List<GridCell>(8);
            var positions = includeDiagonals
                ? GridUtility.GetNeighbours8(pos)
                : GridUtility.GetNeighbours4(pos);

            foreach (var p in positions)
            {
                var cell = GetCell(p);
                if (cell != null) result.Add(cell);
            }
            return result;
        }

        // ── Line of Sight ─────────────────────────────────────────────────────

        public bool HasLineOfSight(Vector2Int from, Vector2Int to) =>
            GridUtility.HasLineOfSight(from, to, GetCell);

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!_drawGizmosInEditor || _onlyDrawSelected || _grid == null) return;
            DrawGridGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmosInEditor || _grid == null) return;
            DrawGridGizmos();
        }

        private void DrawGridGizmos()
        {
            var size = new Vector3(_cellSize * 0.9f, 0.02f, _cellSize * 0.9f);
            for (int x = 0; x < _gridWidth; x++)
            for (int y = 0; y < _gridHeight; y++)
            {
                Gizmos.color = _grid[x, y].IsWalkable ? _walkableColor : _blockedColor;
                var worldPos = GridUtility.GridToWorld(new Vector2Int(x, y), _cellSize, _gridOrigin);
                Gizmos.DrawCube(worldPos, size);
            }
        }
    }
}
