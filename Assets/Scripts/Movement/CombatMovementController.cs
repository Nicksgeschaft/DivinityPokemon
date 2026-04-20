using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.Units;

namespace PokemonAdventure.Movement
{
    // ==========================================================================
    // Combat Movement Controller
    // Replaces GridClickMovementController (combat portion only).
    // Handles click-to-move during combat on the player's own turn.
    //
    // VISUAL FEEDBACK:
    //   Green  = cell is reachable and affordable with current AP
    //   Red    = cell is blocked or too expensive (no AP)
    //   Yellow = hovered path overlay
    //   AP cost label follows cursor while hovering
    //
    // BLOCKED CLICKS:
    //   If the target is outside movement range or unaffordable, the click is
    //   silently ignored. The red colour already communicates the blocked state
    //   — no error popup needed.
    //
    // Attach to the same GameObject as PlayerUnit + ActionPointController.
    // ==========================================================================

    [RequireComponent(typeof(BaseUnit))]
    [RequireComponent(typeof(ActionPointController))]
    public class CombatMovementController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;

        [Header("Raycast")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float     _raycastDistance = 100f;

        [Header("Overlay Colours")]
        [SerializeField] private Color _inRangeColor    = new Color(0.2f, 0.9f, 0.2f, 0.30f);
        [SerializeField] private Color _outOfRangeColor = new Color(0.9f, 0.2f, 0.2f, 0.30f);
        [SerializeField] private Color _pathColor       = new Color(1.0f, 1.0f, 0.2f, 0.55f);

        // ── Services ──────────────────────────────────────────────────────────

        private WorldGridManager      _gridManager;
        private GameStateManager      _stateManager;
        private IPlayerInput          _input;
        private Camera                _camera;
        private BaseUnit              _unit;
        private ActionPointController _ap;

        // Resolved on first use so DebugSceneSetup has time to create it.
        private GridOverlay _overlay;
        private GridOverlay Overlay => _overlay != null
            ? _overlay
            : (_overlay = FindAnyObjectByType<GridOverlay>());

        // ── State ─────────────────────────────────────────────────────────────

        private bool                _isMyTurn;
        private bool                _isMoving;
        private bool                _skillTargetingActive;
        private bool                _isActiveControlledUnit;
        private Coroutine           _moveCoroutine;
        private HashSet<Vector2Int> _moveRange   = new();
        private List<Vector2Int>    _hoveredPath = new();
        private int                 _hoveredAPCost;
        private Vector2Int?         _hoveredCell;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _unit = GetComponent<BaseUnit>();
            _ap   = GetComponent<ActionPointController>();
        }

        private void Start()
        {
            _gridManager  = ServiceLocator.Get<WorldGridManager>();
            _stateManager = ServiceLocator.Get<GameStateManager>();
            _input        = ServiceLocator.Get<IPlayerInput>();
            _camera       = Camera.main;

            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Subscribe<APChangedEvent>(OnAPChanged);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Subscribe<SkillTargetingStartedEvent>(OnSkillTargetingStarted);
            GameEventBus.Subscribe<SkillTargetingCancelledEvent>(OnSkillTargetingEnded);
            GameEventBus.Subscribe<SkillTargetingConfirmedEvent>(OnSkillTargetingConfirmed);
            GameEventBus.Subscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Unsubscribe<APChangedEvent>(OnAPChanged);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Unsubscribe<SkillTargetingStartedEvent>(OnSkillTargetingStarted);
            GameEventBus.Unsubscribe<SkillTargetingCancelledEvent>(OnSkillTargetingEnded);
            GameEventBus.Unsubscribe<SkillTargetingConfirmedEvent>(OnSkillTargetingConfirmed);
            GameEventBus.Unsubscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);
        }

        private void Update()
        {
            if (!_isMyTurn || !_isActiveControlledUnit || _isMoving || _skillTargetingActive) return;
            if (!(_stateManager?.IsInCombat ?? false)) return;
            if (_camera == null || _gridManager == null) return;

            UpdateHover();

            if (_input != null && _input.ConfirmPressed)
                HandleClick();
        }

        // ── Click Handler ─────────────────────────────────────────────────────

        private void HandleClick()
        {
            var pos = GetCellUnderCursor();
            if (pos == null) return;

            // If the clicked cell is occupied by someone other than self,
            // let BasicAttackController handle it — bail out here.
            var cell     = _gridManager.GetCell(pos.Value);
            var occupant = cell?.OccupyingUnit as BaseUnit;
            if (occupant != null && occupant != _unit) return;

            // Silently block if out of range or not affordable (colour already communicates this)
            if (!_moveRange.Contains(pos.Value)) return;

            var request = GridMovementHandler.BuildRequest(_unit, pos.Value, _gridManager);
            if (!request.IsValid)
            {
                Debug.Log($"[CombatMovementController] Move blocked: {request.InvalidReason}");
                return;
            }

            _moveCoroutine = StartCoroutine(ExecuteMove(request));
        }

        // ── Hover ─────────────────────────────────────────────────────────────

        private void UpdateHover()
        {
            var cell = GetCellUnderCursor();
            if (cell == _hoveredCell) return;

            ClearHoveredPath();
            _hoveredCell = cell;

            if (!_hoveredCell.HasValue || _unit == null || _gridManager == null) return;

            var startCell  = _gridManager.GetCell(_unit.GridPosition);
            var targetCell = _gridManager.GetCell(_hoveredCell.Value);
            if (startCell == null || targetCell == null) return;

            var path = PathfindingBase.BuildDirectPath(_unit.GridPosition, _hoveredCell.Value, _gridManager)
                    ?? PathfindingBase.FindPath(startCell, targetCell, _gridManager);
            if (path == null || path.Count == 0) return;

            // Draw path, switching to red at the first cell outside range
            foreach (var step in path)
            {
                var pos     = step.GridPosition;
                bool inRange = _moveRange.Contains(pos);
                Overlay?.HighlightCell(pos, inRange ? _pathColor : _outOfRangeColor);
                _hoveredPath.Add(pos);
                if (!inRange) break; // Don't draw further than reachable
            }

            // Compute AP cost for label
            if (_hoveredPath.Count > 0)
            {
                var pathCells = _hoveredPath
                    .Select(p => _gridManager.GetCell(p))
                    .Where(c => c != null)
                    .ToList();
                _hoveredAPCost = MovementCostCalculator.CalculatePathCost(pathCells, _unit.RuntimeState);
            }
        }

        private void ClearHoveredPath()
        {
            foreach (var pos in _hoveredPath)
                RestoreCellColor(pos);
            _hoveredPath.Clear();
            _hoveredAPCost = 0;

            if (_hoveredCell.HasValue)
                RestoreCellColor(_hoveredCell.Value);
        }

        private void RestoreCellColor(Vector2Int pos)
        {
            if (_moveRange.Contains(pos))
                Overlay?.HighlightCell(pos, _inRangeColor);
            else
                Overlay?.HideCell(pos);
        }

        // ── Move Range ────────────────────────────────────────────────────────

        private void RefreshMoveRange()
        {
            ClearHoveredPath();
            _hoveredCell = null;

            // Clear old range highlights
            foreach (var pos in _moveRange)
                Overlay?.HideCell(pos);

            if (_isMyTurn && _stateManager?.IsInCombat == true && _unit != null)
            {
                _moveRange = GridMovementHandler.GetMovementRange(_unit, _gridManager);
                foreach (var pos in _moveRange)
                    Overlay?.HighlightCell(pos, _inRangeColor);
            }
            else
            {
                _moveRange.Clear();
            }
        }

        private void ClearAll()
        {
            ClearHoveredPath();
            foreach (var pos in _moveRange)
                Overlay?.HideCell(pos);
            _moveRange.Clear();
            _hoveredCell = null;
        }

        // ── Move Execution ────────────────────────────────────────────────────

        private IEnumerator ExecuteMove(MovementRequest request)
        {
            _isMoving = true;
            ClearAll();

            // Spend AP through ActionPointController so APChangedEvent fires and the UI updates.
            if (!_ap.SpendAP(request.APCost))
            {
                _isMoving = false;
                yield break;
            }

            yield return GridMovementHandler.ExecuteMovement(request, _gridManager, _moveSpeed);

            _isMoving = false;

            // Recompute reachable range with remaining AP
            if (_isMyTurn)
                RefreshMoveRange();
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (_unit == null) return;

            if (evt.ActiveUnitId == _unit.UnitId)
            {
                _isMyTurn = true;
                RefreshMoveRange();
            }
            else
            {
                _isMyTurn = false;
                ClearAll();
            }
        }

        private void OnTurnEnded(TurnEndedEvent evt)
        {
            if (_unit != null && evt.ActiveUnitId == _unit.UnitId)
            {
                _isMyTurn = false;
                ClearAll();
            }
        }

        private void OnAPChanged(APChangedEvent evt)
        {
            // Recompute range whenever AP changes mid-turn (spend or gain)
            if (_unit == null || evt.UnitId != _unit.UnitId) return;
            if (_isMyTurn && !_isMoving)
                RefreshMoveRange();
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState != GameState.Combat)
            {
                _isMyTurn = false;
                _skillTargetingActive = false;
                ClearAll();
            }
        }

        private void OnActiveUnitChanged(ActiveUnitChangedEvent evt)
        {
            _isActiveControlledUnit = _unit != null && evt.UnitId == _unit.UnitId;
            // Clear movement highlights when player switches away from this unit mid-turn
            if (!_isActiveControlledUnit && _isMyTurn)
                ClearAll();
        }

        private void OnSkillTargetingStarted(SkillTargetingStartedEvent evt)
        {
            if (!_isMyTurn) return;
            _skillTargetingActive = true;
            ClearAll(); // hide movement overlay while targeting
        }

        private void OnSkillTargetingEnded(SkillTargetingCancelledEvent evt)
        {
            if (!_skillTargetingActive) return;
            _skillTargetingActive = false;
            RefreshMoveRange(); // restore movement overlay
        }

        private void OnSkillTargetingConfirmed(SkillTargetingConfirmedEvent evt)
        {
            if (!_skillTargetingActive) return;
            // Delay one frame so the confirm click is not also processed as a move click
            StartCoroutine(ClearTargetingNextFrame());
        }

        private System.Collections.IEnumerator ClearTargetingNextFrame()
        {
            yield return null;
            _skillTargetingActive = false;
            if (_isMyTurn) RefreshMoveRange();
        }

        // ── AP Cost Label ─────────────────────────────────────────────────────

        private static readonly GUIStyle _apStyle    = new();
        private static bool              _styleReady = false;

        private void OnGUI()
        {
            if (!_isMyTurn || _hoveredAPCost <= 0) return;

            if (!_styleReady)
            {
                _apStyle.fontSize  = 15;
                _apStyle.fontStyle = FontStyle.Bold;
                _styleReady        = true;
            }

            _apStyle.normal.textColor = (_ap?.HasAP(_hoveredAPCost) ?? false)
                ? Color.yellow
                : Color.red;

            var mp   = _input?.CursorScreenPosition ?? Vector2.zero;
            var rect = new Rect(mp.x + 14, Screen.height - mp.y - 34, 70, 26);
            GUI.Label(rect, $"{_hoveredAPCost} AP", _apStyle);
        }

        // ── Raycast ───────────────────────────────────────────────────────────

        private Vector2Int? GetCellUnderCursor()
        {
            if (_camera == null || _gridManager == null) return null;

            var cursorPos = _input?.CursorScreenPosition ?? Vector2.zero;
            var ray = _camera.ScreenPointToRay(new Vector3(cursorPos.x, cursorPos.y, 0f));

            if (Physics.Raycast(ray, out var hit, _raycastDistance, _groundLayer))
                return _gridManager.GetGridPosition(hit.point);

            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
                return _gridManager.GetGridPosition(ray.GetPoint(dist));

            return null;
        }
    }
}
