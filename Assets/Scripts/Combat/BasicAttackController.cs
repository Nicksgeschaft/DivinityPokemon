using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Basic Attack Controller
    // Handles clicking on an adjacent enemy during the player's combat turn.
    //
    // ATTACK RULES:
    //   - Target must be a hostile unit in the grid cell under the cursor.
    //   - Target must be exactly 1 Manhattan step away (adjacent, no diagonals).
    //   - Costs exactly 1 AP. If AP < 1, the click is silently ignored.
    //
    // VISUAL FEEDBACK:
    //   - Hovered adjacent enemy with enough AP → orange highlight
    //   - Hovered adjacent enemy without AP     → red highlight
    //   - Hovered non-adjacent or friendly      → no highlight from this controller
    //
    // Attach to the same GameObject as PlayerUnit + ActionPointController.
    // ==========================================================================

    [RequireComponent(typeof(BaseUnit))]
    [RequireComponent(typeof(ActionPointController))]
    public class BasicAttackController : MonoBehaviour
    {
        public const int AttackAPCost = 1;

        [Header("Overlay Colours")]
        [SerializeField] private Color _attackableColor   = new Color(1.0f, 0.55f, 0.0f, 0.60f); // orange
        [SerializeField] private Color _noAPAttackColor   = new Color(0.9f, 0.15f, 0.1f, 0.45f); // red

        [Header("Raycast")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float     _raycastDistance = 100f;

        // ── Services ──────────────────────────────────────────────────────────

        private WorldGridManager      _gridManager;
        private GameStateManager      _stateManager;
        private IPlayerInput          _input;
        private GridOverlay           _overlay;
        private Camera                _camera;
        private BaseUnit              _unit;
        private ActionPointController _ap;

        // ── State ─────────────────────────────────────────────────────────────

        private bool        _isMyTurn;
        private Vector2Int? _highlightedCell;

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
            _overlay      = FindAnyObjectByType<GridOverlay>();
            _camera       = Camera.main;

            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Subscribe<APChangedEvent>(OnAPChanged);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Unsubscribe<APChangedEvent>(OnAPChanged);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void Update()
        {
            if (!_isMyTurn) return;
            if (!(_stateManager?.IsInCombat ?? false)) return;
            if (_camera == null || _gridManager == null) return;

            UpdateTargetHighlight();

            if (_input != null && _input.ConfirmPressed)
                HandleClick();
        }

        // ── Hover Highlight ───────────────────────────────────────────────────

        private void UpdateTargetHighlight()
        {
            var hovered  = GetCellUnderCursor();
            var target   = GetHostileAt(hovered);
            bool isAdj   = target != null && IsAdjacent(target);

            // Clear previous highlight if cursor moved
            if (_highlightedCell.HasValue && _highlightedCell != hovered)
            {
                _overlay?.HideCell(_highlightedCell.Value);
                _highlightedCell = null;
            }

            if (target == null || !isAdj) return;

            // Choose colour by AP affordability
            var color = _ap.HasAP(AttackAPCost) ? _attackableColor : _noAPAttackColor;
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
            var hovered = GetCellUnderCursor();
            var target  = GetHostileAt(hovered);

            if (target == null || !IsAdjacent(target)) return;

            // Silently block if not enough AP (red highlight already communicates this)
            if (!_ap.HasAP(AttackAPCost)) return;

            ExecuteAttack(target);
        }

        // ── Attack Execution ──────────────────────────────────────────────────

        private void ExecuteAttack(BaseUnit target)
        {
            if (!_ap.SpendAP(AttackAPCost)) return; // Double-check AP

            float damage = _unit.Stats.EffectiveAttack;
            target.TakeDamage(damage, DamageType.Physical, _unit);

            Debug.Log($"[BasicAttackController] {_unit.DisplayName} → {target.DisplayName}: " +
                      $"{damage:F0} Physical (1 AP spent).");

            GameEventBus.Publish(new ActionExecutedEvent
            {
                ActorUnitId = _unit.UnitId,
                ActionName  = "BasicAttack",
                APSpent     = AttackAPCost
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the hostile BaseUnit occupying <paramref name="gridPos"/>,
        /// or null if the cell is empty, out of bounds, or not hostile.
        /// </summary>
        private BaseUnit GetHostileAt(Vector2Int? gridPos)
        {
            if (gridPos == null || _gridManager == null) return null;
            var cell     = _gridManager.GetCell(gridPos.Value);
            var occupant = cell?.OccupyingUnit as BaseUnit;
            return (occupant != null && occupant.Faction == UnitFaction.Hostile && occupant.IsAlive)
                ? occupant : null;
        }

        /// <summary>Manhattan distance of exactly 1 (cardinal adjacency only).</summary>
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
            // Refresh highlight colour when AP changes (e.g. after a move)
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
                ClearHighlight();
            }
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
