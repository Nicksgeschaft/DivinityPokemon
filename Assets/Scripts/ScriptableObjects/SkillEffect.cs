using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.ScriptableObjects
{
    // ==========================================================================
    // Skill Effect — one discrete outcome of a SkillDefinition.
    // A skill can carry any number of these; the resolver iterates them in order.
    // ==========================================================================

    public enum SkillEffectType
    {
        Damage           = 0, // Deal physical or special damage to target unit(s)
        Heal             = 1, // Restore HP to target unit(s)
        Shield           = 2, // Add to physical or special armor of target unit(s)
        ApplyStatus      = 3, // Apply a StatusEffectType to target unit(s)
        StatModify       = 4, // Temporarily modify one or more stats on target unit(s)
        ApplyGridSurface = 5, // Set a SurfaceType on all cells hit by the skill
    }

    public enum EffectTarget
    {
        MainTarget = 0, // Applies to skill's primary target(s) as resolved by SkillExecutionHandler
        Caster     = 1, // Always applies to the skill user (e.g. self-heal on attack)
    }

    [System.Serializable]
    public class StatModifier
    {
        [Tooltip("Which stat to change.")]
        public StatType Stat      = StatType.Attack;

        [Tooltip("Flat amount or percentage depending on IsPercent.")]
        public float Value        = 10f;

        [Tooltip("If true, Value is treated as a percentage of the unit's base stat.")]
        public bool  IsPercent    = false;

        [Tooltip("How many turns the modifier lasts. 0 = permanent until combat ends.")]
        [Min(0)] public int Duration = 2;
    }

    [System.Serializable]
    public class SkillEffect
    {
        [Tooltip("What this effect does.")]
        public SkillEffectType EffectType = SkillEffectType.Damage;

        [Tooltip("Who receives this effect.")]
        public EffectTarget Target = EffectTarget.MainTarget;

        [Tooltip("Probability (0–100) that this effect fires at all on a successful hit. " +
                 "100 = always. Works for every effect type — e.g. set to 10 to give a " +
                 "Damage effect a 10 % proc chance.")]
        [Range(0, 100)] public int ApplyChance = 100;

        // ── Damage / Heal / Shield ────────────────────────────────────────────

        [Tooltip("Base power for Damage, HP amount for Heal, armor amount for Shield.")]
        [Min(0)] public int Power = 40;

        [Tooltip("Physical or Special — determines which armor bar is hit (Damage/Shield) " +
                 "or which attack stat is used in the damage formula (Damage).")]
        public DamageType DamageCategory = DamageType.Physical;

        // ── Apply Status ──────────────────────────────────────────────────────

        [Tooltip("Status effect to inflict on the target.")]
        public StatusEffectType StatusType = StatusEffectType.None;

        [Tooltip("How many turns the status lasts.")]
        [Min(1)] public int StatusDuration = 2;

        [Tooltip("Numeric magnitude (e.g. poison damage per tick). Leave 0 if unused.")]
        public float StatusMagnitude = 0f;

        // ── Apply Grid Surface ────────────────────────────────────────────────

        [Tooltip("Terrain type placed on all cells hit by the skill.")]
        public SurfaceType SurfaceToApply = SurfaceType.FireSurface;

        [Tooltip("How many turns the surface lasts. 0 = permanent until overwritten.")]
        [Min(0)] public int SurfaceDuration = 3;

        [Tooltip("Visual prefab spawned at each affected cell's world position.")]
        public GameObject SurfacePrefab;

        // ── Stat Modify ───────────────────────────────────────────────────────

        [Tooltip("One or more stat changes applied to the target. Each can be flat or percentage.")]
        public List<StatModifier> StatModifiers = new();
    }
}
