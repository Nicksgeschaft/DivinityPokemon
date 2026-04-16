using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;

namespace PokemonAdventure.World
{
    // ==========================================================================
    // Overworld Manager
    // Coordinates top-level overworld state: ambient tick reactions, NPC routines,
    // overworld camera control, and transition hooks into/out of combat.
    //
    // Currently a thin scaffolding. Systems should be added here as the overworld
    // expands (NPC scheduling, weather, day/night cycle, quest tracking hooks).
    // ==========================================================================

    public class OverworldManager : MonoBehaviour, ITickable
    {
        [Header("Debug")]
        [SerializeField] private bool _logTicks;

        private GameStateManager _stateManager;
        private TickSystem       _tickSystem;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _stateManager = ServiceLocator.Get<GameStateManager>();
            _tickSystem   = ServiceLocator.Get<TickSystem>();

            _tickSystem?.Register(this);

            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy()
        {
            _tickSystem?.Unregister(this);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        // ── Tick ──────────────────────────────────────────────────────────────

        public void OnTick(int tickNumber)
        {
            if (_logTicks) Debug.Log($"[OverworldManager] Tick #{tickNumber}");

            // TODO: Tick NPC schedules (move to next waypoint, change dialogue state, etc.)
            // TODO: Tick world events (weather change, shop restock, etc.)
            // TODO: Tick passive regeneration for out-of-combat units
        }

        // ── State Reactions ───────────────────────────────────────────────────

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState == GameState.Overworld && evt.PreviousState == GameState.Combat)
                OnReturnedFromCombat();
        }

        private void OnReturnedFromCombat()
        {
            // TODO: Resume NPC routines paused during combat
            // TODO: Restore overworld camera position
            // TODO: Update quest state based on combat outcome
            Debug.Log("[OverworldManager] Returned from combat. Resuming overworld.");
        }
    }
}
