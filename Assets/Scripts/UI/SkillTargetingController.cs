using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // ==========================================================================
    // Skill Targeting Controller
    // Manages the full targeting flow when a player activates a skill in combat:
    //
    //   Idle ──BeginTargeting──► SelectingTarget ──Confirm──► Confirmed ──► Idle
    //                                │
    //                                └──Cancel──► Idle
    //
    // Responsibilities:
    //   1. Calculate and highlight valid target cells on the GridOverlay.
    //   2. Track which cell the cursor is hovering (raycast each frame).
    //   3. Validate that the hovered cell is a legal target.
    //   4. On confirm: publish SkillTargetingConfirmedEvent.
    //   5. On cancel (right click / Escape): publish SkillTargetingCancelledEvent.
    //
    // Execution of the skill is handled by a listener on SkillTargetingConfirmedEvent
    // (e.g. the future SkillExecutionSystem), not here.
    //
    // DESIGN:
    //   Raycast uses a Physics.Raycast against a dedicated GroundLayer,
    //   with a fallback to intersecting the Y=0 plane via Camera.ScreenPointToRay.
    // ==========================================================================

    public class SkillTargetingController : MonoBehaviour
    {
        [Header("Targeting Settings")]
        [Tooltip("Layer mask for the ground plane collider. Set this to your ground layer.")]
        [SerializeField] private LayerMask _groundLayer;

        [Tooltip("Max raycast distance for ground detection.")]
        [SerializeField] private float _raycastDistance = 100f;

        [Header("Overlay Colours")]
        [SerializeField] private Color _validTargetColor  = new Color(1.0f, 0.6f, 0.0f, 0.40f);
        [SerializeField] private Color _invalidCellColor  = new Color(0.4f, 0.4f, 0.4f, 0.20f);
        [SerializeField] private Color _hoverValidColor   = new Color(1.0f, 1.0f, 0.0f, 0.70f);
        [SerializeField] private Color _hoverInvalidColor = new Color(1.0f, 0.0f, 0.0f, 0.40f);
        [SerializeField] private Color _aoePreviewColor   = new Color(1.0f, 0.3f, 0.3f, 0.35f);

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges = true;

        // ── State ─────────────────────────────────────────────────────────────

        public TargetingState State { get; private set; } = TargetingState.Idle;

        private SkillDefinition     _activeSkill;
        private BaseUnit            _caster;
        private HashSet<Vector2Int> _validTargetCells = new();
        private Vector2Int?         _hoveredCell;
        private List<Vector2Int>    _previewAoECells  = new();

        // ── Services ──────────────────────────────────────────────────────────

        private IPlayerInput     _input;
        private WorldGridManager _gridManager;
        private GridOverlay      _overlay;
        private Camera           _camera;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _input       = ServiceLocator.Get<IPlayerInput>();
            _gridManager = ServiceLocator.Get<WorldGridManager>();
            _overlay     = FindFirstObjectByType<GridOverlay>();
            _camera      = Camera.main;

            GameEventBus.Subscribe<SkillTargetingStartedEvent>(OnExternalTargetingStarted);
            GameEventBus.Subscribe<SkillTargetingCancelledEvent>(OnExternalCancelled);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<SkillTargetingStartedEvent>(OnExternalTargetingStarted);
            GameEventBus.Unsubscribe<SkillTargetingCancelledEvent>(OnExternalCancelled);
        }

        private void Update()
        {
            if (State != TargetingState.SelectingTarget) return;
            if (_input == null || _gridManager == null) return;

            UpdateHover();
            HandleConfirmCancel();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Start targeting for the given skill. Called by PlayerUnitController
        /// (or indirectly via SkillTargetingStartedEvent).
        /// </summary>
        public void BeginTargeting(SkillDefinition skill, BaseUnit caster)
        {
            if (State != TargetingState.Idle) CancelTargeting();

            _activeSkill = skill;
            _caster      = caster;
            State        = TargetingState.SelectingTarget;

            _validTargetCells = CalculateValidTargetCells(skill, caster);
            ShowTargetingOverlay();

            if (_logStateChanges)
                Debug.Log($"[SkillTargetingController] Targeting {skill.SkillName} " +
                          $"— {_validTargetCells.Count} valid cells.");
        }

        /// <summary>Cancel targeting and restore the overlay to the default zone view.</summary>
        public void CancelTargeting()
        {
            if (State == TargetingState.Idle) return;

            GameEventBus.Publish(new SkillTargetingCancelledEvent
            {
                CasterUnitId = _caster?.UnitId ?? string.Empty,
                SkillId      = _activeSkill?.SkillId ?? string.Empty
            });

            ResetState();
            if (_logStateChanges) Debug.Log("[SkillTargetingController] Cancelled.");
        }

        // ── Update Loop ───────────────────────────────────────────────────────

        private void UpdateHover()
        {
            var newHover = GetCellUnderCursor();

            // Only update overlay when hovered cell changes (avoid per-frame overdraw)
            if (newHover == _hoveredCell) return;

            // Un-highlight previous hover
            if (_hoveredCell.HasValue)
                ResetHoverHighlight(_hoveredCell.Value);

            _hoveredCell = newHover;

            if (!_hoveredCell.HasValue) return;

            bool isValid = _validTargetCells.Contains(_hoveredCell.Value);

            // Highlight hovered cell
            _overlay?.HighlightCell(_hoveredCell.Value,
                isValid ? _hoverValidColor : _hoverInvalidColor);

            // Preview AoE footprint
            if (isValid && _activeSkill.AoERadius > 0)
            {
                _previewAoECells = GridUtility.GetCellsInCircle(
                    _hoveredCell.Value, _activeSkill.AoERadius);
                foreach (var c in _previewAoECells)
                    _overlay?.HighlightCell(c, _aoePreviewColor);
            }
        }

        private void HandleConfirmCancel()
        {
            if (_input.CancelPressed)
            {
                CancelTargeting();
                return;
            }

            if (_input.ConfirmPressed && _hoveredCell.HasValue)
            {
                if (_validTargetCells.Contains(_hoveredCell.Value))
                    ConfirmTarget(_hoveredCell.Value);
                else
                    Debug.Log("[SkillTargetingController] Clicked invalid cell.");
            }
        }

        // ── Confirmation ──────────────────────────────────────────────────────

        private void ConfirmTarget(Vector2Int cell)
        {
            State = TargetingState.Confirmed;

            // Find unit on target cell (if any)
            var targetCell = _gridManager.GetCell(cell);
            string targetUnitId = targetCell?.OccupyingUnit?.UnitId ?? string.Empty;

            GameEventBus.Publish(new SkillTargetingConfirmedEvent
            {
                CasterUnitId = _caster.UnitId,
                SkillId      = _activeSkill.SkillId,
                TargetCell   = cell,
                TargetUnitId = targetUnitId
            });

            if (_logStateChanges)
                Debug.Log($"[SkillTargetingController] Confirmed: {_activeSkill.SkillName} " +
                          $"→ ({cell.x},{cell.y}) target={targetUnitId}");

            ResetState();
        }

        // ── Valid Cell Calculation ────────────────────────────────────────────

        private HashSet<Vector2Int> CalculateValidTargetCells(SkillDefinition skill, BaseUnit caster)
        {
            var result      = new HashSet<Vector2Int>();
            var casterPos   = caster.RuntimeState.GridPosition;
            int range       = skill.Range;

            switch (skill.Targeting)
            {
                case TargetingType.Self:
                    result.Add(casterPos);
                    break;

                case TargetingType.SingleEnemy:
                    AddUnitsInRange(result, casterPos, range, u =>
                        u.Faction != caster.Faction && u.IsAlive);
                    break;

                case TargetingType.SingleAlly:
                    AddUnitsInRange(result, casterPos, range, u =>
                        u.Faction == caster.Faction && u.IsAlive);
                    break;

                case TargetingType.SingleAny:
                    AddUnitsInRange(result, casterPos, range, u => u.IsAlive);
                    break;

                case TargetingType.GroundTarget:
                case TargetingType.CircleAoE:
                    AddWalkableCellsInRange(result, casterPos, range);
                    break;

                case TargetingType.AllEnemies:
                case TargetingType.AllAllies:
                case TargetingType.All:
                    // No cell selection — auto-executes on confirm with no hover needed
                    // Mark caster cell as the "confirm" cell
                    result.Add(casterPos);
                    break;

                // TODO: LineForward, Cone — require direction selection (separate UI state)
                default:
                    AddWalkableCellsInRange(result, casterPos, range);
                    break;
            }

            return result;
        }

        private void AddUnitsInRange(
            HashSet<Vector2Int> result,
            Vector2Int origin,
            int range,
            System.Func<BaseUnit, bool> predicate)
        {
            var cells = GridUtility.GetCellsInManhattanRange(origin, range);
            foreach (var pos in cells)
            {
                if (!_gridManager.IsInBounds(pos)) continue;
                var cell = _gridManager.GetCell(pos);
                if (cell?.OccupyingUnit != null && predicate(cell.OccupyingUnit))
                    result.Add(pos);
            }
        }

        private void AddWalkableCellsInRange(
            HashSet<Vector2Int> result,
            Vector2Int origin,
            int range)
        {
            var cells = GridUtility.GetCellsInManhattanRange(origin, range);
            foreach (var pos in cells)
            {
                if (!_gridManager.IsInBounds(pos)) continue;
                result.Add(pos);
            }
        }

        // ── Overlay Management ────────────────────────────────────────────────

        private void ShowTargetingOverlay()
        {
            if (_overlay == null) return;

            // Show valid targets in targeting colour; invalid cells in muted colour
            var allRangeCells = GridUtility.GetCellsInManhattanRange(
                _caster.RuntimeState.GridPosition, _activeSkill.Range);

            foreach (var pos in allRangeCells)
            {
                if (!_gridManager.IsInBounds(pos)) continue;
                bool valid = _validTargetCells.Contains(pos);
                _overlay.HighlightCell(pos, valid ? _validTargetColor : _invalidCellColor);
            }
        }

        private void ResetHoverHighlight(Vector2Int pos)
        {
            bool isValid = _validTargetCells.Contains(pos);
            _overlay?.HighlightCell(pos, isValid ? _validTargetColor : _invalidCellColor);

            // Clear AoE preview
            foreach (var c in _previewAoECells)
            {
                bool prevValid = _validTargetCells.Contains(c);
                _overlay?.HighlightCell(c, prevValid ? _validTargetColor : _invalidCellColor);
            }
            _previewAoECells.Clear();
        }

        private void ResetState()
        {
            State        = TargetingState.Idle;
            _activeSkill = null;
            _caster      = null;
            _hoveredCell = null;
            _validTargetCells.Clear();
            _previewAoECells.Clear();

            // Restore overlay to default zone display
            // (CombatUIController / GridOverlay will refresh on next TurnStartedEvent)
        }

        // ── Raycasting ────────────────────────────────────────────────────────

        private Vector2Int? GetCellUnderCursor()
        {
            if (_camera == null || _gridManager == null) return null;

            var screenPos = _input?.CursorScreenPosition
                         ?? new Vector2(Input.mousePosition.x, Input.mousePosition.y);

            var ray = _camera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));

            // Try physics raycast against the ground layer first
            if (Physics.Raycast(ray, out var hit, _raycastDistance, _groundLayer))
                return _gridManager.GetGridPosition(hit.point);

            // Fallback: intersect with Y=0 plane
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
            {
                var worldPoint = ray.GetPoint(dist);
                return _gridManager.GetGridPosition(worldPoint);
            }

            return null;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnExternalTargetingStarted(SkillTargetingStartedEvent evt)
        {
            var skillRegistry = ServiceLocator.Get<SkillRegistry>();
            var unitRegistry  = ServiceLocator.Get<UnitRegistry>();

            if (skillRegistry == null || unitRegistry == null)
            {
                Debug.LogWarning("[SkillTargetingController] SkillRegistry or UnitRegistry " +
                                 "not registered — cannot start targeting.");
                return;
            }

            if (!skillRegistry.TryGet(evt.SkillId, out var skill))
            {
                Debug.LogWarning($"[SkillTargetingController] Skill '{evt.SkillId}' not in SkillRegistry.");
                return;
            }

            if (!unitRegistry.TryGet(evt.CasterUnitId, out var caster))
            {
                Debug.LogWarning($"[SkillTargetingController] Caster '{evt.CasterUnitId}' not in UnitRegistry.");
                return;
            }

            BeginTargeting(skill, caster);
        }

        private void OnExternalCancelled(SkillTargetingCancelledEvent evt)
        {
            if (State == TargetingState.SelectingTarget &&
                _caster?.UnitId == evt.CasterUnitId)
                ResetState();
        }
    }

    // ==========================================================================
    // Targeting State Enum
    // ==========================================================================

    public enum TargetingState
    {
        Idle,
        SelectingTarget,
        Confirmed,
        Cancelled
    }
}
