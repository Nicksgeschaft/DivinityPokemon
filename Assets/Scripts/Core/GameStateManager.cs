using UnityEngine;
using PokemonAdventure.Data;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Game State Manager
    // Single source of truth for the top-level game state.
    // All state changes go through TransitionTo() to guarantee event broadcast.
    // Register with ServiceLocator from GameBootstrapper.
    // ==========================================================================

    public class GameStateManager
    {
        public GameState CurrentState { get; private set; } = GameState.Booting;

        // ── State Transitions ─────────────────────────────────────────────────

        public void TransitionTo(GameState newState)
        {
            if (newState == CurrentState)
            {
                Debug.LogWarning($"[GameStateManager] Ignored redundant transition to {newState}.");
                return;
            }

            if (!IsValidTransition(CurrentState, newState))
            {
                Debug.LogWarning($"[GameStateManager] Invalid transition: {CurrentState} → {newState}. " +
                                 "Add a rule to IsValidTransition if this should be allowed.");
                return;
            }

            var previous = CurrentState;
            CurrentState = newState;
            Debug.Log($"[GameStateManager] State: {previous} → {newState}");

            GameEventBus.Publish(new GameStateChangedEvent
            {
                PreviousState = previous,
                NewState      = newState
            });
        }

        // ── Convenience Queries ───────────────────────────────────────────────

        public bool IsInCombat    => CurrentState == GameState.Combat;
        public bool IsInOverworld => CurrentState == GameState.Overworld;
        public bool IsLoading     => CurrentState == GameState.Loading;
        public bool IsPaused      => CurrentState == GameState.Paused;

        public bool CanReceiveInput =>
            CurrentState == GameState.Overworld ||
            CurrentState == GameState.Combat;

        // ── Transition Validity ───────────────────────────────────────────────

        /// <summary>
        /// Whitelist of legal state transitions. Prevents accidental jumps that
        /// would bypass teardown/setup logic in other systems.
        /// </summary>
        private static bool IsValidTransition(GameState from, GameState to) => (from, to) switch
        {
            (GameState.Booting,    GameState.MainMenu)  => true,
            (GameState.Booting,    GameState.Overworld) => true, // Direct boot into game (dev shortcut)
            (GameState.MainMenu,   GameState.Loading)   => true,
            (GameState.Loading,    GameState.Overworld) => true,
            (GameState.Overworld,  GameState.Combat)    => true,
            (GameState.Overworld,  GameState.Dialogue)  => true,
            (GameState.Overworld,  GameState.Cutscene)  => true,
            (GameState.Overworld,  GameState.Paused)    => true,
            (GameState.Overworld,  GameState.MainMenu)  => true,
            (GameState.Combat,     GameState.Overworld) => true,
            (GameState.Combat,     GameState.Cutscene)  => true,
            (GameState.Combat,     GameState.Paused)    => true,
            (GameState.Dialogue,   GameState.Overworld) => true,
            (GameState.Cutscene,   GameState.Overworld) => true,
            (GameState.Cutscene,   GameState.Combat)    => true,
            (GameState.Paused,     GameState.Overworld) => true,
            (GameState.Paused,     GameState.Combat)    => true,
            _ => false
        };
    }
}
