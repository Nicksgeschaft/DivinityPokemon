using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Combat Encounter
    // Data container for one discrete combat event. Tracks all participating
    // units, the initiating trigger, and the encounter outcome.
    //
    // The CombatManager creates and owns this object for each fight.
    // ==========================================================================

    public class CombatEncounter
    {
        // ── Identity ──────────────────────────────────────────────────────────

        public string EncounterId { get; }
        public float  StartTime   { get; }

        // ── Participants ──────────────────────────────────────────────────────

        private readonly List<BaseUnit> _participants = new();
        public IReadOnlyList<BaseUnit> Participants => _participants;

        // ── Outcome ───────────────────────────────────────────────────────────

        public bool IsActive       { get; private set; } = true;
        public bool PlayerVictory  { get; private set; }
        public float EndTime       { get; private set; }

        // ── Construction ──────────────────────────────────────────────────────

        public CombatEncounter(IEnumerable<BaseUnit> initialParticipants)
        {
            EncounterId = Guid.NewGuid().ToString("N")[..8];
            StartTime   = Time.time;

            foreach (var unit in initialParticipants)
                AddUnit(unit);

            Debug.Log($"[CombatEncounter] {EncounterId} started with {_participants.Count} units.");
        }

        // ── Participant Management ────────────────────────────────────────────

        /// <summary>Add a unit to the encounter (also works mid-combat).</summary>
        public void AddUnit(BaseUnit unit)
        {
            if (unit == null || _participants.Contains(unit)) return;
            _participants.Add(unit);
            unit.RuntimeState.IsInCombat = true;
            Debug.Log($"[CombatEncounter] {unit.DisplayName} joined encounter {EncounterId}.");
        }

        /// <summary>Remove a unit (death, flee, etc.). Does not end combat automatically.</summary>
        public void RemoveUnit(BaseUnit unit)
        {
            if (_participants.Remove(unit))
                unit.RuntimeState.IsInCombat = false;
        }

        // ── Faction Queries ───────────────────────────────────────────────────

        public IEnumerable<BaseUnit> GetUnitsOfFaction(UnitFaction faction) =>
            _participants.Where(u => u.IsAlive && u.Faction == faction);

        public bool HasLivingFaction(UnitFaction faction) =>
            _participants.Any(u => u.IsAlive && u.Faction == faction);

        // ── Victory Condition Check ───────────────────────────────────────────

        /// <summary>
        /// Returns true if a decisive condition has been met.
        /// Combat ends when no Hostile units remain alive, or no Friendly units remain.
        /// </summary>
        public bool CheckEndCondition(out bool playerWon)
        {
            bool hostilesAlive  = HasLivingFaction(UnitFaction.Hostile);
            bool friendliesAlive = HasLivingFaction(UnitFaction.Friendly);

            if (!hostilesAlive || !friendliesAlive)
            {
                playerWon = !hostilesAlive;
                return true;
            }
            playerWon = false;
            return false;
        }

        // ── Closing ───────────────────────────────────────────────────────────

        public void Close(bool playerVictory)
        {
            IsActive      = false;
            PlayerVictory = playerVictory;
            EndTime       = Time.time;

            foreach (var unit in _participants)
                unit.RuntimeState.IsInCombat = false;

            float duration = EndTime - StartTime;
            Debug.Log($"[CombatEncounter] {EncounterId} ended. " +
                      $"Victory={playerVictory}. Duration={duration:F1}s.");
        }
    }
}
