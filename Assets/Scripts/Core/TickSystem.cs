using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Tick System
    // Fires a world tick every N seconds while in the Overworld state.
    // Paused automatically during Combat, Dialogue, Cutscene, and Paused states.
    //
    // Systems that need per-tick updates implement ITickable and register here,
    // OR subscribe to WorldTickEvent via the GameEventBus (preferred for loose coupling).
    //
    // Attach to the persistent GameBootstrapper GameObject.
    // ==========================================================================

    public class TickSystem : MonoBehaviour
    {
        [Header("Tick Settings")]
        [Tooltip("World tick interval in seconds. Default: 3.")]
        [SerializeField] private float _tickIntervalSeconds = 3f;

        [Header("Pause Conditions")]
        [SerializeField] private bool _pauseDuringDialogue = true;
        [SerializeField] private bool _pauseDuringCutscene = true;

        [Header("Debug")]
        [SerializeField] private bool _logTicks;

        private float _timer;
        private int   _tickCount;
        private GameStateManager _stateManager;
        private readonly List<ITickable> _tickables = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _stateManager = ServiceLocator.Get<GameStateManager>();
        }

        private void Update()
        {
            if (!ShouldTick()) return;

            _timer += Time.deltaTime;
            if (_timer >= _tickIntervalSeconds)
            {
                _timer -= _tickIntervalSeconds;
                FireTick();
            }
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private bool ShouldTick()
        {
            if (_stateManager == null) return false;
            var state = _stateManager.CurrentState;

            if (state == GameState.Combat)   return false;
            if (state == GameState.Paused)   return false;
            if (_pauseDuringDialogue && state == GameState.Dialogue) return false;
            if (_pauseDuringCutscene && state == GameState.Cutscene) return false;

            return true;
        }

        private void FireTick()
        {
            _tickCount++;

            if (_logTicks)
                Debug.Log($"[TickSystem] Tick #{_tickCount} at t={Time.time:F1}s");

            // Broadcast via event bus first (prefer loose coupling)
            GameEventBus.Publish(new WorldTickEvent
            {
                TickNumber = _tickCount,
                GameTime   = Time.time
            });

            // Then notify directly registered ITickable objects
            for (int i = _tickables.Count - 1; i >= 0; i--)
                _tickables[i].OnTick(_tickCount);
        }

        // ── Registration ──────────────────────────────────────────────────────

        public void Register(ITickable tickable)
        {
            if (!_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable) =>
            _tickables.Remove(tickable);

        // ── Inspector Utility ─────────────────────────────────────────────────

        public float TickInterval
        {
            get => _tickIntervalSeconds;
            set => _tickIntervalSeconds = Mathf.Max(0.1f, value);
        }
    }

    // ==========================================================================
    // ITickable Interface
    // ==========================================================================

    /// <summary>
    /// Implement this on any MonoBehaviour that needs to receive world ticks
    /// without subscribing to the event bus. Register via TickSystem.Register().
    /// </summary>
    public interface ITickable
    {
        /// <param name="tickNumber">Monotonically increasing tick counter (starts at 1).</param>
        void OnTick(int tickNumber);
    }
}
