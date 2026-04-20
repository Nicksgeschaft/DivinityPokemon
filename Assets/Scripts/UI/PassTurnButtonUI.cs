using UnityEngine;
using UnityEngine.UI;
using PokemonAdventure.Core;
using PokemonAdventure.Data;

namespace PokemonAdventure.UI
{
    // Connects the PassTurnButton to the turn system.
    // Publishes TurnEndedEvent for the active friendly unit, mirroring Space-bar behaviour.
    // Button is interactable only during the local player's turn.
    public class PassTurnButtonUI : MonoBehaviour
    {
        [SerializeField] private Button _passTurnButton;

        private string _activePlayerUnitId;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_passTurnButton == null)
                _passTurnButton = GetComponentInChildren<Button>();

            _passTurnButton?.onClick.AddListener(RequestEndTurn);
            SetInteractable(false);
        }

        private void Start()
        {
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Subscribe<CombatStartedEvent>(OnCombatStarted);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Unsubscribe<CombatStartedEvent>(OnCombatStarted);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void RequestEndTurn()
        {
            if (string.IsNullOrEmpty(_activePlayerUnitId))
            {
                Debug.Log("[PassTurnButtonUI] No active player turn to end.");
                return;
            }

            Debug.Log($"[PassTurnButtonUI] Ending turn for unit {_activePlayerUnitId}");
            GameEventBus.Publish(new TurnEndedEvent { ActiveUnitId = _activePlayerUnitId });
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            bool isPlayerTurn = evt.ActiveFaction == UnitFaction.Friendly;
            _activePlayerUnitId = isPlayerTurn ? evt.ActiveUnitId : null;
            SetInteractable(isPlayerTurn);
        }

        private void OnTurnEnded(TurnEndedEvent evt)
        {
            _activePlayerUnitId = null;
            SetInteractable(false);
        }

        private void OnCombatStarted(CombatStartedEvent evt)
        {
            // Reset state at combat start — turn events will enable the button when needed
            _activePlayerUnitId = null;
            SetInteractable(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetInteractable(bool value)
        {
            if (_passTurnButton != null)
                _passTurnButton.interactable = value;
        }
    }
}
