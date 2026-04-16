using UnityEngine;
using PokemonAdventure.Data;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // IUnit Interface
    // Minimal contract that all participating entities (players, enemies, NPCs)
    // must fulfil. Keeps manager code independent of concrete implementations.
    //
    // Concrete implementations: PlayerUnit, EnemyUnit, NeutralUnit (all extend BaseUnit).
    // ==========================================================================

    public interface IUnit
    {
        // ── Identity ──────────────────────────────────────────────────────────

        /// <summary>Unique runtime ID (generated on spawn, used for serialisation).</summary>
        string UnitId { get; }

        string DisplayName { get; }
        UnitFaction Faction { get; }
        UnitRole    Role    { get; }

        // ── Stats & State ─────────────────────────────────────────────────────

        UnitStats        Stats        { get; }
        RuntimeUnitState RuntimeState { get; }

        // ── Position ──────────────────────────────────────────────────────────

        Vector2Int GridPosition { get; }
        Vector3    WorldPosition { get; }

        // ── Vitals ────────────────────────────────────────────────────────────

        bool IsAlive { get; }

        // ── Actions ───────────────────────────────────────────────────────────

        /// <summary>Apply damage. Routing through armor is handled by the implementer.</summary>
        void TakeDamage(float amount, DamageType damageType, IUnit source);

        void Heal(float amount);
        void RestorePhysicalArmor(float amount);
        void RestoreSpecialArmor(float amount);

        void ApplyStatusEffect(StatusEffectInstance effect);
        void RemoveStatusEffect(StatusEffectType effectType);

        // ── Turn Hooks ────────────────────────────────────────────────────────

        /// <summary>Called at the START of this unit's turn. Grants AP, ticks effects.</summary>
        void OnTurnStart();

        /// <summary>Called at the END of this unit's turn. Cleanup.</summary>
        void OnTurnEnd();
    }
}
