using System.Collections.Generic;
using UnityEngine;

namespace PokemonAdventure.Grid
{
    // ==========================================================================
    // Grid Overlay
    // Shows a tile-based visual overlay during combat. Hidden in the Overworld.
    // Uses pooled quad GameObjects. Each tile has its own material instance
    // so colours can be set independently.
    //
    // Call Show(cells) to display and Hide() to remove.
    // Use the colour-coding methods to indicate movement / attack ranges.
    //
    // TODO: Replace individual GO pool with GPU-instanced mesh for large grids.
    // TODO: Add animated hover pulse shader on highlighted tiles.
    // ==========================================================================

    public class GridOverlay : MonoBehaviour
    {
        [Header("Tile Visuals")]
        [Tooltip("Simple flat quad prefab. Assign a semi-transparent unlit material.")]
        [SerializeField] private GameObject _tilePrefab;

        [Header("Colours")]
        [SerializeField] private Color _defaultColor    = new Color(0.5f, 0.8f, 1.0f, 0.20f);
        [SerializeField] private Color _movementColor   = new Color(0.2f, 0.9f, 0.2f, 0.35f);
        [SerializeField] private Color _attackColor     = new Color(1.0f, 0.2f, 0.2f, 0.35f);
        [SerializeField] private Color _skillRangeColor = new Color(0.9f, 0.6f, 0.0f, 0.30f);
        [SerializeField] private Color _hoverColor      = new Color(1.0f, 1.0f, 0.0f, 0.60f);

        private WorldGridManager _gridManager;
        private readonly Dictionary<Vector2Int, GameObject> _activeTiles = new();
        private readonly Queue<GameObject>                  _tilePool    = new();

        // Lazily-created material used when no _tilePrefab is assigned.
        private Material _runtimeMaterial;

        public bool IsVisible { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _gridManager = GetComponentInParent<WorldGridManager>()
                        ?? FindAnyObjectByType<WorldGridManager>();
        }

        private void OnDestroy()
        {
            ClearPool();
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show tiles for the given cells using the default colour.</summary>
        public void Show(IEnumerable<GridCell> cells)
        {
            HideAll();
            foreach (var cell in cells)
                GetOrCreateTile(cell.GridPosition, _defaultColor);
            IsVisible = true;
        }

        /// <summary>Colour specific cells to indicate movement range.</summary>
        public void MarkMovementRange(IEnumerable<GridCell> cells) =>
            SetCellColours(cells, _movementColor);

        /// <summary>Colour specific cells to indicate attack / skill range.</summary>
        public void MarkAttackRange(IEnumerable<GridCell> cells) =>
            SetCellColours(cells, _attackColor);

        /// <summary>Colour specific cells to indicate targeted AoE area.</summary>
        public void MarkSkillArea(IEnumerable<GridCell> cells) =>
            SetCellColours(cells, _skillRangeColor);

        /// <summary>Highlight a single cell using the default hover colour.</summary>
        public void HighlightCell(Vector2Int pos)
        {
            SetCellColour(pos, _hoverColor);
        }

        /// <summary>Highlight a single cell with an explicit colour.</summary>
        public void HighlightCell(Vector2Int pos, Color color)
        {
            SetCellColour(pos, color);
        }

        /// <summary>Reset a single cell to its default colour.</summary>
        public void ResetCellColour(Vector2Int pos)
        {
            SetCellColour(pos, _defaultColor);
        }

        /// <summary>Hide a single cell and return it to the pool.</summary>
        public void HideCell(Vector2Int pos)
        {
            if (_activeTiles.TryGetValue(pos, out var tile))
            {
                ReturnToPool(tile);
                _activeTiles.Remove(pos);
            }
        }

        /// <summary>Remove all visible tiles and return them to the pool.</summary>
        public void HideAll()
        {
            foreach (var kvp in _activeTiles)
                ReturnToPool(kvp.Value);
            _activeTiles.Clear();
            IsVisible = false;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void SetCellColours(IEnumerable<GridCell> cells, Color color)
        {
            foreach (var cell in cells)
                SetCellColour(cell.GridPosition, color);
        }

        private void SetCellColour(Vector2Int pos, Color color)
        {
            if (!_activeTiles.TryGetValue(pos, out var tile))
                tile = GetOrCreateTile(pos, color);
            ApplyColor(tile, color);
        }

        private GameObject GetOrCreateTile(Vector2Int pos, Color color)
        {
            GameObject tile;
            if (_tilePool.Count > 0)
            {
                tile = _tilePool.Dequeue();
                tile.SetActive(true);
            }
            else
            {
                tile = CreateFreshTile();
                tile.name = "OverlayTile";
            }

            if (_gridManager != null)
            {
                var worldPos = _gridManager.GetWorldPosition(pos);
                worldPos.y  += 0.02f; // Tiny offset to avoid z-fighting with ground
                tile.transform.position = worldPos;
            }

            ApplyColor(tile, color);
            _activeTiles[pos] = tile;
            return tile;
        }

        private static void ApplyColor(GameObject tile, Color color)
        {
            if (tile == null) return;
            var r = tile.GetComponent<Renderer>();
            if (r == null) return;

            // Use MaterialPropertyBlock to avoid extra material allocations
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
        }

        /// <summary>
        /// Instantiates a new tile — from the assigned prefab when available,
        /// otherwise builds a flat quad primitive so the overlay works out-of-the-box
        /// even in scenes where no prefab has been wired up.
        /// </summary>
        private GameObject CreateFreshTile()
        {
            if (_tilePrefab != null)
                return Instantiate(_tilePrefab, transform);

            // ── Runtime fallback: flat quad ──────────────────────────────────
            var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tile.transform.SetParent(transform);

            // Lay it flat on the XZ plane
            tile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Match the cell footprint (95 % to leave a 1-pixel gap between tiles)
            float size = (_gridManager?.CellSize ?? 1f) * 0.95f;
            tile.transform.localScale = new Vector3(size, size, 1f);

            // Remove the MeshCollider — overlay tiles must not interfere with raycasts
            var col = tile.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            // Assign a transparent unlit material (Sprites/Default is always available)
            if (_runtimeMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _runtimeMaterial = new Material(shader != null ? shader : Shader.Find("Standard"));
            }
            tile.GetComponent<Renderer>().material = _runtimeMaterial;

            return tile;
        }

        private void ReturnToPool(GameObject tile)
        {
            tile.SetActive(false);
            _tilePool.Enqueue(tile);
        }

        private void ClearPool()
        {
            foreach (var tile in _tilePool)
                Destroy(tile);
            _tilePool.Clear();
        }
    }
}
