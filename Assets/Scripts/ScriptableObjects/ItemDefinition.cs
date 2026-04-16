using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.ScriptableObjects
{
    // ==========================================================================
    // Item Definition (ScriptableObject)
    // Design-time specification for inventory items (consumables and equipment).
    // Create via: Assets → Create → PokemonAdventure → ItemDefinition
    // ==========================================================================

    [CreateAssetMenu(
        menuName = "PokemonAdventure/ItemDefinition",
        fileName = "New ItemDefinition",
        order    = 3)]
    public class ItemDefinition : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Header("Identity")]
        public string ItemId   = "item_unnamed";
        public string ItemName = "Unknown Item";

        [TextArea(2, 4)]
        public string Description;

        public Sprite ItemIcon;

        // ── Classification ────────────────────────────────────────────────────

        [Header("Classification")]
        public ItemType Type         = ItemType.Consumable;
        public bool     IsConsumable = true;

        [Tooltip("If Type == Equipment, which slot does it occupy?")]
        public EquipmentSlot Slot = EquipmentSlot.None;

        // ── Combat Usage ──────────────────────────────────────────────────────

        [Header("Combat")]
        [Tooltip("AP cost to use this item during combat. 0 = free action.")]
        [Range(0, 6)] public int APCostInCombat = 1;

        [Tooltip("If true, can be used as a thrown projectile targeting a grid cell.")]
        public bool CanBeThrown;

        [Tooltip("Throw range in grid cells (if CanBeThrown is true).")]
        [Min(1)] public int ThrowRange = 4;

        // ── Effects ───────────────────────────────────────────────────────────

        [Header("Effects")]
        [Tooltip("List of effects this item applies when used.")]
        public List<ItemEffect> Effects = new();

        // ── Equipment Stat Modifiers ──────────────────────────────────────────

        [Header("Equipment Stat Bonuses (Equipment only)")]
        [Tooltip("Flat bonus added to the wearer's UnitStats when equipped.")]
        public int BonusHP;
        public int BonusAttack;
        public int BonusDefense;
        public int BonusSpecialAttack;
        public int BonusSpecialDefense;
        public int BonusInitiative;

        // ── Economy ───────────────────────────────────────────────────────────

        [Header("Economy")]
        [Min(0)] public int BuyPrice;
        [Min(0)] public int SellPrice;
        public bool CanBeSold = true;
    }

    // ==========================================================================
    // Item Effect
    // Describes one discrete effect triggered when an item is used.
    // Effects are interpreted by ItemEffectResolver (TODO).
    // ==========================================================================

    [System.Serializable]
    public class ItemEffect
    {
        public ItemEffectType EffectType;
        public float          Magnitude;
        public StatType       TargetStat;  // For stat-restore effects
        public StatusEffectType StatusToCure; // For cure effects

        // TODO: Add targeting (self / ally / enemy / AoE) when resolver is implemented.
    }

    // ==========================================================================
    // Item Effect Type Enum
    // ==========================================================================

    public enum ItemEffectType
    {
        RestoreHP,              // Heal target's HP by Magnitude
        RestorePhysicalArmor,   // Restore physical armor bar
        RestoreSpecialArmor,    // Restore special armor bar
        RestoreAP,              // Grant AP to target
        CureStatus,             // Remove StatusToCure from target
        CureAllStatus,          // Remove all status effects
        ApplyStatus,            // Apply a status effect (StatusToCure field used inversely)
        BoostStat,              // Temporarily boost a stat (TargetStat)
        GrantShield,            // Grant temporary absorption shield
        ReviveUnit,             // Revive a downed unit with partial HP
        DealDamage,             // Throwable damage item
        CreateSurface           // Create a surface effect on the grid
    }
}
