using System.Collections;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Basic Attack Controller
    // Executes the unit's basic attack skill when the player clicks an adjacent
    // enemy without a skill selected.
    //
    // The actual damage / AP / VFX pipeline is handled by SkillExecutionHandler
    // so the basic attack behaves identically to any other skill.
    //
    // Setup:
    //   1. Assign a SkillDefinition to _basicAttackSkill in the Inspector.
    //   2. Attach to the same GameObject as PlayerUnit + ActionPointController.
    // ==========================================================================

    [RequireComponent(typeof(BaseUnit))]
    [RequireComponent(typeof(ActionPointController))]
    public class BasicAttackController : MonoBehaviour
    {
        [Header("Basic Attack Skill")]
        [Tooltip("SkillDefinition used when clicking an adjacent enemy with no skill selected.")]
        [SerializeField] private SkillDefinition _basicAttackSkill;

        /// <summary>Call after AddComponent when spawning via code.</summary>
        public void Initialize(SkillDefinition basicAttackSkill)
        {
            _basicAttackSkill = basicAttackSkill;
        }

        [Header("Overlay Colours")]
        [SerializeField] private Color _attackableColor = new Color(1.0f, 0.55f, 0.0f, 0.60f);
        [SerializeField] private Color _noAPAttackColor = new Color(0.9f, 0.15f, 0.1f, 0.45f);

        [Header("Raycast")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float     _raycastDistance = 100f;

        // ── Services ──────────────────────────────────────────────────────────

        private WorldGridManager      _gridManager;
        private GameStateManager      _stateManager;
        private IPlayerInput          _input;
        private GridOverlay           _overlay;
        private SkillExecutionHandler _executionHandler;
        private Camera                _camera;
        private BaseUnit              _unit;
        private ActionPointController _ap;

        // ── State ─────────────────────────────────────────────────────────────

        private bool        _isMyTurn;
        private bool        _skillTargetingActive;
        private bool        _isActiveControlledUnit;
        private Vector2Int? _highlightedCell;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _unit = GetComponent<BaseUnit>();
            _ap   = GetComponent<ActionPointController>();
        }

        private void Start()
        {
            _gridManager      = ServiceLocator.Get<WorldGridManager>();
            _stateManager     = ServiceLocator.Get<GameStateManager>();
            _input            = ServiceLocator.Get<IPlayerInput>();
            _overlay          = FindAnyObjectByType<GridOverlay>();
            _executionHandler = FindAnyObjectByType<SkillExecutionHandler>();
            _camera           = Camera.main;

            if (_basicAttackSkill == null)
                Debug.LogWarning("[BasicAttackController] No BasicAttackSkill assigned — basic attack will not work.");
            if (_executionHandler == null)
                Debug.LogWarning("[BasicAttackController] SkillExecutionHandler not found in scene.");

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
            if (!_isMyTurn || !_isActiveControlledUnit || _skillTargetingActive) return;
            if (!(_stateManager?.IsInCombat ?? false)) return;
            if (_camera == null || _gridManager == null) return;

            UpdateTargetHighlight();

            if (_input != null && _input.ConfirmPressed)
                HandleClick();
        }

        // ── Hover Highlight ───────────────────────────────────────────────────

        private void UpdateTargetHighlight()
        {
            var hovered = GetCellUnderCursor();
            var target  = GetHostileAt(hovered);
            bool isAdj  = target != null && IsAdjacent(target);

            if (_highlightedCell.HasValue && _highlightedCell != hovered)
            {
                _overlay?.HideCell(_highlightedCell.Value);
                _highlightedCell = null;
            }

            if (target == null || !isAdj) return;

            int apCost = _basicAttackSkill?.APCost ?? 1;
            var color  = _ap.HasAP(apCost) ? _attackableColor : _noAPAttackColor;
            _overlay?.HighlightCell(hovered.Value, color);
            _highlightedCell = hovered;
        }

        private void ClearHighlight()
        {
            if (_highlightedCell.HasValue)
            {
                _overlay?.HideCell(_highlightedCell.Value);
                _highlightedCell = null;
            }
        }

        // ── Click Handler ─────────────────────────────────────────────────────

        private void HandleClick()
        {
            if (_basicAttackSkill == null || _executionHandler == null) return;

            var hovered = GetCellUnderCursor();
            var target  = GetHostileAt(hovered);

            if (target == null || !IsAdjacent(target)) return;
            if (!_ap.HasAP(_basicAttackSkill.APCost)) return;

            _executionHandler.ExecuteDirect(_basicAttackSkill, _unit, target);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private BaseUnit GetHostileAt(Vector2Int? gridPos)
        {
            if (gridPos == null || _gridManager == null) return null;
            var cell     = _gridManager.GetCell(gridPos.Value);
            var occupant = cell?.OccupyingUnit as BaseUnit;
            return (occupant != null && occupant.Faction == UnitFaction.Hostile && occupant.IsAlive)
                ? occupant : null;
        }

        private bool IsAdjacent(BaseUnit target)
        {
            if (_unit == null || target == null) return false;
            return GridUtility.ManhattanDistance(_unit.GridPosition, target.GridPosition) == 1;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            _isMyTurn = (_unit != null && evt.ActiveUnitId == _unit.UnitId);
            if (!_isMyTurn) ClearHighlight();
        }

        private void OnActiveUnitChanged(ActiveUnitChangedEvent evt)
        {
            _isActiveControlledUnit = _unit != null && evt.UnitId == _unit.UnitId;
            if (!_isActiveControlledUnit) ClearHighlight();
        }

        private void OnTurnEnded(TurnEndedEvent evt)
        {
            if (_unit != null && evt.ActiveUnitId == _unit.UnitId)
            {
                _isMyTurn = false;
                ClearHighlight();
            }
        }

        private void OnAPChanged(APChangedEvent evt)
        {
            if (_unit != null && evt.UnitId == _unit.UnitId && _highlightedCell.HasValue)
            {
                _overlay?.HideCell(_highlightedCell.Value);
                _highlightedCell = null;
            }
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState != GameState.Combat)
            {
                _isMyTurn = false;
                _skillTargetingActive = false;
                ClearHighlight();
            }
        }

        private void OnSkillTargetingStarted(SkillTargetingStartedEvent evt)
        {
            _skillTargetingActive = true;
            ClearHighlight();
        }

        private void OnSkillTargetingEnded(SkillTargetingCancelledEvent evt)
        {
            _skillTargetingActive = false;
        }

        private void OnSkillTargetingConfirmed(SkillTargetingConfirmedEvent evt)
        {
            // Delay by one frame so ConfirmPressed from the confirming click
            // is no longer true when Update() next runs.
            StartCoroutine(ClearTargetingNextFrame());
        }

        private IEnumerator ClearTargetingNextFrame()
        {
            yield return null;
            _skillTargetingActive = false;
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
