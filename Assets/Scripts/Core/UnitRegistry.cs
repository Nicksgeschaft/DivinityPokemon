using System.Collections.Generic;
using PokemonAdventure.Units;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Unit Registry
    // Central lookup service: UnitId (string) → BaseUnit reference.
    //
    // Units self-register in Start() and deregister in OnDestroy() so the
    // registry always reflects the live scene state.
    //
    // Registered with ServiceLocator by GameBootstrapper as a pure C# object.
    // Callers never hold a direct BaseUnit reference across frames — always
    // re-resolve via TryGet to handle units that died / left the scene.
    // ==========================================================================

    public class UnitRegistry
    {
        private readonly Dictionary<string, BaseUnit> _units = new();

        // ── Registration ──────────────────────────────────────────────────────

        public void Register(BaseUnit unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.UnitId)) return;
            _units[unit.UnitId] = unit;
        }

        public void Unregister(string unitId)
        {
            if (!string.IsNullOrEmpty(unitId))
                _units.Remove(unitId);
        }

        // ── Lookup ────────────────────────────────────────────────────────────

        public bool TryGet(string unitId, out BaseUnit unit)
        {
            unit = null;
            if (string.IsNullOrEmpty(unitId)) return false;
            return _units.TryGetValue(unitId, out unit);
        }

        /// <summary>Returns the unit or null if not found.</summary>
        public BaseUnit Get(string unitId) =>
            TryGet(unitId, out var u) ? u : null;

        /// <summary>Read-only view of all currently registered units.</summary>
        public IReadOnlyDictionary<string, BaseUnit> All => _units;

        public void Clear() => _units.Clear();
    }
}
