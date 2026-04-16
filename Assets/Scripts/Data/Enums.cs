// =============================================================================
// PokemonAdventure – Core Enumerations
// All game-wide enums in one organized file for easy reference.
// Add new values at the END of each enum to avoid serialization shifts.
// =============================================================================

namespace PokemonAdventure.Data
{
    // ─── Pokémon Type System ──────────────────────────────────────────────────
    // Full 18-type system matching Generation VI+. Used for damage calculation,
    // skill typing, and surface interaction.
    public enum PokemonType
    {
        Normal    = 0,
        Fire      = 1,
        Water     = 2,
        Electric  = 3,
        Grass     = 4,
        Ice       = 5,
        Fighting  = 6,
        Poison    = 7,
        Ground    = 8,
        Flying    = 9,
        Psychic   = 10,
        Bug       = 11,
        Rock      = 12,
        Ghost     = 13,
        Dragon    = 14,
        Dark      = 15,
        Steel     = 16,
        Fairy     = 17,
        None      = 18  // Secondary type slot placeholder / typeless effects
    }

    // ─── Skill Category ───────────────────────────────────────────────────────
    public enum SkillCategory
    {
        Physical,   // Offensive; resolved against PhysicalArmor
        Special,    // Offensive; resolved against SpecialArmor
        Status      // Non-damaging; applies effects, modifies stats
    }

    // ─── Damage Type ──────────────────────────────────────────────────────────
    public enum DamageType
    {
        Physical,   // Depletes PhysicalArmor first, then HP
        Special,    // Depletes SpecialArmor first, then HP
        True,       // Bypasses both armor bars, hits HP directly
        Healing     // Negative damage (restoration)
    }

    // ─── Unit Faction ─────────────────────────────────────────────────────────
    public enum UnitFaction
    {
        Friendly,   // Player-controlled or allied units
        Neutral,    // Non-hostile NPCs; become Hostile if attacked
        Hostile     // Enemies that initiate combat on sight
    }

    // ─── Unit Role / Archetype ────────────────────────────────────────────────
    public enum UnitRole
    {
        Attacker,       // High offense, lower defense
        Defender,       // High HP and armor; frontline
        Support,        // Buffs, heals, utility focus
        Speedster,      // High initiative, mobility-based
        Controller,     // Crowd control and debuff specialist
        Specialist,     // Unique or irregular mechanics
        None
    }

    // ─── Item Type ────────────────────────────────────────────────────────────
    public enum ItemType
    {
        Consumable,     // Used once, then removed from inventory
        Equipment,      // Worn in an equipment slot
        KeyItem,        // Quest/story item; cannot be dropped or sold
        Material,       // Crafting ingredient
        Throwable       // Single-use ranged combat item
    }

    // ─── Equipment Slot ───────────────────────────────────────────────────────
    public enum EquipmentSlot
    {
        None,
        Head,
        Body,
        Accessory1,
        Accessory2,
        HeldItem    // Pokémon-style passive held item
    }

    // ─── Status Effect Type ───────────────────────────────────────────────────
    // Classic Pokémon conditions + DOS2-inspired extended set.
    // Resolution logic lives in StatusEffectResolver (TODO).
    public enum StatusEffectType
    {
        // ── Classic Pokémon Conditions ───────────────────────────────────────
        Burn,           // HP damage per turn; reduces Physical Attack
        Freeze,         // Cannot act; thawed by Fire damage
        Paralysis,      // Chance to lose turn; halves Initiative
        Poison,         // HP damage per turn (fixed)
        BadPoison,      // Escalating HP damage (Toxic)
        Sleep,          // Cannot act; woken by damage
        Confusion,      // Chance to attack self
        Flinch,         // Lose turn if hit before acting

        // ── Extended Conditions ───────────────────────────────────────────────
        Blind,          // Significantly reduced accuracy
        Taunt,          // Forced to use Physical/offensive moves only
        Cursed,         // Damage each turn; transferred on death
        Blessed,        // Bonus to next offensive action
        Rooted,         // Cannot move; can still use skills
        Silenced,       // Cannot use Special-category skills
        Stunned,        // Loses AP this turn
        Wet,            // Amplifies Electric damage; reduces Fire damage
        Oiled,          // Highly flammable; slippery surface interaction

        None
    }

    // ─── Surface / Terrain Type ───────────────────────────────────────────────
    // Applied to GridCells at runtime. Combinations handled by SurfaceEffectResolver.
    public enum SurfaceType
    {
        Normal,
        FireSurface,        // Burns units; ignites Oil
        WaterSurface,       // Applies Wet; conducts Electricity
        ElectricSurface,    // Shocks units standing on it
        PoisonSurface,      // Applies Poison on entry/tick
        IceSurface,         // Slippery; applies Freeze on fall
        MudSurface,         // Reduces movement speed
        OilSurface,         // Highly flammable; slippery
        BloodSurface,       // Can apply Cursed; attracts certain enemies
        SacredGround,       // Healing over time; boosts holy damage
        None
    }

    // ─── Combat State ─────────────────────────────────────────────────────────
    public enum CombatStateType
    {
        Inactive,           // No active combat encounter
        Initializing,       // Hard transition in progress; units being placed
        WaitingForTurn,     // Between turns; evaluating queue
        PlayerTurn,         // Local player's unit is the active actor
        RemotePlayerTurn,   // A network player's unit is the active actor
        AITurn,             // Enemy AI unit is the active actor
        ResolvingAction,    // Action animation / resolution playing out
        EndOfTurn,          // Per-turn cleanup and status ticks
        CombatEnd,          // Resolving victory / defeat condition
        Transitioning       // Hard transition back to overworld
    }

    // ─── Targeting Type ───────────────────────────────────────────────────────
    public enum TargetingType
    {
        Self,               // Only affects the skill user
        SingleEnemy,        // One hostile unit
        SingleAlly,         // One friendly unit
        SingleAny,          // Any one unit
        LineForward,        // All units in a line ahead of user
        Cone,               // Fan-shaped area ahead of user
        CircleAoE,          // Radius around a chosen target point
        CircleAroundSelf,   // Radius centered on the caster
        AllEnemies,         // Every hostile unit in encounter
        AllAllies,          // Every allied unit in encounter
        All,                // Every unit in encounter
        GroundTarget        // Targets a grid cell (no unit required)
    }

    // ─── AoE Shape (for area skill visuals & hit detection) ──────────────────
    public enum AoEShape
    {
        Single,
        Circle,
        Square,
        Cross,
        Line,
        Cone
    }

    // ─── Top-Level Game State ─────────────────────────────────────────────────
    public enum GameState
    {
        Booting,
        MainMenu,
        Loading,
        Overworld,
        Combat,
        Dialogue,
        Cutscene,
        Paused
    }
}
