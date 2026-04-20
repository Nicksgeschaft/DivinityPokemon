using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Turn Manager
    // Replaces CombatTurnFlow. Pure C# class — no MonoBehaviour.
    // Owns and drives the turn loop for one CombatEncounter.
    //
    // TURN ORDER:
    //   Initiative is sorted once at the start of each round and is NOT
    //   recalculated mid-round. Changes happen only via:
    //     a) DelayCurrentTurn()  — moves active unit to end of current round
    //     b) AddUnit()           — inserts at correct initiative position
    //
    // DRIVING:
    //   CombatStateController calls Begin() once, then routes TurnEndedEvent →
    //   ForceAdvance(). TurnManager publishes all turn/round/phase events.
    //
    // AP:
    //   AP gain happens inside BaseUnit.OnTurnStart() (called by
    //   UnitController.BeginTurn). ActionPointController listens to
    //   TurnStartedEvent and broadcasts APChangedEvent so the UI updates.
    // ==========================================================================

    public sealed class TurnManager
    {
        // ── Public State ──────────────────────────────────────────────────────

        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Idle;
        public BaseUnit  ActiveUnit   { get; private set; }
        public int       RoundNumber  { get; private set; }
        public int       TurnNumber   { get; private set; }
        public bool      IsRunning    { get; private set; }

        // ── Internal ──────────────────────────────────────────────────────────

        private CombatEncounter _encounter;
        private TurnQueue        _queue;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>
        /// Start the turn loop for the given encounter.
        /// Call once after CombatEncounter is fully initialised.
        /// </summary>
        public void Begin(CombatEncounter encounter)
        {
            _encounter  = encounter;
            _queue      = new TurnQueue();
            IsRunning   = true;
            RoundNumber = 0;
            TurnNumber  = 0;
            StartNewRound();
        }

        public void Stop()
        {
            IsRunning  = false;
            ActiveUnit = null;
            SetPhase(TurnPhase.Idle);
        }

        // ── Public Commands ───────────────────────────────────────────────────

        /// <summary>
        /// Called by CombatStateController when a TurnEndedEvent is received.
        /// Moves to the next unit in the queue.
        /// </summary>
        public void ForceAdvance()
        {
            if (!IsRunning) return;
            EndCurrentTurn();
            AdvanceToNextTurn();
        }

        /// <summary>
        /// Move the active unit to the end of the current round's queue.
        /// It will still act this round, just last. Future rounds are unaffected.
        /// </summary>
        public void DelayCurrentTurn()
        {
            if (!IsRunning || ActiveUnit == null) return;

            int newPos = _queue.DelayUnit(ActiveUnit);

            GameEventBus.Publish(new TurnDelayedEvent
            {
                UnitId           = ActiveUnit.UnitId,
                NewQueuePosition = newPos
            });

            Debug.Log($"[TurnManager] {ActiveUnit.DisplayName} delayed → queue pos {newPos}.");

            // No AP/cleanup yet — unit acts again this round
            ActiveUnit = null;
            AdvanceToNextTurn();
        }

        /// <summary>Add a unit mid-combat (inserted at correct initiative position).</summary>
        public void AddUnit(BaseUnit unit)
        {
            _encounter?.AddUnit(unit);
            _queue.AddUnit(unit);
        }

        /// <summary>
        /// Add a unit that walked into the combat zone mid-round.
        /// Placed at the END of the current round; sorted normally from next round on.
        /// </summary>
        public void AddUnitAtEndOfRound(BaseUnit unit)
        {
            _encounter?.AddUnit(unit);
            _queue.AddUnitAtEndOfRound(unit);
        }

        /// <summary>Remove a unit (death, escape). Advances the turn if it was active.</summary>
        public void RemoveUnit(BaseUnit unit)
        {
            bool wasActive = ActiveUnit == unit;
            _queue.RemoveUnit(unit);

            if (wasActive)
            {
                ActiveUnit = null;
                AdvanceToNextTurn(); // checks end condition internally
            }
            else
            {
                // Dead unit was not active — check if all enemies are gone right now
                if (_encounter != null && _encounter.CheckEndCondition(out bool playerWon))
                {
                    SetPhase(TurnPhase.EncounterEnd);
                    Debug.Log($"[TurnManager] Encounter over (non-active unit died). PlayerVictory={playerWon}");
                    GameEventBus.Publish(new CombatEndedEvent
                    {
                        EncounterId   = _encounter.EncounterId,
                        PlayerVictory = playerWon
                    });
                    Stop();
                }
            }
        }

        // ── Internal Flow ─────────────────────────────────────────────────────

        private void StartNewRound()
        {
            RoundNumber++;
            SetPhase(TurnPhase.RoundSetup);

            _queue.Build(_encounter.Participants);

            Debug.Log($"[TurnManager] Round {RoundNumber} — " +
                      $"{_encounter.Participants.Count} participants.");
            _queue.LogQueue();

            GameEventBus.Publish(new RoundStartedEvent { RoundNumber = RoundNumber });
            AdvanceToNextTurn();
        }

        private void AdvanceToNextTurn()
        {
            // End-condition check before dequeuing the next unit
            if (_encounter.CheckEndCondition(out bool playerWon))
            {
                SetPhase(TurnPhase.EncounterEnd);
                Debug.Log($"[TurnManager] Encounter over. PlayerVictory={playerWon}");
                GameEventBus.Publish(new CombatEndedEvent
                {
                    EncounterId   = _encounter.EncounterId,
                    PlayerVictory = playerWon
                });
                Stop();
                return;
            }

            // Queue exhausted → start a new round
            if (_queue.IsEmpty)
            {
                SetPhase(TurnPhase.RoundEnd);
                StartNewRound();
                return;
            }

            // Skip dead units that haven't been removed yet
            BaseUnit next;
            do { next = _queue.Dequeue(); }
            while (next != null && !next.IsAlive && !_queue.IsEmpty);

            if (next == null || !next.IsAlive)
            {
                // All remaining units are dead — recursion resolves end condition
                AdvanceToNextTurn();
                return;
            }

            BeginTurn(next);
        }

        private void BeginTurn(BaseUnit unit)
        {
            ActiveUnit = unit;
            TurnNumber++;

            SetPhase(TurnPhase.TurnStart);

            // AP gain + status ticks happen inside UnitController.BeginTurn()
            // → BaseUnit.OnTurnStart(). ActionPointController then broadcasts
            // APChangedEvent when TurnStartedEvent fires (see below).
            var controller = unit.GetComponent<UnitController>();
            controller?.BeginTurn();

            SetPhase(TurnPhase.WaitingForAction);

            // NOTE: TurnStartedEvent fires AFTER AP has already been gained.
            // ActionPointController.OnTurnStarted broadcasts APChangedEvent so the UI refreshes.
            GameEventBus.Publish(new TurnStartedEvent
            {
                ActiveUnitId  = unit.UnitId,
                TurnNumber    = TurnNumber,
                ActiveFaction = unit.Faction
            });
        }

        private void EndCurrentTurn()
        {
            if (ActiveUnit == null) return;
            SetPhase(TurnPhase.TurnEnd);

            var controller = ActiveUnit.GetComponent<UnitController>();
            controller?.EndTurn();

            // IMPORTANT: Do NOT republish TurnEndedEvent here.
            // TurnEndedEvent is the *request* published by UnitController.RequestEndTurn().
            // CombatStateController listens to that and calls ForceAdvance() exactly once.
            // Republishing here would cause a second ForceAdvance(), silently skipping a turn.
            ActiveUnit = null;
        }

        private void SetPhase(TurnPhase phase)
        {
            CurrentPhase = phase;
            GameEventBus.Publish(new TurnPhaseChangedEvent
            {
                ActiveUnitId = ActiveUnit?.UnitId ?? string.Empty,
                NewPhase     = phase
            });
        }
    }
}
