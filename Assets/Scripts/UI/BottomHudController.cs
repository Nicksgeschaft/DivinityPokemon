using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // Wires the BottomHudRoot to the currently controlled PlayerUnit.
    // Delegates display work to specialised sub-UI components.
    public class BottomHudController : MonoBehaviour
    {
        [Header("Sub-UI Components")]
        [SerializeField] private PlayerPortraitUI   _portraitUI;
        [SerializeField] private PlayerStatusBarsUI _statusBarsUI;
        [SerializeField] private SkillBarUI         _skillBarUI;
        [SerializeField] private ActionPointBarUI   _apBarUI;
        [SerializeField] private QuickMenuUI        _quickMenuUI;
        [SerializeField] private PassTurnButtonUI   _passTurnUI;

        private BaseUnit _trackedUnit;
        private bool     _inCombat;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Hide combat-only elements immediately — before any Start() or event fires.
            SetCombatOnlyRootsVisible(false);
            _statusBarsUI?.SetCombatMode(false);
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            GameEventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            GameEventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
        }

        private void Start()
        {
            var player = FindAnyObjectByType<PlayerUnit>();
            if (player != null) SetTrackedUnit(player);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetTrackedUnit(BaseUnit unit)
        {
            _trackedUnit = unit;
            _portraitUI?.SetUnit(unit);
            _statusBarsUI?.SetUnit(unit);
            _skillBarUI?.SetUnit(unit);
            _apBarUI?.SetUnit(unit);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (evt.ActiveFaction != UnitFaction.Friendly) return;

            var registry = ServiceLocator.Get<UnitRegistry>();
            if (registry == null) return;

            var unit = registry.Get(evt.ActiveUnitId);
            if (unit != null) SetTrackedUnit(unit);
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            _inCombat = evt.NewState == GameState.Combat;
            _statusBarsUI?.SetCombatMode(_inCombat);

            // Ensure tracked unit is wired BEFORE making the combat UI visible,
            // so the AP bar shows the correct state in OnEnable rather than 0.
            if (_inCombat && _trackedUnit == null)
            {
                var player = FindAnyObjectByType<PlayerUnit>();
                if (player != null) SetTrackedUnit(player);
            }

            SetCombatOnlyRootsVisible(_inCombat);
        }

        private void SetCombatOnlyRootsVisible(bool visible)
        {
            if (_apBarUI   != null) _apBarUI.gameObject.SetActive(visible);
            if (_passTurnUI != null) _passTurnUI.gameObject.SetActive(visible);
        }

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_trackedUnit == null) return;
            if (evt.DefenderUnitId == _trackedUnit.UnitId)
                _statusBarsUI?.Refresh();
        }
    }
}
