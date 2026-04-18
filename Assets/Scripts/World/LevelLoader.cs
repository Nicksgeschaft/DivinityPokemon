using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Animations;
using PokemonAdventure.Combat;
using PokemonAdventure.Core;
using PokemonAdventure.Grid;
using PokemonAdventure.Movement;
using PokemonAdventure.Multiplayer;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.World
{
    // ==========================================================================
    // Level Loader
    // Reads a LevelData asset and builds the scene at runtime:
    //   - Coloured flat tile quads (URP-compatible)
    //   - Enemy units via EnemyPrefabController
    //   - Player units with movement + combat components
    //   - NPC placeholders
    //
    // Place this in the scene alongside GameBootstrapper and the camera.
    // Assign a LevelData asset to _levelData in the Inspector.
    //
    // Replaces DebugSceneSetup for authored content.
    // ==========================================================================

    public class LevelLoader : MonoBehaviour
    {
        [Header("Level")]
        [SerializeField] private LevelData _levelData;

        [Header("Player Definitions")]
        [Tooltip("PokemonDefinition for each player slot. Leave null to skip spawning.")]
        [SerializeField] private PokemonDefinition _player1Definition;
        [SerializeField] private PokemonDefinition _player2Definition;
        [SerializeField] private PokemonDefinition _player3Definition;
        [SerializeField] private PokemonDefinition _player4Definition;

        [Header("Tile Appearance")]
        [SerializeField] [Range(0f, 0.5f)]
        [Tooltip("Tile visual thickness (Y scale of the quad).")]
        private float _tileElevation = 0.02f;

        [SerializeField] [Range(0f, 0.05f)]
        [Tooltip("Gap between adjacent tiles (world units). 0 = seamless fill.")]
        private float _tileGap = 0f;

        // ── Private State ─────────────────────────────────────────────────────

        private WorldGridManager _gridManager;
        private Transform _tileRoot;
        private Transform _entityRoot;
        private readonly Dictionary<TileTerrain, Material> _materialCache = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // Wait one frame so GameBootstrapper services are registered
            yield return null;

            _gridManager = ServiceLocator.Get<WorldGridManager>();
            if (_gridManager == null) { Debug.LogError("[LevelLoader] WorldGridManager not found."); yield break; }
            if (_levelData  == null) { Debug.LogError("[LevelLoader] LevelData not assigned.");      yield break; }

            _tileRoot   = new GameObject("Tiles").transform;
            _entityRoot = new GameObject("Entities").transform;

            EnsureCombatStateController();
            BuildTiles();
            BuildEntities();
            PositionCamera();
        }

        // ── Tile Construction ─────────────────────────────────────────────────

        private void BuildTiles()
        {
            foreach (var tile in _levelData.Tiles)
            {
                var worldPos = _levelData.GridToWorld(tile.GridPosition);

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Tile_{tile.GridPosition.x}_{tile.GridPosition.y}";
                go.transform.SetParent(_tileRoot, false);
                go.transform.position = worldPos + new Vector3(0f, _tileElevation, 0f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                float cs  = _levelData.CellSize;
                float vis = cs - _tileGap;
                go.transform.localScale = new Vector3(vis, vis, 1f);

                DestroyImmediate(go.GetComponent<MeshCollider>());

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial    = GetOrCreateMaterial(tile.Terrain, tile.CustomColor);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows    = false;

                // Inform the grid
                _gridManager.SetWalkable(tile.GridPosition, tile.IsWalkable);
                _gridManager.SetSurface(tile.GridPosition, tile.Surface);
            }
        }

        private Material GetOrCreateMaterial(TileTerrain terrain, Color customColor)
        {
            if (terrain != TileTerrain.Custom && _materialCache.TryGetValue(terrain, out var cached))
                return cached;

            var mat = new Material(GetFlatColorShader());
            mat.color = terrain == TileTerrain.Custom ? customColor : TerrainColor(terrain);

            if (terrain != TileTerrain.Custom)
                _materialCache[terrain] = mat;

            return mat;
        }

        private static Shader GetFlatColorShader() =>
            Shader.Find("Universal Render Pipeline/Unlit")
         ?? Shader.Find("Unlit/Color")
         ?? Shader.Find("Standard");

        // ── Entity Spawning ───────────────────────────────────────────────────

        private void BuildEntities()
        {
            var playerDefs = new[]
            {
                _player1Definition,
                _player2Definition,
                _player3Definition,
                _player4Definition,
            };

            foreach (var entity in _levelData.Entities)
            {
                var worldPos = _levelData.GridToWorld(entity.GridPosition);
                worldPos.y = 0f;

                switch (entity.EntityType)
                {
                    case PlacedEntityType.Enemy:
                        SpawnEnemy(entity, worldPos);
                        break;

                    case PlacedEntityType.Player1:
                    case PlacedEntityType.Player2:
                    case PlacedEntityType.Player3:
                    case PlacedEntityType.Player4:
                        int slot = (int)entity.EntityType;
                        var def  = entity.PlayerDefinition ?? (slot < playerDefs.Length ? playerDefs[slot] : null);
                        SpawnPlayer(entity, worldPos, slot, def);
                        break;

                    case PlacedEntityType.NPC:
                        SpawnNPC(entity, worldPos);
                        break;
                }
            }
        }

        private void SpawnEnemy(EntityPlacementData data, Vector3 worldPos)
        {
            if (data.EnemyArchetype == null)
            {
                Debug.LogWarning($"[LevelLoader] Enemy at {data.GridPosition} has no archetype — skipped.");
                return;
            }

            var go = new GameObject();
            go.transform.SetParent(_entityRoot, false);
            go.transform.position = worldPos;

            var ctrl = go.AddComponent<EnemyPrefabController>();
            ctrl.Build(data.EnemyArchetype);
        }

        private void SpawnPlayer(EntityPlacementData data, Vector3 worldPos, int slot, PokemonDefinition def)
        {
            string unitName = def != null ? def.PokemonName : $"Player{slot + 1}";
            var go = new GameObject(unitName);
            go.transform.SetParent(_entityRoot, false);
            go.transform.position = worldPos;

            var unit = go.AddComponent<PlayerUnit>();
            if (def != null)
                unit.Initialize(def);

            go.AddComponent<PlayerUnitController>();
            go.AddComponent<ActionPointController>();
            go.AddComponent<CombatMovementController>();
            go.AddComponent<BasicAttackController>();
            go.AddComponent<OverworldMovementController>();

            // Sprite from definition
            var animSet = def?.AnimationSet;
            if (animSet != null)
            {
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(go.transform, false);
                spriteGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);

                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 1;

                var animator = spriteGo.AddComponent<PokemonSpriteAnimator>();
                animator.AnimSet = animSet;
            }

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1f;
            col.radius = 0.3f;

            // Wire into multiplayer session
            var session = ServiceLocator.TryGet(out MultiplayerSessionManager mgr) ? mgr : null;
            session?.AssignUnitToSlot(slot, unit, def);

            // Point camera at first player
            if (slot == 0)
            {
                var cam = FindAnyObjectByType<OverworldCameraController>();
                cam?.SetTarget(go.transform);
            }
        }

        private void SpawnNPC(EntityPlacementData data, Vector3 worldPos)
        {
            var go = new GameObject("NPC");
            go.transform.SetParent(_entityRoot, false);
            go.transform.position = worldPos;

            // Placeholder: coloured disc
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.transform.SetParent(go.transform, false);
            disc.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
            disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            disc.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0.1f);
            DestroyImmediate(disc.GetComponent<Collider>());

            go.AddComponent<PlayerUnit>(); // Temporary — replace with NeutralUnit
        }

        // ── Scene Helpers ─────────────────────────────────────────────────────

        private void EnsureCombatStateController()
        {
            if (ServiceLocator.TryGet(out CombatStateController _)) return;

            var go  = new GameObject("CombatStateController");
            var csc = go.AddComponent<CombatStateController>();
            ServiceLocator.Register(csc);
        }

        private void PositionCamera()
        {
            var cam = FindAnyObjectByType<OverworldCameraController>();
            if (cam == null) return;

            float span = Mathf.Max(_levelData.GridSize.x, _levelData.GridSize.y) * _levelData.CellSize;
            cam.ConfigureView(tiltDegrees: 90f, height: span * 0.75f);
        }

        // ── Terrain Colour Lookup ─────────────────────────────────────────────

        public static Color TerrainColor(TileTerrain terrain) => terrain switch
        {
            TileTerrain.Grass       => new Color(0.35f, 0.72f, 0.29f),
            TileTerrain.BurnedGrass => new Color(0.48f, 0.28f, 0.09f),
            TileTerrain.Sand        => new Color(0.93f, 0.84f, 0.52f),
            TileTerrain.Stone       => new Color(0.52f, 0.52f, 0.52f),
            TileTerrain.Water       => new Color(0.18f, 0.52f, 0.88f),
            TileTerrain.Lava        => new Color(0.90f, 0.34f, 0.04f),
            TileTerrain.Ice         => new Color(0.74f, 0.91f, 1.00f),
            TileTerrain.Void        => new Color(0.07f, 0.07f, 0.10f),
            TileTerrain.Path        => new Color(0.74f, 0.64f, 0.44f),
            TileTerrain.Forest      => new Color(0.17f, 0.43f, 0.17f),
            _                       => Color.white,
        };
    }
}
