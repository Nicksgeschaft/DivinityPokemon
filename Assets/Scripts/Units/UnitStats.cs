using System;
using UnityEngine;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Unit Stats
    // Defines base stat values and computes derived stats for a unit.
    //
    // DESIGN INTENT:
    //   HP      — raw vitality. Restored only by heals, not combat regeneration.
    //   PhysicalArmor — separate bar depleted by Physical skills first.
    //   SpecialArmor  — separate bar depleted by Special skills first.
    //   Defense / SpecialDefense — scale the ARMOR MAXIMUMS, not divide damage.
    //   This allows DOS2-style "armour as a separate layer" gameplay.
    //
    // SCALING:
    //   MaxHP and armor bars grow with Level + stat investment (BonusXxx).
    //   Attack/SpAtk grow with level for effective damage output.
    //   Initiative is the primary turn-order determinant.
    // ==========================================================================

    [Serializable]
    public class UnitStats
    {
        // ── Base Stats (set by PokemonDefinition or EnemyArchetypeDefinition) ─

        [Header("Vitality")]
        [Min(1)] public int BaseHP = 80;

        [Header("Offense")]
        [Min(0)] public int BaseAttack        = 10;
        [Min(0)] public int BaseSpecialAttack = 10;

        [Header("Defense (scales armor maximums)")]
        [Min(0)] public int BaseDefense        = 10;
        [Min(0)] public int BaseSpecialDefense = 10;

        [Header("Speed")]
        [Min(1)] public int BaseInitiative = 5;

        // ── Level ─────────────────────────────────────────────────────────────

        [Header("Level")]
        [Range(1, 100)] public int Level = 1;

        // ── Flat Bonuses (from equipment, permanent upgrades, etc.) ───────────

        [Header("Flat Bonuses")]
        public float BonusHP;
        public float BonusPhysicalArmor;
        public float BonusSpecialArmor;
        public float BonusAttack;
        public float BonusSpecialAttack;
        public float BonusInitiative;

        // ── Derived Stats ─────────────────────────────────────────────────────

        // Scaling constants — tuned via playtesting.
        private const float HpPerLevel          = 8f;
        private const float ArmorScaleFactor    = 1.5f;
        private const float AttackScaleFactor   = 0.5f;
        private const float InitiativePerLevel  = 0.3f;

        public float MaxHP =>
            BaseHP + (Level - 1) * HpPerLevel + BonusHP;

        public float MaxPhysicalArmor =>
            BaseDefense * ArmorScaleFactor * (1f + (Level - 1) * 0.08f) + BonusPhysicalArmor;

        public float MaxSpecialArmor =>
            BaseSpecialDefense * ArmorScaleFactor * (1f + (Level - 1) * 0.08f) + BonusSpecialArmor;

        public float EffectiveAttack =>
            BaseAttack + (Level - 1) * AttackScaleFactor + BonusAttack;

        public float EffectiveSpecialAttack =>
            BaseSpecialAttack + (Level - 1) * AttackScaleFactor + BonusSpecialAttack;

        public float EffectiveInitiative =>
            BaseInitiative + (Level - 1) * InitiativePerLevel + BonusInitiative;

        // ── Typed Stat Access ─────────────────────────────────────────────────

        /// <summary>Generic stat lookup by StatType enum. Avoids string-based access.</summary>
        public float GetStat(StatType statType) => statType switch
        {
            StatType.HP               => MaxHP,
            StatType.Attack           => EffectiveAttack,
            StatType.Defense          => BaseDefense,
            StatType.SpecialAttack    => EffectiveSpecialAttack,
            StatType.SpecialDefense   => BaseSpecialDefense,
            StatType.Initiative       => EffectiveInitiative,
            StatType.MaxPhysicalArmor => MaxPhysicalArmor,
            StatType.MaxSpecialArmor  => MaxSpecialArmor,
            _                         => 0f
        };
    }

    // ==========================================================================
    // Stat Type Enum
    // Strongly-typed identifiers for all stat slots.
    // Use these instead of string keys for all stat lookups.
    // ==========================================================================

    public enum StatType
    {
        HP,
        Attack,
        Defense,
        SpecialAttack,
        SpecialDefense,
        Initiative,
        MaxPhysicalArmor,
        MaxSpecialArmor
    }
}
