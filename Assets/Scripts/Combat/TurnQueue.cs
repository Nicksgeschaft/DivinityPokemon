using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Turn Queue
    // Priority queue sorted by Initiative (descending). Ties broken by unit ID
    // (deterministic across host and clients).
    //
    // Supports mid-combat unit insertion (units joining an active encounter).
    // A round ends when the queue is exhausted; it is then rebuilt from the
    // full participant list for the next round.
    // ==========================================================================

    public class TurnQueue
    {
        // Sorted by effective initiative, highest first
        private readonly List<BaseUnit> _queue       = new();
        private readonly List<BaseUnit> _participants = new();

        public int Count => _queue.Count;
        public IReadOnlyList<BaseUnit> Participants => _participants;

        // ── Building ──────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the queue from a list of participants, sorted by Initiative.
        /// Call at the start of each combat round.
        /// </summary>
        public void Build(IEnumerable<BaseUnit> participants)
        {
            _participants.Clear();
            _participants.AddRange(participants.Where(u => u != null && u.IsAlive));

            RebuildQueue();
        }

        /// <summary>Rebuild the queue from the current participant list (new round).</summary>
        public void RebuildForNextRound()
        {
            _participants.RemoveAll(u => u == null || !u.IsAlive);
            RebuildQueue();
        }

        private void RebuildQueue()
        {
            _queue.Clear();
            _queue.AddRange(
                _participants
                    .OrderByDescending(u => u.Stats.EffectiveInitiative +
                                           u.RuntimeState.InitiativeModifier)
                    .ThenBy(u => u.UnitId) // Deterministic tiebreak
            );
        }

        // ── Navigation ────────────────────────────────────────────────────────

        /// <summary>
        /// Removes and returns the next unit in the queue.
        /// Returns null when the queue is empty (round over).
        /// </summary>
        public BaseUnit Dequeue()
        {
            if (_queue.Count == 0) return null;
            var unit = _queue[0];
            _queue.RemoveAt(0);
            return unit;
        }

        /// <summary>Peek at the next unit without removing it.</summary>
        public BaseUnit Peek() => _queue.Count > 0 ? _queue[0] : null;

        public bool IsEmpty => _queue.Count == 0;

        // ── Mid-Combat Insertion ──────────────────────────────────────────────

        /// <summary>
        /// Adds a new unit mid-combat. The unit is inserted at the correct
        /// initiative position in the CURRENT round's remaining queue.
        /// It is also added to the participant list for future rounds.
        /// </summary>
        public void AddUnit(BaseUnit unit)
        {
            if (unit == null || _participants.Contains(unit)) return;

            _participants.Add(unit);

            // Insert into the live queue at the correct initiative position
            float initiative = unit.Stats.EffectiveInitiative + unit.RuntimeState.InitiativeModifier;
            int insertIndex  = 0;
            for (int i = 0; i < _queue.Count; i++)
            {
                float qInit = _queue[i].Stats.EffectiveInitiative +
                              _queue[i].RuntimeState.InitiativeModifier;
                if (initiative <= qInit)
                    insertIndex = i + 1;
            }
            _queue.Insert(insertIndex, unit);
            Debug.Log($"[TurnQueue] {unit.DisplayName} joined combat mid-round at queue pos {insertIndex}.");
        }

        /// <summary>
        /// Adds a unit that is joining combat mid-round by entering the combat zone.
        /// Placed at the END of the current round — acts after everyone already queued.
        /// Added to the participant list so future rounds include them at correct initiative.
        /// </summary>
        public void AddUnitAtEndOfRound(BaseUnit unit)
        {
            if (unit == null || _participants.Contains(unit)) return;

            _participants.Add(unit);
            _queue.Add(unit);
            Debug.Log($"[TurnQueue] {unit.DisplayName} joined combat — placed last in current round.");
        }

        // ── Delay Turn ────────────────────────────────────────────────────────

        /// <summary>
        /// Moves a unit to the END of the current round's queue.
        /// The unit's position in future rounds is unaffected (re-sorted by initiative).
        /// Call AFTER the unit has been dequeued from the front (i.e. it's the active unit).
        /// Returns the new queue index (always Count after insertion).
        /// </summary>
        public int DelayUnit(BaseUnit unit)
        {
            // If somehow still in the queue, remove first
            _queue.Remove(unit);

            // Append to end of current round
            _queue.Add(unit);

            return _queue.Count - 1;
        }

        /// <summary>Remove a unit from both the live queue and participant list.</summary>
        public void RemoveUnit(BaseUnit unit)
        {
            _queue.Remove(unit);
            _participants.Remove(unit);
        }

        // ── Debug ─────────────────────────────────────────────────────────────

        public void LogQueue()
        {
            var order = string.Join(" → ", _queue.Select(u =>
                $"{u.DisplayName}({u.Stats.EffectiveInitiative:F1})"));
            Debug.Log($"[TurnQueue] Queue: {order}");
        }
    }
}
