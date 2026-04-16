using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Turn State
    // Read-only snapshot of the current turn within a combat encounter.
    // The CombatManager owns this object and updates it between turns.
    // UI and other systems read from it without modifying it.
    // ==========================================================================

    public class TurnState
    {
        // ── Current Turn ──────────────────────────────────────────────────────

        /// <summary>The unit currently acting. Null between turns or during resolution.</summary>
        public BaseUnit ActiveUnit { get; private set; }

        /// <summary>Current phase of the combat loop.</summary>
        public CombatStateType Phase { get; private set; } = CombatStateType.Inactive;

        /// <summary>Monotonically increasing round counter (increments when full queue is exhausted).</summary>
        public int RoundNumber { get; private set; }

        /// <summary>Total turns elapsed since combat began.</summary>
        public int TurnNumber { get; private set; }

        // ── Internal Setters (only CombatManager should call these) ──────────

        internal void SetPhase(CombatStateType phase) => Phase = phase;

        internal void SetActiveUnit(BaseUnit unit)
        {
            ActiveUnit = unit;
            TurnNumber++;
        }

        internal void IncrementRound() => RoundNumber++;

        internal void Reset()
        {
            ActiveUnit  = null;
            Phase       = CombatStateType.Inactive;
            RoundNumber = 0;
            TurnNumber  = 0;
        }

        // ── Queries ───────────────────────────────────────────────────────────

        public bool IsPlayerActing =>
            Phase == CombatStateType.PlayerTurn ||
            Phase == CombatStateType.RemotePlayerTurn;

        public bool IsAIActing => Phase == CombatStateType.AITurn;

        public bool IsResolvingAction => Phase == CombatStateType.ResolvingAction;

        public override string ToString() =>
            $"TurnState [Phase={Phase} Round={RoundNumber} Turn={TurnNumber} " +
            $"Active={ActiveUnit?.DisplayName ?? "none"}]";
    }
}
