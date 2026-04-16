using System.Collections;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.Movement;
using PokemonAdventure.Units;

namespace PokemonAdventure.World
{
    // ==========================================================================
    // Overworld Movement Controller
    // Replaces OverworldPlayerController + overworld click-to-move from
    // GridClickMovementController.
    //
    // Click anywhere walkable → pathfind → animate cell by cell.
    // After landing on each cell, all CombatTriggerDetectors are polled.
    // If the player has stepped inside one's sight radius, movement stops
    // immediately on that tile and the detector fires combat. No physics needed.
    //
    // Active only when GameState == Overworld.
    // Does NOT use CharacterController — movement is purely grid-based.
    //
    // Requires: PlayerUnit (or any BaseUnit) on the same GameObject.
    // ==========================================================================

    [RequireComponent(typeof(BaseUnit))]
    public class OverworldMovementController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("World units per second while walking between cells.")]
        [SerializeField] private float _moveSpeed = 5f;

        [Header("Raycast")]
        [Tooltip("Layer mask for the ground plane. Ensure your terrain/floor uses this layer.")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _raycastDistance = 100f;

        // ── Services ──────────────────────────────────────────────────────────

        private WorldGridManager _gridManager;
        private GameStateManager _stateManager;
        private Camera           _camera;
        private BaseUnit         _unit;

        // ── State ─────────────────────────────────────────────────────────────

        private Coroutine _moveCoroutine;
        private bool      _isActive;
        private bool      _stopRequested; // set true when CombatStartedEvent fires

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _unit = GetComponent<BaseUnit>();
        }

        private void Start()
        {
            _gridManager  = ServiceLocator.Get<WorldGridManager>();
            _stateManager = ServiceLocator.Get<GameStateManager>();
            _camera       = Camera.main;

            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Subscribe<CombatStartedEvent>(OnCombatStarted);

            _isActive = _stateManager?.IsInOverworld ?? true;
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Unsubscribe<CombatStartedEvent>(OnCombatStarted);
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_isActive || _camera == null || _gridManager == null) return;

            if (Input.GetMouseButtonDown(0))
                HandleClick();
        }

        // ── Click Handler ─────────────────────────────────────────────────────

        private void HandleClick()
        {
            var target = GetCellUnderCursor();
            if (target == null) return;

            // Find nearest walkable destination (accounts for clicking on
            // occupied or impassable cells)
            var dest = FindNearestWalkable(target.Value);
            if (dest == null) return;

            // Cancel any in-progress movement and restart
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            _moveCoroutine = StartCoroutine(MoveAlongPath(dest.Value));
        }

        // ── Movement Coroutine ────────────────────────────────────────────────

        private IEnumerator MoveAlongPath(Vector2Int destination)
        {
            if (_unit == null || _gridManager == null) yield break;

            var startCell = _gridManager.GetCell(_unit.GridPosition);
            var destCell  = _gridManager.GetCell(destination);
            if (startCell == null || destCell == null) yield break;

            var path = PathfindingBase.FindPath(startCell, destCell, _gridManager);
            if (path == null || path.Count == 0) yield break;

            _stopRequested = false;

            // Cache detectors once per path — FindObjectsByType is expensive per-frame
            var detectors = Object.FindObjectsByType<CombatTriggerDetector>(FindObjectsSortMode.None);

            foreach (var cell in path)
            {
                if (_stopRequested) yield break;

                var worldTarget = _gridManager.GetWorldPosition(cell.GridPosition);
                worldTarget.y   = transform.position.y; // Stay grounded

                // ── Animate to next cell ──────────────────────────────────────
                while (Vector3.Distance(transform.position, worldTarget) > 0.01f)
                {
                    if (_stopRequested) yield break;

                    var dir = (worldTarget - transform.position).normalized;
                    if (dir.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation,
                            Quaternion.LookRotation(dir, Vector3.up),
                            720f * Time.deltaTime);

                    transform.position = Vector3.MoveTowards(
                        transform.position, worldTarget, _moveSpeed * Time.deltaTime);
                    yield return null;
                }

                // Snap to exact cell center and update grid occupancy
                transform.position = worldTarget;
                UpdateGridOccupancy(cell.GridPosition);

                // ── Check combat triggers after each step ─────────────────────
                // One FixedUpdate ensures physics is in sync before the detector fires
                if (IsInsideAnyTrigger(_unit.GridPosition, detectors))
                {
                    yield return new WaitForFixedUpdate();
                    FireNearestTrigger(_unit.GridPosition, detectors);
                    yield break;
                }
            }

            _moveCoroutine = null;
        }

        // ── Grid Occupancy ────────────────────────────────────────────────────

        private void UpdateGridOccupancy(Vector2Int newPos)
        {
            if (_gridManager == null || _unit == null) return;

            var oldCell = _gridManager.GetCell(_unit.GridPosition);
            if (oldCell?.OccupyingUnit == _unit)
                oldCell.ClearOccupant();

            _unit.RuntimeState.GridPosition = newPos;

            var newCell = _gridManager.GetCell(newPos);
            newCell?.SetOccupied(_unit);
        }

        // ── Trigger Detection ─────────────────────────────────────────────────

        private bool IsInsideAnyTrigger(Vector2Int gridPos, CombatTriggerDetector[] detectors)
        {
            if (detectors == null || _gridManager == null) return false;
            var worldPos = _gridManager.GetWorldPosition(gridPos);
            foreach (var d in detectors)
            {
                if (d == null || !d.isActiveAndEnabled || d.HasFired) continue;
                if (d.IsPlayerInRange(worldPos)) return true;
            }
            return false;
        }

        private void FireNearestTrigger(Vector2Int gridPos, CombatTriggerDetector[] detectors)
        {
            if (_gridManager == null) return;
            var worldPos = _gridManager.GetWorldPosition(gridPos);

            CombatTriggerDetector best     = null;
            float                 bestDist = float.MaxValue;

            foreach (var d in detectors)
            {
                if (d == null || !d.isActiveAndEnabled || d.HasFired) continue;
                float dist = Vector3.Distance(worldPos, d.transform.position);
                if (dist <= d.SightRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best     = d;
                }
            }

            best?.FireCombat(_unit);
        }

        // ── Nearest Walkable ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the target cell if walkable, otherwise spirals outward to find
        /// the nearest walkable neighbour (max 5 cells radius).
        /// </summary>
        private Vector2Int? FindNearestWalkable(Vector2Int target)
        {
            for (int r = 0; r <= 5; r++)
            {
                foreach (var pos in GridUtility.GetCellsInManhattanRange(target, r))
                {
                    if (_gridManager.IsInBounds(pos) &&
                        _gridManager.GetCell(pos)?.IsWalkable == true)
                        return pos;
                }
            }
            return null;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            _isActive = evt.NewState == GameState.Overworld;

            if (!_isActive)
            {
                _stopRequested = true;
                if (_moveCoroutine != null)
                {
                    StopCoroutine(_moveCoroutine);
                    _moveCoroutine = null;
                }
            }
        }

        private void OnCombatStarted(CombatStartedEvent evt)
        {
            _stopRequested = true;
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
        }

        // ── Raycast ───────────────────────────────────────────────────────────

        private Vector2Int? GetCellUnderCursor()
        {
            if (_camera == null || _gridManager == null) return null;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out var hit, _raycastDistance, _groundLayer))
                return _gridManager.GetGridPosition(hit.point);

            // Fallback: intersect the Y=0 world plane
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
                return _gridManager.GetGridPosition(ray.GetPoint(dist));

            return null;
        }
    }
}
