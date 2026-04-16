using UnityEngine;
using PokemonAdventure.Core;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Unit Controller (Abstract)
    // Drives a unit's decisions — from player input OR AI. Separated from
    // BaseUnit so "who controls this unit" is fully decoupled from "what this
    // unit is".
    //
    // Turn lifecycle (driven by CombatTurnFlow via CombatManager):
    //   BeginTurn()  →  OnTurnStarted()  →  [controller acts]
    //   EndTurn()    →  OnTurnEnded()
    //
    // A controller signals intent via GameEventBus:
    //   RequestEndTurn()   → TurnEndedEvent
    //   RequestDelayTurn() → TurnDelayRequestedEvent
    //
    // Concrete subclasses: PlayerUnitController, AIController (in AI namespace)
    // ==========================================================================

    public abstract class UnitController : MonoBehaviour
    {
        protected BaseUnit Unit { get; private set; }

        /// <summary>True while this unit's turn is the active one in CombatTurnFlow.</summary>
        public bool IsMyTurn { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            Unit = GetComponent<BaseUnit>();
        }

        // ── Turn Interface (called by CombatTurnFlow) ─────────────────────────

        public void BeginTurn()
        {
            IsMyTurn = true;
            Unit.OnTurnStart();
            OnTurnStarted();
        }

        public void EndTurn()
        {
            IsMyTurn = false;
            Unit.OnTurnEnd();
            OnTurnEnded();
        }

        // ── Abstract Hooks ────────────────────────────────────────────────────

        /// <summary>Override to enable input, start AI evaluation, update UI.</summary>
        protected abstract void OnTurnStarted();

        /// <summary>Override to disable input, cancel previews, clean up.</summary>
        protected abstract void OnTurnEnded();

        // ── Shared Signals ────────────────────────────────────────────────────

        /// <summary>Signal to CombatTurnFlow: this unit is done with its turn.</summary>
        protected void RequestEndTurn()
        {
            if (!IsMyTurn) return;
            GameEventBus.Publish(new TurnEndedEvent { ActiveUnitId = Unit.UnitId });
        }

        /// <summary>
        /// Signal to CombatTurnFlow: move this unit to the end of the current round.
        /// The unit will still act this round, just last.
        /// Future rounds are unaffected (initiative order recalculated fresh).
        /// </summary>
        protected void RequestDelayTurn()
        {
            if (!IsMyTurn) return;
            GameEventBus.Publish(new TurnDelayRequestedEvent { UnitId = Unit.UnitId });
        }
    }

    // ==========================================================================
    // Player Unit Controller
    // Handles turn-management input for the local player during combat.
    // Movement  → CombatMovementController
    // Attacks   → BasicAttackController
    // This class handles only: Space = End Turn.
    // ==========================================================================

    public class PlayerUnitController : UnitController
    {
        [Header("Player Settings")]
        [Tooltip("Which player slot this controller belongs to (0 = host, 1-3 = clients).")]
        [SerializeField] private int _playerSlotIndex;

        public int PlayerSlotIndex => _playerSlotIndex;

        private IPlayerInput _input;
        private bool         _inputEnabled;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();

            if (_input == null)
                Debug.LogWarning("[PlayerUnitController] IPlayerInput not found. " +
                                 "Add PlayerInputProvider to the scene.");
        }

        private void Update()
        {
            if (!_inputEnabled || _input == null) return;

            if (_input.EndTurnPressed)
                RequestEndTurn();
        }

        // ── Turn Hooks ────────────────────────────────────────────────────────

        protected override void OnTurnStarted()
        {
            _inputEnabled = true;
            Debug.Log($"[PlayerUnitController] Slot {_playerSlotIndex}: {Unit.DisplayName}'s turn. " +
                      $"AP={Unit.RuntimeState.CurrentAP}  (Space = end turn)");
        }

        protected override void OnTurnEnded()
        {
            _inputEnabled = false;
        }
    }
}
