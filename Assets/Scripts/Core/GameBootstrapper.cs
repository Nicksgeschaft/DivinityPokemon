using UnityEngine;
using PokemonAdventure.ScriptableObjects;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Game Bootstrapper
    // Scene entry point. Responsible for:
    //   1. Ensuring only one bootstrapper exists (DontDestroyOnLoad guard).
    //   2. Instantiating and registering all core services.
    //   3. Transitioning to the initial game state.
    //
    // Hierarchy suggestion:
    //   [GameBootstrapper]
    //     ├── [TickSystem]
    //     ├── [WorldGridManager]
    //     └── [CombatManager]
    //
    // Child components are discovered via GetComponentInChildren so the
    // hierarchy is flexible — no hardcoded Find() calls.
    // ==========================================================================

    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Bootstrap Settings")]
        [Tooltip("Keep this object alive across scene loads (main game scenes).")]
        [SerializeField] private bool _persistAcrossScenes = true;

        [Header("Initial State")]
        [SerializeField] private Data.GameState _initialState = Data.GameState.Overworld;

        [Header("Data Assets")]
        [Tooltip("Assign the project-wide SkillRegistry asset here so skills can be " +
                 "resolved at runtime by ID.")]
        [SerializeField] private SkillRegistry _skillRegistry;

        [Header("Debug")]
        [SerializeField] private bool _verboseLogging = true;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_persistAcrossScenes)
            {
                // Guard against duplicate bootstrappers after scene reload
                var existing = FindObjectsByType<GameBootstrapper>(FindObjectsSortMode.None);
                if (existing.Length > 1)
                {
                    Destroy(gameObject);
                    return;
                }
                DontDestroyOnLoad(gameObject);
            }

            Boot();
        }

        private void OnDestroy()
        {
            GameEventBus.Clear();
            ServiceLocator.Clear();
        }

        // ── Boot Sequence ─────────────────────────────────────────────────────

        private void Boot()
        {
            Log("=== GameBootstrapper: Boot Start ===");

            RegisterPureCSharpServices();
            RegisterMonoBehaviourServices();

            // Kick off the first state transition
            ServiceLocator.Get<GameStateManager>()?.TransitionTo(_initialState);

            Log("=== GameBootstrapper: Boot Complete ===");
        }

        private void RegisterPureCSharpServices()
        {
            // GameStateManager is a plain C# object — no MonoBehaviour needed
            ServiceLocator.Register(new GameStateManager());
            Log("GameStateManager registered.");

            // UnitRegistry — scene-wide UnitId → BaseUnit lookup (pure C#)
            ServiceLocator.Register(new UnitRegistry());
            Log("UnitRegistry registered.");
        }

        private void RegisterMonoBehaviourServices()
        {
            // Input provider — registered as IPlayerInput so callers are decoupled
            // from the concrete implementation (swap for tests or rebinding UI).
            var input = GetOrCreateChild<PlayerInputProvider>("PlayerInputProvider");
            ServiceLocator.Register<IPlayerInput>(input);
            Log("PlayerInputProvider registered as IPlayerInput.");

            // Tick system — must be a MonoBehaviour for Update()
            var tick = GetOrCreateChild<TickSystem>("TickSystem");
            ServiceLocator.Register(tick);
            Log("TickSystem registered.");

            // WorldGridManager — optional; may not exist in all scenes
            var grid = GetComponentInChildren<Grid.WorldGridManager>();
            if (grid != null)
            {
                ServiceLocator.Register(grid);
                Log("WorldGridManager registered.");
            }

            // CombatStateController — optional; may not exist in main menu
            var combat = GetComponentInChildren<Combat.CombatStateController>();
            if (combat != null)
            {
                ServiceLocator.Register(combat);
                Log("CombatStateController registered.");
            }

            // MultiplayerSessionManager — optional
            var multiplayer = GetComponentInChildren<Multiplayer.MultiplayerSessionManager>();
            if (multiplayer != null)
            {
                ServiceLocator.Register(multiplayer);
                Log("MultiplayerSessionManager registered.");
            }

            // SkillRegistry — ScriptableObject asset assigned in Inspector
            if (_skillRegistry != null)
            {
                ServiceLocator.Register(_skillRegistry);
                Log("SkillRegistry registered.");
            }
            else
            {
                Log("SkillRegistry not assigned — skill execution will log warnings at runtime.");
            }

            // SkillExecutionHandler — listens for SkillTargetingConfirmedEvent and
            // routes through SkillResolver. Requires CombatStateController to be present.
            if (GetComponentInChildren<Combat.CombatStateController>() != null)
            {
                GetOrCreateChild<Combat.SkillExecutionHandler>("SkillExecutionHandler");
                Log("SkillExecutionHandler created.");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns an existing child component of type T, or creates a new
        /// child GameObject with that component if none is found.
        /// </summary>
        private T GetOrCreateChild<T>(string goName) where T : MonoBehaviour
        {
            var existing = GetComponentInChildren<T>();
            if (existing != null) return existing;

            var go = new GameObject(goName);
            go.transform.SetParent(transform, worldPositionStays: false);
            return go.AddComponent<T>();
        }

        private void Log(string msg)
        {
            if (_verboseLogging) Debug.Log($"[GameBootstrapper] {msg}");
        }
    }
}
