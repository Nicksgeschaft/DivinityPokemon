using UnityEngine;
using PokemonAdventure.Core;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Action Point Controller
    // Single source of truth for a unit's current AP in combat.
    //
    // Every AP change routes through SpendAP() / GainAP() so APChangedEvent
    // is always fired, keeping CombatMovementController, BasicAttackController,
    // and CombatUIController in sync without polling.
    //
    // TURN-START FLOW:
    //   TurnManager.BeginTurn() calls UnitController.BeginTurn()
    //     → BaseUnit.OnTurnStart() → RuntimeState.GainTurnAP()   (AP updated)
    //     → TurnStartedEvent fires
    //     → ActionPointController.OnTurnStarted fires APChangedEvent  (UI refresh)
    //
    // Attach to every unit that participates in combat.
    // ==========================================================================

    [RequireComponent(typeof(BaseUnit))]
    public class ActionPointController : MonoBehaviour
    {
        private BaseUnit _unit;

        public int CurrentAP => _unit != null ? _unit.RuntimeState.CurrentAP : 0;
        public int MaxAP     => RuntimeUnitState.MaxAPCap;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _unit = GetComponent<BaseUnit>();
        }

        private void Start()
        {
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns true if the unit currently has at least <paramref name="cost"/> AP.</summary>
        public bool HasAP(int cost) =>
            _unit != null && _unit.RuntimeState.CanAfford(cost);

        /// <summary>
        /// Attempt to spend <paramref name="cost"/> AP.
        /// Returns false without side-effects if the unit cannot afford it.
        /// Fires APChangedEvent on success.
        /// </summary>
        public bool SpendAP(int cost)
        {
            if (_unit == null) return false;
            if (!_unit.RuntimeState.TrySpendAP(cost)) return false;

            Broadcast(-cost);
            return true;
        }

        /// <summary>
        /// Add <paramref name="amount"/> AP (e.g. from an item or effect).
        /// Capped at MaxAPCap. Fires APChangedEvent.
        /// </summary>
        public void GainAP(int amount)
        {
            if (_unit == null || amount <= 0) return;
            _unit.RuntimeState.CurrentAP = Mathf.Min(
                _unit.RuntimeState.CurrentAP + amount,
                RuntimeUnitState.MaxAPCap);
            Broadcast(amount);
        }

        // ── Turn Start ────────────────────────────────────────────────────────

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (_unit == null || evt.ActiveUnitId != _unit.UnitId) return;

            // AP was already gained by UnitController.BeginTurn() before this event fired.
            // Just broadcast the updated value so the UI refreshes.
            Broadcast(0);
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private void Broadcast(int delta)
        {
            GameEventBus.Publish(new APChangedEvent
            {
                UnitId = _unit.UnitId,
                NewAP  = _unit.RuntimeState.CurrentAP,
                Delta  = delta
            });
        }
    }
}
