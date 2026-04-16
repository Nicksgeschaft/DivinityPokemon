using System.Collections;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.Movement;
using PokemonAdventure.Units;
using PokemonAdventure.AI;
using PokemonAdventure.Combat;

namespace PokemonAdventure.World
{
    // ==========================================================================
    // Debug Scene Setup
    // Procedurally creates a minimal test scene at runtime for rapid iteration.
    // Place this component on any GameObject in a test scene.
    //
    // Creates:
    //   ✓ A flat grid plane
    //   ✓ A player-controlled Friendly unit
    //   ✓ A Hostile test enemy with SightTrigger
    //   ✓ A Neutral NPC
    //   ✓ A Friendly NPC (allied unit)
    //   ✓ Gizmo visualisation for all sight radii and combat zones
    //
    // NOTE: This is for development only. Remove from production builds.
    // ==========================================================================

    public class DebugSceneSetup : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private int   _testGridSize = 20;
        [SerializeField] private float _cellSize     = 1f;

        [Header("Unit Prefabs (optional — uses primitives if null)")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _hostilePrefab;
        [SerializeField] private GameObject _neutralPrefab;
        [SerializeField] private GameObject _friendlyNPCPrefab;

        [Header("Sprite Animations (optional)")]
        [Tooltip("Assign Charmander's AnimationSet to replace the blue capsule with sprite animation.")]
        [SerializeField] private PokemonAnimationSet _playerAnimSet;

        [Header("Spawn Positions (Grid Coordinates)")]
        [SerializeField] private Vector2Int _playerSpawn        = new(5, 5);
        [SerializeField] private Vector2Int _hostileSpawn       = new(12, 12);
        [SerializeField] private Vector2Int _neutralSpawn       = new(5, 12);
        [SerializeField] private Vector2Int _friendlyNPCSpawn   = new(7, 5);

        [Header("Debug Options")]
        [SerializeField] private bool _logSetupSteps = true;

        private WorldGridManager _gridManager;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // Wait one frame so every other Start() (including OverworldCameraController.Start()
            // which resets _currentHeight from its serialised _defaultHeight) has already run.
            // This guarantees our ConfigureView(90°, 12) call is the LAST thing that sets
            // the camera angle — not the first.
            yield return null;

            _gridManager = ServiceLocator.Get<WorldGridManager>();
            if (_gridManager == null)
            {
                Debug.LogError("[DebugSceneSetup] WorldGridManager not found. " +
                               "Make sure GameBootstrapper has run first.");
                yield break;
            }

            EnsureCombatStateController();
            EnsureDebugGridRenderer();
            EnsureGridOverlay();
            SpawnTestUnits();
            Log("Debug scene setup complete.");
        }

        /// <summary>Adds GridOverlay to the scene if not already present.</summary>
        private void EnsureGridOverlay()
        {
            if (FindFirstObjectByType<Grid.GridOverlay>() != null) return;

            var go = new GameObject("GridOverlay");
            go.AddComponent<Grid.GridOverlay>();
            Log("GridOverlay created.");
        }

        /// <summary>Adds DebugGridRenderer to the scene if not already present.</summary>
        private void EnsureDebugGridRenderer()
        {
            if (FindFirstObjectByType<DebugGridRenderer>() != null) return;

            var go = new GameObject("DebugGridRenderer");
            go.AddComponent<DebugGridRenderer>();
            Log("DebugGridRenderer created.");
        }

        /// <summary>
        /// Creates and registers CombatStateController if GameBootstrapper didn't find
        /// one in its hierarchy (e.g. this debug scene has no pre-placed combat GO).
        /// </summary>
        private void EnsureCombatStateController()
        {
            if (ServiceLocator.TryGet(out Combat.CombatStateController _)) return;

            var go  = new GameObject("CombatStateController");
            var csc = go.AddComponent<Combat.CombatStateController>();
            ServiceLocator.Register(csc);
            Log("CombatStateController created and registered.");
        }

        // ── Unit Spawning ─────────────────────────────────────────────────────

        private void SpawnTestUnits()
        {
            var playerGo = SpawnPlayerUnit();
            SpawnHostileEnemy();
            SpawnNeutralNPC();
            SpawnFriendlyNPC();

            // Force true top-down view regardless of whatever angle is saved in the scene
            var cam = FindFirstObjectByType<OverworldCameraController>();
            if (cam != null)
            {
                cam.SetTarget(playerGo.transform);
                cam.ConfigureView(tiltDegrees: 90f, height: 12f);
            }
            else
                Log("No OverworldCameraController found — assign one to Main Camera for follow-cam.");
        }

        private GameObject SpawnPlayerUnit()
        {
            var go = SpawnUnit(_playerPrefab, "TestPlayer", _playerSpawn, Color.blue);

            // Unit + controller first so GetComponent<BaseUnit>() works in movement controllers
            go.AddComponent<PlayerUnit>();
            go.AddComponent<PlayerUnitController>();

            // CapsuleCollider for general physics interactions
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1f;
            col.radius = 0.3f;

            // Replace blue capsule mesh with sprite animation if an AnimSet is assigned
            if (_playerAnimSet != null)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;

                // Sprite lies flat on the grid — just above the surface to avoid z-fighting.
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(go.transform, false);
                spriteGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);

                var sr           = spriteGo.AddComponent<SpriteRenderer>();
                sr.sortingOrder  = 1;

                var animator     = spriteGo.AddComponent<PokemonSpriteAnimator>();
                animator.AnimSet = _playerAnimSet;
            }

            // AP tracking — needed by CombatMovementController and BasicAttackController
            go.AddComponent<Units.ActionPointController>();

            // Movement / attack controllers — order matters: unit must exist first
            go.AddComponent<Movement.CombatMovementController>();
            go.AddComponent<Combat.BasicAttackController>();
            go.AddComponent<OverworldMovementController>();

            Log($"Spawned PlayerUnit at {_playerSpawn}");
            return go;
        }

        private void SpawnHostileEnemy()
        {
            var go = SpawnUnit(_hostilePrefab, "TestEnemy", _hostileSpawn, Color.red);

            go.AddComponent<EnemyUnit>();
            go.AddComponent<Units.ActionPointController>();
            go.AddComponent<AIController>();
            go.AddComponent<CombatTriggerDetector>(); // No physics needed — polled per grid step

            Log($"Spawned EnemyUnit at {_hostileSpawn}");
        }

        private void SpawnNeutralNPC()
        {
            var go = SpawnUnit(_neutralPrefab, "TestNeutralNPC", _neutralSpawn, Color.yellow);

            // Use base unit as neutral placeholder
            // TODO: Create a dedicated NeutralUnit subclass with dialogue/quest hooks
            var unit = go.AddComponent<PlayerUnit>(); // Temporary; replace with NeutralUnit

            Log($"Spawned Neutral NPC at {_neutralSpawn}");
        }

        private void SpawnFriendlyNPC()
        {
            var go = SpawnUnit(_friendlyNPCPrefab, "TestFriendlyNPC", _friendlyNPCSpawn, Color.green);

            var unit = go.AddComponent<PlayerUnit>(); // Temporary; replace with FriendlyNPCUnit

            Log($"Spawned Friendly NPC at {_friendlyNPCSpawn}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private GameObject SpawnUnit(GameObject prefab, string unitName, Vector2Int gridPos, Color debugColor)
        {
            GameObject go;
            var worldPos = _gridManager.GetWorldPosition(gridPos);
            worldPos.y  = 0f; // Sit on grid surface

            if (prefab != null)
            {
                go = Instantiate(prefab, worldPos, Quaternion.identity);
            }
            else
            {
                // Fallback: flat coloured disc (Cylinder) so it looks like a
                // top-down token rather than a floating capsule.
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f); // Wide & flat
                go.transform.position   = worldPos + Vector3.up * 0.05f;  // Flush on grid
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = debugColor;

                // Remove auto-added collider — movement controllers use grid
                // occupancy, not physics colliders, for position tracking.
                var col = go.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
            }

            go.name = unitName;
            return go;
        }

        private void Log(string msg)
        {
            if (_logSetupSteps) Debug.Log($"[DebugSceneSetup] {msg}");
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (_gridManager == null) return;

            DrawSpawnGizmo(_playerSpawn,      Color.blue,   "Player");
            DrawSpawnGizmo(_hostileSpawn,     Color.red,    "Hostile");
            DrawSpawnGizmo(_neutralSpawn,     Color.yellow, "Neutral");
            DrawSpawnGizmo(_friendlyNPCSpawn, Color.green,  "FriendlyNPC");
        }

        private void DrawSpawnGizmo(Vector2Int gridPos, Color color, string label)
        {
            if (_gridManager == null) return;
            var worldPos = _gridManager.GetWorldPosition(gridPos);
            Gizmos.color = color;
            Gizmos.DrawSphere(worldPos + Vector3.up * 0.5f, 0.3f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(worldPos + Vector3.up * 1.2f, label);
#endif
        }
    }
}
