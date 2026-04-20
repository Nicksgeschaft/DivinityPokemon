using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Units;
using PokemonAdventure.World;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Party Switch Controller
    // Handles switching the active controlled character via Tab key.
    // Works in both Overworld and Combat states.
    //
    // In combat: switching to a character outside the encounter lets the player
    // move them freely in the overworld while the encounter continues.
    // In combat: switching to a combat participant just refocuses the camera.
    //
    // Place on any persistent GameObject (e.g. GameManager / GameBootstrapper).
    // ==========================================================================

    public class PartySwitchController : MonoBehaviour
    {
        private IPlayerInput _input;
        private List<PlayerUnit> _units = new();
        private int _currentIndex;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _input = ServiceLocator.Get<IPlayerInput>();

            GameEventBus.Subscribe<UnitRegisteredEvent>(OnUnitRegistered);
            GameEventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
            GameEventBus.Subscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);

            RebuildUnitList();
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<UnitRegisteredEvent>(OnUnitRegistered);
            GameEventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
            GameEventBus.Unsubscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_input == null || !_input.SwitchCharacterPressed) return;
            CycleToNextUnit();
        }

        // ── Cycle Logic ───────────────────────────────────────────────────────

        private void CycleToNextUnit()
        {
            if (_units.Count <= 1) return;

            // Find next alive unit, wrapping around
            for (int i = 1; i <= _units.Count; i++)
            {
                int idx  = (_currentIndex + i) % _units.Count;
                var unit = _units[idx];
                if (unit == null || !unit.IsAlive) continue;

                _currentIndex = idx;
                SwitchTo(unit);
                return;
            }
        }

        private void SwitchTo(PlayerUnit unit)
        {
            GameEventBus.Publish(new ActiveUnitChangedEvent { UnitId = unit.UnitId });

            var cam = FindAnyObjectByType<OverworldCameraController>();
            cam?.SetTarget(unit.transform);
        }

        // ── Unit List ─────────────────────────────────────────────────────────

        private void RebuildUnitList()
        {
            _units.Clear();
            var found = FindObjectsByType<PlayerUnit>(FindObjectsSortMode.None);
            // Sort by UnitId for deterministic order
            System.Array.Sort(found, (a, b) => string.Compare(a.UnitId, b.UnitId, System.StringComparison.Ordinal));
            _units.AddRange(found);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnUnitRegistered(UnitRegisteredEvent evt)
        {
            if (evt.Faction == Data.UnitFaction.Friendly)
                RebuildUnitList();
        }

        private void OnUnitDied(UnitDiedEvent evt)
        {
            if (evt.UnitFaction == Data.UnitFaction.Friendly)
                RebuildUnitList();
        }

        private void OnActiveUnitChanged(ActiveUnitChangedEvent evt)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i] != null && _units[i].UnitId == evt.UnitId)
                {
                    _currentIndex = i;
                    return;
                }
            }
        }
    }
}
