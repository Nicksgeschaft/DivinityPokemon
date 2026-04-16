using System;
using UnityEngine;

namespace PokemonAdventure.Data
{
    // ==========================================================================
    // Status Effect Data
    // Separates static definition data (StatusEffectDefinition) from live
    // instance data (StatusEffectInstance) that lives on a unit at runtime.
    // ==========================================================================

    /// <summary>
    /// Live instance of a status effect on a specific unit.
    /// Effects do NOT stack — reapplying the same effect refreshes its duration.
    /// </summary>
    [Serializable]
    public class StatusEffectInstance
    {
        public StatusEffectType EffectType;

        /// <summary>Remaining turns. -1 = permanent until explicitly cleansed.</summary>
        public int RemainingTurns;

        /// <summary>Potency/magnitude (e.g. poison damage per tick, slow amount).</summary>
        public float Magnitude;

        /// <summary>ID of the unit that applied this effect (for credit / attribution).</summary>
        public string SourceUnitId;

        // TODO: Add hook here for armor-bar check when damage resolves.
        //       Physical effects should check PhysicalArmor; Special effects SpecialArmor.

        public bool IsExpired => RemainingTurns == 0;

        /// <summary>
        /// Decrements turn counter. Returns true if still active after tick.
        /// Permanent effects (RemainingTurns == -1) always return true.
        /// </summary>
        public bool Tick()
        {
            if (RemainingTurns > 0)
                RemainingTurns--;
            return !IsExpired;
        }

        public StatusEffectInstance Clone() => new()
        {
            EffectType     = EffectType,
            RemainingTurns = RemainingTurns,
            Magnitude      = Magnitude,
            SourceUnitId   = SourceUnitId
        };
    }

    /// <summary>
    /// Static definition of a status effect type. Describes behaviour flags
    /// so managers can query capabilities without hard-coding per-type logic.
    ///
    /// Actual per-turn resolution is handled by StatusEffectResolver (TODO).
    /// </summary>
    [Serializable]
    public class StatusEffectDefinition
    {
        public StatusEffectType EffectType;
        public string DisplayName;

        [TextArea(2, 4)]
        public string Description;

        [Header("Refresh / Stack Rules")]
        /// <summary>If true, reapplying refreshes duration. If false, adds stacks (future).</summary>
        public bool RefreshOnReapply = true;

        [Header("Armor Interaction")]
        /// <summary>Which armor bar is checked when this effect deals damage.</summary>
        public DamageType ArmorCheckType = DamageType.Physical;

        [Header("Control Flags")]
        public bool BlocksPlayerControl;
        public bool BlocksSkillUse;
        public bool BlocksMovement;
        public bool BlocksAPGeneration;

        [Header("Per-Turn Damage")]
        [Tooltip("Damage dealt to the unit each turn while afflicted. 0 = no per-turn damage.")]
        public float DamagePerTurn;

        // TODO: Add on-apply / on-tick / on-remove callback hooks via IStatusEffectHandler.
        // TODO: Add immunities list (e.g. Fire types immune to Burn).
        // TODO: Add surface interaction flags (e.g. Wet amplifies Electric damage by 50%).
    }
}
