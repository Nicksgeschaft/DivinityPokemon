using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Grid;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Combat Zone Definition
    // Defines the spatial extent of a combat encounter on the world grid.
    // Zones are circular (by default) and centred on the triggering unit.
    //
    // The zone can be visualised as a grid overlay during combat.
    // Units that wander into the zone mid-combat can optionally be pulled in.
    // ==========================================================================

    public class CombatZoneDefinition : MonoBehaviour
    {
        [Header("Zone Shape")]
        [Tooltip("Radius in grid cells. Default: 7. Adjust per encounter type.")]
        [SerializeField] private float _radiusInCells = 7f;

        [Header("Dynamic Join")]
        [Tooltip("If true, hostile units that enter the zone mid-combat auto-join.")]
        [SerializeField] private bool _allowDynamicJoin = true;
        [Tooltip("World-space radius for the dynamic join trigger collider.")]
        [SerializeField] private float _joinTriggerRadius = 8f;

        [Header("Debug")]
        [SerializeField] private bool  _drawGizmo    = true;
        [SerializeField] private Color _gizmoColor   = new Color(1f, 0.5f, 0f, 0.25f);

        // ── Runtime Data ──────────────────────────────────────────────────────

        private Vector2Int         _centerCell;
        private List<GridCell>     _zoneCells;
        private WorldGridManager   _gridManager;

        public Vector2Int       CenterCell    => _centerCell;
        public IReadOnlyList<GridCell> ZoneCells => _zoneCells;
        public float            RadiusInCells => _radiusInCells;
        public bool             AllowDynamicJoin => _allowDynamicJoin;

        // ── Initialisation ────────────────────────────────────────────────────

        /// <summary>
        /// Computes the zone cell list from a world-space centre point.
        /// Called by CombatManager when an encounter begins.
        /// </summary>
        public void Initialise(Vector3 worldCenter, WorldGridManager gridManager)
        {
            _gridManager = gridManager;
            _centerCell  = gridManager.GetGridPosition(worldCenter);
            _zoneCells   = gridManager.GetCellsInCircle(_centerCell, _radiusInCells);

            // Mark zone cells
            foreach (var cell in _zoneCells)
                cell.IsInCombatZone = true;

            Debug.Log($"[CombatZoneDefinition] Zone initialised: {_zoneCells.Count} cells, " +
                      $"centre={_centerCell}, radius={_radiusInCells}");
        }

        /// <summary>Clears zone markings when combat ends.</summary>
        public void Clear()
        {
            if (_zoneCells == null) return;
            foreach (var cell in _zoneCells)
                cell.IsInCombatZone = false;
            _zoneCells = null;
        }

        // ── Queries ───────────────────────────────────────────────────────────

        public bool ContainsCell(Vector2Int pos)
        {
            if (_zoneCells == null) return false;
            return _zoneCells.Exists(c => c.GridPosition == pos);
        }

        public bool ContainsWorldPosition(Vector3 worldPos)
        {
            if (_gridManager == null) return false;
            return ContainsCell(_gridManager.GetGridPosition(worldPos));
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!_drawGizmo) return;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawWireSphere(transform.position, _joinTriggerRadius);

            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.08f);
            Gizmos.DrawSphere(transform.position, _joinTriggerRadius);
        }
    }
}
