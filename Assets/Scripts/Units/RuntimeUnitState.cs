using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Runtime Unit State
    // All mutable in-play state for a unit, kept separate from UnitStats
    // (the base definition data). Reset on encounter start; persisted between
    // overworld encounters via save system (TODO).
    //
    // DESIGN: Managers read/write this object directly. Nothing here triggers
    // external effects — side-effects live in BaseUnit or system managers.
    // ==========================================================================

    [Serializable]
    public class RuntimeUnitState
    {
        // ── Vitals ────────────────────────────────────────────────────────────

        public float CurrentHP;
        public float CurrentPhysicalArmor;
        public float CurrentSpecialArmor;

        public bool IsAlive         => CurrentHP > 0f;
        public bool HasPhysArmor    => CurrentPhysicalArmor > 0f;
        public bool HasSpecArmor    => CurrentSpecialArmor > 0f;

        // ── Action Points ─────────────────────────────────────────────────────

        /// <summary>Current available AP. Persists unspent AP to next turn.</summary>
        public int CurrentAP;

        /// <summary>AP gained at the start of each turn (usually 3).</summary>
        public int APPerTurn = 3;

        /// <summary>Hard cap on accumulated AP (max slot count = 6).</summary>
        public const int MaxAPCap = 6;

        // ── Turn Tracking ─────────────────────────────────────────────────────

        /// <summary>Has this unit taken at least one action this turn?</summary>
        public bool HasActedThisTurn;

        /// <summary>Has this unit moved at least one cell this turn?</summary>
        public bool HasMovedThisTurn;

        // ── Grid Position ─────────────────────────────────────────────────────

        public Vector2Int GridPosition;

        /// <summary>Intermediate position during multi-step movement (partial move).</summary>
        public Vector2Int? PendingMovementTarget;

        public bool IsInCombat;

        // ── Sight & Stealth ───────────────────────────────────────────────────

        /// <summary>How many cells this unit can see (line-of-sight range).</summary>
        public int VisionRange = 8;

        /// <summary>Stealth rating; 0 = fully visible. Checked against enemy VisionRange.</summary>
        public int StealthLevel;

        // ── Status Effects ────────────────────────────────────────────────────

        public List<StatusEffectInstance> ActiveStatusEffects = new();

        // ── Skill Cooldowns ───────────────────────────────────────────────────

        /// <summary>Maps SkillDefinition.SkillId → remaining cooldown turns.</summary>
        public Dictionary<string, int> SkillCooldowns = new();

        // ── Stat Modifiers (temporary buffs/debuffs) ──────────────────────────

        // TODO: Replace with a typed modifier list that tracks source + expiry.
        // For now, flat additive modifiers are stored here for quick prototyping.
        public float AttackModifier;
        public float DefenseModifier;
        public float InitiativeModifier;

        // ═════════════════════════════════════════════════════════════════════
        // Initialisation
        // ═════════════════════════════════════════════════════════════════════

        public void Initialize(UnitStats stats)
        {
            CurrentHP             = stats.MaxHP;
            CurrentPhysicalArmor  = stats.MaxPhysicalArmor;
            CurrentSpecialArmor   = stats.MaxSpecialArmor;
            CurrentAP             = stats.BaseInitiative >= 5 ? 3 : 2;
            APPerTurn             = 3;
            HasActedThisTurn      = false;
            HasMovedThisTurn      = false;
            PendingMovementTarget = null;
            AttackModifier        = 0f;
            DefenseModifier       = 0f;
            InitiativeModifier    = 0f;
            ActiveStatusEffects.Clear();
            SkillCooldowns.Clear();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Action Points
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Add AP on turn start. Retained from last turn, capped at MaxAPCap.</summary>
        public void GainTurnAP()
        {
            CurrentAP = Mathf.Min(CurrentAP + APPerTurn, MaxAPCap);
        }

        public bool CanAfford(int cost) => CurrentAP >= cost && cost >= 0;

        /// <summary>Returns false without spending if cost exceeds current AP.</summary>
        public bool TrySpendAP(int cost)
        {
            if (!CanAfford(cost)) return false;
            CurrentAP -= cost;
            return true;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Status Effects
        // ═════════════════════════════════════════════════════════════════════

        public bool HasStatus(StatusEffectType type) =>
            ActiveStatusEffects.Exists(e => e.EffectType == type);

        /// <summary>Apply effect, refreshing duration if already present (no stacking).</summary>
        public void ApplyStatus(StatusEffectInstance newEffect)
        {
            var existing = ActiveStatusEffects.Find(e => e.EffectType == newEffect.EffectType);
            if (existing != null)
            {
                // Refresh: keep whichever duration is longer
                existing.RemainingTurns = Mathf.Max(existing.RemainingTurns, newEffect.RemainingTurns);
                existing.Magnitude      = newEffect.Magnitude; // Update potency
                return;
            }
            ActiveStatusEffects.Add(newEffect);
        }

        public void RemoveStatus(StatusEffectType type) =>
            ActiveStatusEffects.RemoveAll(e => e.EffectType == type);

        /// <summary>Decrement all effect durations; prune expired ones.</summary>
        public void TickStatusEffects() =>
            ActiveStatusEffects.RemoveAll(e => !e.Tick());

        // ═════════════════════════════════════════════════════════════════════
        // Skill Cooldowns
        // ═════════════════════════════════════════════════════════════════════

        public bool IsOnCooldown(string skillId) =>
            SkillCooldowns.TryGetValue(skillId, out var cd) && cd > 0;

        public void SetCooldown(string skillId, int turns) =>
            SkillCooldowns[skillId] = turns;

        public void TickCooldowns()
        {
            var keys = new List<string>(SkillCooldowns.Keys);
            foreach (var k in keys)
            {
                if (SkillCooldowns[k] > 0)
                    SkillCooldowns[k]--;
            }
        }
    }
}
