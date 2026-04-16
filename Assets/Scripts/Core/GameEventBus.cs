using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Game Event Bus
    // Type-safe, decoupled publish/subscribe event system.
    // Publishers and subscribers never need direct references to each other.
    //
    // Usage:
    //   GameEventBus.Subscribe<CombatStartedEvent>(OnCombatStarted);
    //   GameEventBus.Publish(new CombatStartedEvent { Encounter = enc });
    //   GameEventBus.Unsubscribe<CombatStartedEvent>(OnCombatStarted);
    //
    // All event types are value types (struct) to avoid allocation overhead.
    // ==========================================================================

    public static class GameEventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        // ── Subscribe / Unsubscribe ───────────────────────────────────────────

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        // ── Publish ───────────────────────────────────────────────────────────

        public static void Publish<T>(T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
                return;

            // Snapshot to allow safe unsubscription inside handlers
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
            {
                try
                {
                    ((Action<T>)handler)(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameEventBus] Handler exception for <{typeof(T).Name}>: {ex}");
                }
            }
        }

        /// <summary>Clears all subscriptions. Call on application quit or full scene reload.</summary>
        public static void Clear() => _handlers.Clear();
    }

    // ==========================================================================
    // Core Game Event Structs
    // Keep event structs small (value types). Use IDs/handles instead of
    // direct object references where possible to avoid retention issues.
    // ==========================================================================

    // ── Game State ────────────────────────────────────────────────────────────

    public struct GameStateChangedEvent
    {
        public Data.GameState PreviousState;
        public Data.GameState NewState;
    }

    // ── Tick ──────────────────────────────────────────────────────────────────

    public struct WorldTickEvent
    {
        public int TickNumber;
        public float GameTime;
    }

    // ── Combat ────────────────────────────────────────────────────────────────

    public struct CombatStartedEvent
    {
        /// <summary>Unique identifier of the encounter that just started.</summary>
        public string EncounterId;
    }

    public struct CombatEndedEvent
    {
        public string EncounterId;
        public bool PlayerVictory;
    }

    // ── Turn ──────────────────────────────────────────────────────────────────

    public struct TurnStartedEvent
    {
        /// <summary>ID of the unit whose turn is beginning.</summary>
        public string ActiveUnitId;
        public int TurnNumber;
        public Data.UnitFaction ActiveFaction;
    }

    public struct TurnEndedEvent
    {
        public string ActiveUnitId;
    }

    // ── Units ─────────────────────────────────────────────────────────────────

    public struct UnitDiedEvent
    {
        public string UnitId;
        public string KillerUnitId; // null if no killer (e.g. environment)
        public Data.UnitFaction UnitFaction;
    }

    public struct UnitEnteredCombatEvent
    {
        public string UnitId;
        public string EncounterId;
    }

    public struct UnitStatusAppliedEvent
    {
        public string UnitId;
        public Data.StatusEffectType EffectType;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    public struct ActionExecutedEvent
    {
        public string ActorUnitId;
        public string ActionName;
        public int APSpent;
    }

    public struct MovementCompletedEvent
    {
        public string UnitId;
        public UnityEngine.Vector2Int FromCell;
        public UnityEngine.Vector2Int ToCell;
        public int APSpent;
    }

    // ── Turn Flow ─────────────────────────────────────────────────────────────

    public struct RoundStartedEvent
    {
        public int RoundNumber;
    }

    public struct TurnDelayedEvent
    {
        /// <summary>Unit that chose to delay its turn to the end of the current round.</summary>
        public string UnitId;
        public int NewQueuePosition;
    }

    /// <summary>Published by UnitController when the active unit wants to delay its turn.</summary>
    public struct TurnDelayRequestedEvent
    {
        public string UnitId;
    }

    public struct TurnPhaseChangedEvent
    {
        public string ActiveUnitId;
        public Combat.TurnPhase NewPhase;
    }

    // ── Skill Targeting ───────────────────────────────────────────────────────

    public struct SkillTargetingStartedEvent
    {
        public string CasterUnitId;
        public string SkillId;
    }

    public struct SkillTargetingConfirmedEvent
    {
        public string CasterUnitId;
        public string SkillId;
        public UnityEngine.Vector2Int TargetCell;
        public string TargetUnitId; // Empty if targeting ground
    }

    public struct SkillTargetingCancelledEvent
    {
        public string CasterUnitId;
        public string SkillId;
    }

    // ── Action Points ─────────────────────────────────────────────────────────

    /// <summary>
    /// Published by ActionPointController whenever a unit's AP changes.
    /// Delta is positive for gains, negative for spends, 0 for turn-start broadcast.
    /// </summary>
    public struct APChangedEvent
    {
        public string UnitId;
        public int    NewAP;
        public int    Delta;
    }

    // ── Damage ────────────────────────────────────────────────────────────────

    public struct DamageDealtEvent
    {
        public string AttackerUnitId;
        public string DefenderUnitId;
        public string SkillId;
        public float FinalDamage;
        public float ArmorAbsorbed;
        public float HPDamage;
        public Combat.EffectivenessCategory Effectiveness;
    }
}
