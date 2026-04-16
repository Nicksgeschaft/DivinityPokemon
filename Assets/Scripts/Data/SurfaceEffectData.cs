using System;

namespace PokemonAdventure.Data
{
    // ==========================================================================
    // Surface / Terrain Effect Data
    // Defines how terrain types affect units and how surfaces interact with
    // each other. Actual resolution logic lives in SurfaceEffectResolver (TODO).
    // ==========================================================================

    /// <summary>
    /// Static definition for a surface type's per-tick and on-enter behaviour.
    /// One definition should exist per SurfaceType value.
    /// </summary>
    [Serializable]
    public class SurfaceEffectDefinition
    {
        public SurfaceType SurfaceType;
        public string DisplayName;

        [UnityEngine.TextArea(2, 4)]
        public string Description;

        [UnityEngine.Header("Damage")]
        /// <summary>Damage applied to units on this surface each world tick.</summary>
        public float DamagePerTick;

        /// <summary>Type of the per-tick damage (used for armor resolution).</summary>
        public DamageType TickDamageType = DamageType.Physical;

        [UnityEngine.Header("Movement")]
        /// <summary>Movement AP cost multiplier. 1 = normal. 2 = double cost.</summary>
        public float MovementCostMultiplier = 1f;

        [UnityEngine.Header("On-Enter Status")]
        /// <summary>Status applied when a unit steps onto this surface.</summary>
        public StatusEffectType AppliedStatusOnEnter = StatusEffectType.None;
        public int AppliedStatusDuration;
        public float AppliedStatusMagnitude;

        [UnityEngine.Header("Interaction Flags")]
        /// <summary>Can this surface be ignited by Fire surfaces/skills?</summary>
        public bool Flammable;

        /// <summary>Does this surface conduct electricity from ElectricSurface?</summary>
        public bool Conductive;

        // TODO: Define combination rule lookup table separately (SurfaceCombination[]).
        // TODO: Add immunity list (e.g. Fire types step on FireSurface without damage).
    }

    /// <summary>
    /// Describes the result when two surface types interact in adjacent or
    /// overlapping cells. Evaluated by SurfaceEffectResolver.
    ///
    /// Example: FireSurface + OilSurface → Explosion (FireSurface, Burn on all in zone).
    ///          FireSurface + WaterSurface → Normal surface + Wet status applied.
    ///          ElectricSurface + WaterSurface → ElectricSurface propagated.
    /// </summary>
    [Serializable]
    public struct SurfaceCombination
    {
        public SurfaceType SourceA;
        public SurfaceType SourceB;
        public SurfaceType ResultSurface;
        public StatusEffectType ResultStatusOnUnitsInZone;
        public int ResultStatusDuration;
        public float ResultDamage;

        // TODO: Add radius for explosion-type combinations.
        // TODO: Add bool for whether the combination should trigger a VFX event.
    }
}
