namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Turn Phase
    // Granular phases within a single unit's turn. Kept as a separate file so
    // GameEventBus.cs can reference it without a circular dependency.
    // ==========================================================================

    public enum TurnPhase
    {
        /// <summary>No active turn. Between rounds or before combat.</summary>
        Idle,

        /// <summary>This round's queue is being built/sorted.</summary>
        RoundSetup,

        /// <summary>A unit's turn has just started; status effects have ticked.</summary>
        TurnStart,

        /// <summary>The active unit is making decisions (player input or AI evaluation).</summary>
        WaitingForAction,

        /// <summary>An action is being executed (animation, resolution in progress).</summary>
        ResolvingAction,

        /// <summary>End-of-turn cleanup (cooldowns, lingering effects).</summary>
        TurnEnd,

        /// <summary>All units in the queue have acted; about to start a new round.</summary>
        RoundEnd,

        /// <summary>Encounter over — victory or defeat being resolved.</summary>
        EncounterEnd
    }
}
