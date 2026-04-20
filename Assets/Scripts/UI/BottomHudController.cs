using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // Wires the BottomHudRoot to the currently controlled PlayerUnit.
    // Delegates display work to specialised sub-UI components.
    //
    // Combat-only elements (AP bar, End Turn, armor bars) are shown only when
    // the tracked unit is an active participant in the current encounter.
    // Switching to a character outside the combat zone hides them.
    public class BottomHudController : MonoBehaviour
    {
        [Header("Sub-UI Components")]
        [SerializeField] private PlayerPortraitUI   _portraitUI;
        [SerializeField] private PlayerStatusBarsUI _statusBarsUI;
        [SerializeField] private SkillBarUI         _skillBarUI;
        [SerializeField] private ActionPointBarUI   _apBarUI;
        [SerializeField] private QuickMenuUI        _quickMenuUI;
        [SerializeField] private PassTurnButtonUI   _passTurnUI;

        private BaseUnit           _trackedUnit;
        private bool               _inCombat;
        private HashSet<string>    _combatParticipants = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            SetCombatOnlyRootsVisible(false);
            _statusBarsUI?.SetCombatMode(false);
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            GameEventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Subscribe<UnitRegisteredEvent>(OnUnitRegistered);
            GameEventBus.Subscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);
            GameEventBus.Subscribe<UnitEnteredCombatEvent>(OnUnitEnteredCombat);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            GameEventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Unsubscribe<UnitRegisteredEvent>(OnUnitRegistered);
            GameEventBus.Unsubscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);
            GameEventBus.Unsubscribe<UnitEnteredCombatEvent>(OnUnitEnteredCombat);
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
            RefreshCombatUI();
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

            if (!_inCombat)
                _combatParticipants.Clear();

            if (_inCombat && _trackedUnit == null)
            {
                var player = FindAnyObjectByType<PlayerUnit>();
                if (player != null) SetTrackedUnit(player);
            }

            RefreshCombatUI();
        }

        private void OnUnitEnteredCombat(UnitEnteredCombatEvent evt)
        {
            _combatParticipants.Add(evt.UnitId);
            // Re-evaluate visibility in case the tracked unit just joined combat
            if (_trackedUnit != null && evt.UnitId == _trackedUnit.UnitId)
                RefreshCombatUI();
        }

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_trackedUnit == null) return;
            if (evt.DefenderUnitId == _trackedUnit.UnitId)
                _statusBarsUI?.Refresh();
        }

        private void OnUnitRegistered(UnitRegisteredEvent evt)
        {
            if (_trackedUnit != null) return;
            if (evt.Faction != UnitFaction.Friendly) return;

            var registry = ServiceLocator.Get<UnitRegistry>();
            var unit = registry?.Get(evt.UnitId);
            if (unit != null) SetTrackedUnit(unit);
        }

        private void OnActiveUnitChanged(ActiveUnitChangedEvent evt)
        {
            var registry = ServiceLocator.Get<UnitRegistry>();
            var unit = registry?.Get(evt.UnitId);
            if (unit != null) SetTrackedUnit(unit);
        }

        // ── Combat UI Visibility ──────────────────────────────────────────────

        // Combat elements are shown only when the tracked unit is actually
        // participating in the current encounter, not just because a fight is active.
        private void RefreshCombatUI()
        {
            bool showCombatUI = _inCombat && IsTrackedUnitParticipant();
            SetCombatOnlyRootsVisible(showCombatUI);
            _statusBarsUI?.SetCombatMode(showCombatUI);
        }

        private bool IsTrackedUnitParticipant() =>
            _trackedUnit != null && _combatParticipants.Contains(_trackedUnit.UnitId);

        private void SetCombatOnlyRootsVisible(bool visible)
        {
            if (_apBarUI    != null) _apBarUI.gameObject.SetActive(visible);
            if (_passTurnUI != null) _passTurnUI.gameObject.SetActive(visible);
        }
    }
}
