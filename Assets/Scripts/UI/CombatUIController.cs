using UnityEngine;
using UnityEngine.UI;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // ==========================================================================
    // Combat UI Controller
    // Manages the in-combat HUD. Reacts to events, never polls.
    //
    // AP SLOTS:
    //   Up to MaxAPCap (6) slot GameObjects. Active = full, Inactive = empty.
    //   Updates on APChangedEvent so the display is always current.
    //
    // TURN INFO:
    //   Shows the active unit's name and turn number from TurnStartedEvent.
    //
    // VISIBILITY:
    //   Combat HUD shown on Combat state, hidden on Overworld/other states.
    //
    // TRACKED UNIT:
    //   Automatically finds the first PlayerUnit in the scene on Start.
    //   Can be overridden via SetTrackedUnit() for multi-player later.
    // ==========================================================================

    public class CombatUIController : MonoBehaviour
    {
        [Header("Vital Bars")]
        [SerializeField] private Slider _hpBar;
        [SerializeField] private Slider _physArmorBar;
        [SerializeField] private Slider _specArmorBar;

        [Header("AP Slots")]
        [Tooltip("Array of slot GameObjects (up to 6). Active = full, inactive = empty.")]
        [SerializeField] private GameObject[] _apSlots = new GameObject[RuntimeUnitState.MaxAPCap];

        [Header("Turn Info")]
        [SerializeField] private TMPro.TextMeshProUGUI _turnInfoLabel;
        [SerializeField] private TMPro.TextMeshProUGUI _activeUnitLabel;

        [Header("Panels")]
        [SerializeField] private GameObject _combatHUDRoot;
        [SerializeField] private GameObject _overworldHUDRoot;

        // ── Runtime ───────────────────────────────────────────────────────────

        private BaseUnit _trackedUnit;
        private UnitRegistry _registry;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _registry = ServiceLocator.Get<UnitRegistry>();

            // Auto-track the first PlayerUnit in the scene (single-player prototype)
            var player = FindFirstObjectByType<PlayerUnit>();
            if (player != null)
                _trackedUnit = player;

            GameEventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<APChangedEvent>(OnAPChanged);

            SetCombatHUDVisible(false);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<APChangedEvent>(OnAPChanged);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Override the unit whose stats are shown (e.g. after a player selection).</summary>
        public void SetTrackedUnit(BaseUnit unit)
        {
            _trackedUnit = unit;
            RefreshAll();
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            SetCombatHUDVisible(evt.NewState == GameState.Combat);
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            // Update turn label for whoever's turn it is
            if (_turnInfoLabel != null)
                _turnInfoLabel.text = $"Turn {evt.TurnNumber}";

            // Try to show the active unit's display name from the registry
            string activeName = evt.ActiveUnitId;
            var    activeUnit = _registry?.Get(evt.ActiveUnitId);
            if (activeUnit != null)
                activeName = activeUnit.DisplayName;

            if (_activeUnitLabel != null)
                _activeUnitLabel.text = $"{activeName}'s turn";

            // Refresh bars if it's the tracked unit's turn
            if (_trackedUnit != null && evt.ActiveUnitId == _trackedUnit.UnitId)
                RefreshAll();
        }

        private void OnAPChanged(APChangedEvent evt)
        {
            if (_trackedUnit == null || evt.UnitId != _trackedUnit.UnitId) return;
            RefreshAPSlots(_trackedUnit.RuntimeState.CurrentAP);
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_trackedUnit == null || !_trackedUnit.IsAlive) return;

            var state = _trackedUnit.RuntimeState;
            var stats = _trackedUnit.Stats;

            if (_hpBar != null)
                _hpBar.value = stats.MaxHP > 0 ? state.CurrentHP / stats.MaxHP : 0f;

            if (_physArmorBar != null)
                _physArmorBar.value = stats.MaxPhysicalArmor > 0
                    ? state.CurrentPhysicalArmor / stats.MaxPhysicalArmor : 0f;

            if (_specArmorBar != null)
                _specArmorBar.value = stats.MaxSpecialArmor > 0
                    ? state.CurrentSpecialArmor / stats.MaxSpecialArmor : 0f;

            RefreshAPSlots(state.CurrentAP);
        }

        private void RefreshAPSlots(int currentAP)
        {
            for (int i = 0; i < _apSlots.Length; i++)
            {
                if (_apSlots[i] == null) continue;
                _apSlots[i].SetActive(i < currentAP);
            }
        }

        // ── Visibility ────────────────────────────────────────────────────────

        private void SetCombatHUDVisible(bool visible)
        {
            if (_combatHUDRoot   != null) _combatHUDRoot.SetActive(visible);
            if (_overworldHUDRoot != null) _overworldHUDRoot.SetActive(!visible);

            if (visible) RefreshAll();
        }
    }
}
