using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;

namespace PokemonAdventure.ScriptableObjects
{
    // ── Terrain Visual Type ───────────────────────────────────────────────────
    // Drives tile colour in both the editor and runtime.
    // Combine with SurfaceType for runtime effects.

    public enum TileTerrain
    {
        Grass       = 0,
        BurnedGrass = 1,
        Sand        = 2,
        Stone       = 3,
        Water       = 4,
        Lava        = 5,
        Ice         = 6,
        Void        = 7,
        Path        = 8,
        Forest      = 9,
        Custom      = 10,
    }

    // ── Entity Placement Type ─────────────────────────────────────────────────

    public enum PlacedEntityType
    {
        Player1 = 0,
        Player2 = 1,
        Player3 = 2,
        Player4 = 3,
        Enemy   = 4,
        NPC     = 5,
    }

    // ── Placement Records ─────────────────────────────────────────────────────

    [Serializable]
    public class TilePlacementData
    {
        public Vector2Int GridPosition;
        public TileTerrain Terrain    = TileTerrain.Grass;
        public SurfaceType Surface    = SurfaceType.Normal;
        public bool        IsWalkable = true;
        public Color       CustomColor = Color.white;  // used when Terrain == Custom
    }

    [Serializable]
    public class EntityPlacementData
    {
        public Vector2Int  GridPosition;
        public PlacedEntityType EntityType;

        // Enemy only
        public EnemyArchetypeDefinition EnemyArchetype;

        // Player 1-4
        public PokemonDefinition PlayerDefinition;
    }

    // ── Level Data Asset ──────────────────────────────────────────────────────

    [CreateAssetMenu(
        menuName = "PokemonAdventure/Level Data",
        fileName = "NewLevel",
        order    = 10)]
    public class LevelData : ScriptableObject
    {
        [Header("Grid")]
        public Vector2Int GridSize   = new(20, 20);
        public float      CellSize   = 1f;
        public Vector3    GridOrigin = Vector3.zero;

        [Header("Content")]
        public List<TilePlacementData>   Tiles    = new();
        public List<EntityPlacementData> Entities = new();

        // ── Tile Helpers ──────────────────────────────────────────────────────

        public TilePlacementData GetTile(Vector2Int pos) =>
            Tiles.Find(t => t.GridPosition == pos);

        public bool HasTile(Vector2Int pos) =>
            Tiles.Exists(t => t.GridPosition == pos);

        public void SetTile(Vector2Int pos, TileTerrain terrain, SurfaceType surface,
                            bool walkable, Color customColor = default)
        {
            var existing = GetTile(pos);
            if (existing != null)
            {
                existing.Terrain     = terrain;
                existing.Surface     = surface;
                existing.IsWalkable  = walkable;
                existing.CustomColor = customColor;
            }
            else
            {
                Tiles.Add(new TilePlacementData
                {
                    GridPosition = pos,
                    Terrain      = terrain,
                    Surface      = surface,
                    IsWalkable   = walkable,
                    CustomColor  = customColor
                });
            }
        }

        public void RemoveTile(Vector2Int pos) =>
            Tiles.RemoveAll(t => t.GridPosition == pos);

        // ── Entity Helpers ────────────────────────────────────────────────────

        public EntityPlacementData GetEntity(Vector2Int pos) =>
            Entities.Find(e => e.GridPosition == pos);

        public bool HasEntity(Vector2Int pos) =>
            Entities.Exists(e => e.GridPosition == pos);

        public void SetEntity(Vector2Int pos, PlacedEntityType type,
                              EnemyArchetypeDefinition archetype = null,
                              PokemonDefinition playerDef        = null)
        {
            RemoveEntity(pos);
            Entities.Add(new EntityPlacementData
            {
                GridPosition     = pos,
                EntityType       = type,
                EnemyArchetype   = archetype,
                PlayerDefinition = playerDef,
            });
        }

        public void RemoveEntity(Vector2Int pos) =>
            Entities.RemoveAll(e => e.GridPosition == pos);

        // ── Coordinate Helpers (mirrors GridUtility, usable without runtime) ─

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return GridOrigin + new Vector3(
                gridPos.x * CellSize + CellSize * 0.5f,
                0f,
                gridPos.y * CellSize + CellSize * 0.5f);
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            var local = worldPos - GridOrigin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / CellSize),
                Mathf.FloorToInt(local.z / CellSize));
        }

        public bool IsInBounds(Vector2Int pos) =>
            pos.x >= 0 && pos.x < GridSize.x &&
            pos.y >= 0 && pos.y < GridSize.y;
    }
}
