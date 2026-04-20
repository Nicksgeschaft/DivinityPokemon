using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Grid;
using PokemonAdventure.Combat;

namespace PokemonAdventure.UI
{
    // Shows a semi-transparent orange overlay on all grid cells within the combat
    // join radius of any active participant. Matches IsInsideCombatZone() exactly
    // (Euclidean radius via GridUtility.GetCellsInCircle). Updates on movement
    // and when a free unit joins. Disappears when combat ends.
    public class CombatZoneOverlay : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Semi-transparent orange. Low alpha so movement overlay reads on top.")]
        [SerializeField] private Color _zoneColor = new Color(1f, 0.55f, 0.1f, 0.22f);

        [Header("Radius")]
        [Tooltip("Must match OverworldMovementController._combatJoinRadius (default 6).")]
        [SerializeField] private float _zoneRadius = 6f;

        // ── Services ──────────────────────────────────────────────────────────

        private GridOverlay           _overlay;
        private WorldGridManager      _gridManager;
        private CombatStateController _combatController;

        // ── State ─────────────────────────────────────────────────────────────

        private readonly HashSet<Vector2Int> _zoneCells = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _overlay          = FindAnyObjectByType<GridOverlay>();
            _gridManager      = ServiceLocator.Get<WorldGridManager>();
            ServiceLocator.TryGet(out _combatController);

            GameEventBus.Subscribe<CombatStartedEvent>(OnCombatStarted);
            GameEventBus.Subscribe<CombatEndedEvent>(OnCombatEnded);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Subscribe<MovementCompletedEvent>(OnMovementCompleted);
            GameEventBus.Subscribe<UnitEnteredCombatEvent>(OnUnitEnteredCombat);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<CombatStartedEvent>(OnCombatStarted);
            GameEventBus.Unsubscribe<CombatEndedEvent>(OnCombatEnded);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Unsubscribe<MovementCompletedEvent>(OnMovementCompleted);
            GameEventBus.Unsubscribe<UnitEnteredCombatEvent>(OnUnitEnteredCombat);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnCombatStarted(CombatStartedEvent _)        => DrawZone();
        private void OnUnitEnteredCombat(UnitEnteredCombatEvent _) => DrawZone();

        private void OnCombatEnded(CombatEndedEvent _) => ClearZone();

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState == Data.GameState.Overworld)
                ClearZone();
        }

        private void OnMovementCompleted(MovementCompletedEvent evt)
        {
            var encounter = _combatController?.ActiveEncounter;
            if (encounter == null || !encounter.IsActive) return;

            // Only redraw if a participant moved — not a free-roaming overworld unit
            foreach (var p in encounter.Participants)
            {
                if (p != null && p.UnitId == evt.UnitId)
                {
                    DrawZone();
                    return;
                }
            }
        }

        // ── Zone Draw / Clear ─────────────────────────────────────────────────

        private void DrawZone()
        {
            if (_overlay == null || _gridManager == null || _combatController == null) return;

            var encounter = _combatController.ActiveEncounter;
            if (encounter == null || !encounter.IsActive) return;

            ClearZone();

            foreach (var participant in encounter.Participants)
            {
                if (participant == null || !participant.IsAlive) continue;

                foreach (var cell in GridUtility.GetCellsInCircle(participant.GridPosition, _zoneRadius))
                {
                    if (!_gridManager.IsInBounds(cell)) continue;

                    // HashSet.Add returns false when already present — avoids double-highlight
                    if (_zoneCells.Add(cell))
                        _overlay.HighlightCell(cell, _zoneColor);
                }
            }
        }

        private void ClearZone()
        {
            foreach (var cell in _zoneCells)
                _overlay.HideCell(cell);

            _zoneCells.Clear();
        }
    }
}
