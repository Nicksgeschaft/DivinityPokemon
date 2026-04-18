using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // Shows skill range on the GridOverlay when the player hovers a skill slot
    // outside of combat. Listens to SkillPreviewEvent and GameStateChangedEvent.
    // Attach this to any persistent GameObject in the scene (e.g. the HUD root).
    public class OverworldSkillPreview : MonoBehaviour
    {
        private GridOverlay    _overlay;
        private WorldGridManager _gridManager;
        private bool           _inCombat;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _overlay     = FindAnyObjectByType<GridOverlay>();
            _gridManager = ServiceLocator.TryGet(out WorldGridManager mgr) ? mgr : null;
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<SkillPreviewEvent>(OnSkillPreview);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<SkillPreviewEvent>(OnSkillPreview);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            _inCombat = evt.NewState == GameState.Combat;
            if (_inCombat)
                ClearPreview();
        }

        private void OnSkillPreview(SkillPreviewEvent evt)
        {
            if (_inCombat) return;

            if (string.IsNullOrEmpty(evt.SkillId))
            {
                ClearPreview();
                return;
            }

            // Resolve caster position
            var caster = FindCaster(evt.CasterUnitId);
            if (caster == null) { ClearPreview(); return; }

            // Resolve skill definition
            var skill = FindSkill(evt.SkillId);
            if (skill == null) { ClearPreview(); return; }

            ShowRange(caster.RuntimeState.GridPosition, skill.Range);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ShowRange(Vector2Int center, int range)
        {
            if (_overlay == null || _gridManager == null) return;

            _overlay.HideAll();

            var positions = GridUtility.GetCellsInManhattanRange(center, range);
            positions.Remove(center);

            var cells = new List<GridCell>(positions.Count);
            foreach (var pos in positions)
            {
                var cell = _gridManager.GetCell(pos);
                if (cell != null) cells.Add(cell);
            }

            _overlay.Show(cells);
            _overlay.MarkSkillArea(cells);
        }

        private void ClearPreview()
        {
            _overlay?.HideAll();
        }

        private static BaseUnit FindCaster(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return FindAnyObjectByType<PlayerUnit>();

            foreach (var unit in FindObjectsByType<BaseUnit>(FindObjectsSortMode.None))
                if (unit.UnitId == unitId) return unit;

            return null;
        }

        private static SkillDefinition FindSkill(string skillId)
        {
            // Skills are ScriptableObjects — load all from the project
            var all = Resources.FindObjectsOfTypeAll<SkillDefinition>();
            foreach (var s in all)
                if (s.SkillId == skillId) return s;
            return null;
        }
    }
}
