using System.Text;
using UnityEngine;

namespace PokemonAdventure.Data
{
    // ==========================================================================
    // Type Effectiveness Table
    // Full 18-type Pokémon type chart (Generation VI+, includes Fairy type).
    // Static lookup — data never changes at runtime.
    //
    // STAB (Same-Type Attack Bonus) is a SEPARATE modifier, not baked into
    // this table. Call GetSTABMultiplier() independently and multiply onto
    // the pipeline result.
    //
    // Combined dual-type multipliers (4×, 0.25×, 0×) emerge automatically
    // from GetDualTypeMultiplier() by multiplying both individual lookups.
    //
    // Indexing: [attacker, defender], matching PokemonType enum ordinals.
    //           PokemonType.None (18) is always 1.0 and treated as "no type".
    // ==========================================================================

    public static class TypeEffectivenessTable
    {
        // 19 entries: indices 0–17 = types, 18 = None
        private static readonly float[,] _chart = BuildChart();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Single-type effectiveness multiplier.
        /// Returns 1.0 if either type is None.
        /// </summary>
        public static float GetMultiplier(PokemonType attackType, PokemonType defenderType)
        {
            if (attackType == PokemonType.None || defenderType == PokemonType.None)
                return 1.0f;
            return _chart[(int)attackType, (int)defenderType];
        }

        /// <summary>
        /// Dual-type effectiveness multiplier (type1 × type2).
        /// Pass PokemonType.None for type2 if the defender is single-typed.
        /// Naturally produces 4×, 0.25×, and 0× from the combination.
        /// </summary>
        public static float GetDualTypeMultiplier(
            PokemonType attackType,
            PokemonType defenderType1,
            PokemonType defenderType2)
        {
            float m1 = GetMultiplier(attackType, defenderType1);
            float m2 = defenderType2 == PokemonType.None
                ? 1.0f
                : GetMultiplier(attackType, defenderType2);
            return m1 * m2;
        }

        /// <summary>
        /// STAB (Same-Type Attack Bonus) multiplier.
        /// Returns 1.5 if the skill's type matches either of the caster's types.
        /// Returns 1.0 otherwise.
        /// STAB is intentionally separate from the type chart — multiply it into
        /// the damage modifier pipeline independently.
        /// </summary>
        public static float GetSTABMultiplier(
            PokemonType skillType,
            PokemonType casterType1,
            PokemonType casterType2)
        {
            if (skillType == PokemonType.None) return 1.0f;
            bool hasSTAB = skillType == casterType1 ||
                           (casterType2 != PokemonType.None && skillType == casterType2);
            return hasSTAB ? 1.5f : 1.0f;
        }

        /// <summary>
        /// Returns a human-readable effectiveness category for UI display.
        /// </summary>
        public static EffectivenessHint GetEffectivenessHint(float combinedMultiplier)
        {
            if (combinedMultiplier == 0f)   return EffectivenessHint.Immune;
            if (combinedMultiplier < 1f)    return EffectivenessHint.NotVeryEffective;
            if (combinedMultiplier > 1f)    return EffectivenessHint.SuperEffective;
            return EffectivenessHint.Normal;
        }

        // ── Chart Construction ────────────────────────────────────────────────

        private static float[,] BuildChart()
        {
            int size = 19; // 18 types + None
            var c = new float[size, size];

            // Default: 1.0 (neutral effectiveness)
            for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                c[i, j] = 1.0f;

            // Shorthand aliases for readability
            const int NRM = (int)PokemonType.Normal;
            const int FIR = (int)PokemonType.Fire;
            const int WAT = (int)PokemonType.Water;
            const int ELE = (int)PokemonType.Electric;
            const int GRS = (int)PokemonType.Grass;
            const int ICE = (int)PokemonType.Ice;
            const int FGT = (int)PokemonType.Fighting;
            const int PSN = (int)PokemonType.Poison;
            const int GRD = (int)PokemonType.Ground;
            const int FLY = (int)PokemonType.Flying;
            const int PSY = (int)PokemonType.Psychic;
            const int BUG = (int)PokemonType.Bug;
            const int RCK = (int)PokemonType.Rock;
            const int GHO = (int)PokemonType.Ghost;
            const int DRG = (int)PokemonType.Dragon;
            const int DRK = (int)PokemonType.Dark;
            const int STL = (int)PokemonType.Steel;
            const int FAI = (int)PokemonType.Fairy;

            // ── Normal attacking ──────────────────────────────────────────────
            c[NRM, RCK] = 0.5f;
            c[NRM, STL] = 0.5f;
            c[NRM, GHO] = 0.0f;

            // ── Fire attacking ────────────────────────────────────────────────
            c[FIR, GRS] = 2.0f; c[FIR, ICE] = 2.0f;
            c[FIR, BUG] = 2.0f; c[FIR, STL] = 2.0f;
            c[FIR, FIR] = 0.5f; c[FIR, WAT] = 0.5f;
            c[FIR, RCK] = 0.5f; c[FIR, DRG] = 0.5f;

            // ── Water attacking ───────────────────────────────────────────────
            c[WAT, FIR] = 2.0f; c[WAT, GRD] = 2.0f; c[WAT, RCK] = 2.0f;
            c[WAT, WAT] = 0.5f; c[WAT, GRS] = 0.5f; c[WAT, DRG] = 0.5f;

            // ── Electric attacking ────────────────────────────────────────────
            c[ELE, WAT] = 2.0f; c[ELE, FLY] = 2.0f;
            c[ELE, ELE] = 0.5f; c[ELE, GRS] = 0.5f; c[ELE, DRG] = 0.5f;
            c[ELE, GRD] = 0.0f;

            // ── Grass attacking ───────────────────────────────────────────────
            c[GRS, WAT] = 2.0f; c[GRS, GRD] = 2.0f; c[GRS, RCK] = 2.0f;
            c[GRS, FIR] = 0.5f; c[GRS, GRS] = 0.5f; c[GRS, PSN] = 0.5f;
            c[GRS, FLY] = 0.5f; c[GRS, BUG] = 0.5f; c[GRS, DRG] = 0.5f;
            c[GRS, STL] = 0.5f;

            // ── Ice attacking ─────────────────────────────────────────────────
            c[ICE, GRS] = 2.0f; c[ICE, GRD] = 2.0f;
            c[ICE, FLY] = 2.0f; c[ICE, DRG] = 2.0f;
            c[ICE, FIR] = 0.5f; c[ICE, WAT] = 0.5f;
            c[ICE, ICE] = 0.5f; c[ICE, STL] = 0.5f;

            // ── Fighting attacking ────────────────────────────────────────────
            c[FGT, NRM] = 2.0f; c[FGT, ICE] = 2.0f; c[FGT, RCK] = 2.0f;
            c[FGT, DRK] = 2.0f; c[FGT, STL] = 2.0f;
            c[FGT, PSN] = 0.5f; c[FGT, FLY] = 0.5f; c[FGT, PSY] = 0.5f;
            c[FGT, BUG] = 0.5f; c[FGT, FAI] = 0.5f;
            c[FGT, GHO] = 0.0f;

            // ── Poison attacking ──────────────────────────────────────────────
            c[PSN, GRS] = 2.0f; c[PSN, FAI] = 2.0f;
            c[PSN, PSN] = 0.5f; c[PSN, GRD] = 0.5f;
            c[PSN, RCK] = 0.5f; c[PSN, GHO] = 0.5f;
            c[PSN, STL] = 0.0f;

            // ── Ground attacking ──────────────────────────────────────────────
            c[GRD, FIR] = 2.0f; c[GRD, ELE] = 2.0f; c[GRD, PSN] = 2.0f;
            c[GRD, RCK] = 2.0f; c[GRD, STL] = 2.0f;
            c[GRD, GRS] = 0.5f; c[GRD, BUG] = 0.5f;
            c[GRD, FLY] = 0.0f;

            // ── Flying attacking ──────────────────────────────────────────────
            c[FLY, GRS] = 2.0f; c[FLY, FGT] = 2.0f; c[FLY, BUG] = 2.0f;
            c[FLY, ELE] = 0.5f; c[FLY, RCK] = 0.5f; c[FLY, STL] = 0.5f;

            // ── Psychic attacking ─────────────────────────────────────────────
            c[PSY, FGT] = 2.0f; c[PSY, PSN] = 2.0f;
            c[PSY, PSY] = 0.5f; c[PSY, STL] = 0.5f;
            c[PSY, DRK] = 0.0f;

            // ── Bug attacking ─────────────────────────────────────────────────
            c[BUG, GRS] = 2.0f; c[BUG, PSY] = 2.0f; c[BUG, DRK] = 2.0f;
            c[BUG, FIR] = 0.5f; c[BUG, FGT] = 0.5f; c[BUG, FLY] = 0.5f;
            c[BUG, GHO] = 0.5f; c[BUG, STL] = 0.5f; c[BUG, FAI] = 0.5f;

            // ── Rock attacking ────────────────────────────────────────────────
            c[RCK, FIR] = 2.0f; c[RCK, ICE] = 2.0f;
            c[RCK, FLY] = 2.0f; c[RCK, BUG] = 2.0f;
            c[RCK, FGT] = 0.5f; c[RCK, GRD] = 0.5f; c[RCK, STL] = 0.5f;

            // ── Ghost attacking ───────────────────────────────────────────────
            c[GHO, GHO] = 2.0f; c[GHO, PSY] = 2.0f;
            c[GHO, NRM] = 0.0f;
            c[GHO, DRK] = 0.5f;

            // ── Dragon attacking ──────────────────────────────────────────────
            c[DRG, DRG] = 2.0f;
            c[DRG, STL] = 0.5f;
            c[DRG, FAI] = 0.0f;

            // ── Dark attacking ────────────────────────────────────────────────
            c[DRK, PSY] = 2.0f; c[DRK, GHO] = 2.0f;
            c[DRK, FGT] = 0.5f; c[DRK, DRK] = 0.5f; c[DRK, FAI] = 0.5f;

            // ── Steel attacking ───────────────────────────────────────────────
            c[STL, ICE] = 2.0f; c[STL, RCK] = 2.0f; c[STL, FAI] = 2.0f;
            c[STL, STL] = 0.5f; c[STL, FIR] = 0.5f;
            c[STL, WAT] = 0.5f; c[STL, ELE] = 0.5f;

            // ── Fairy attacking ───────────────────────────────────────────────
            c[FAI, FGT] = 2.0f; c[FAI, DRG] = 2.0f; c[FAI, DRK] = 2.0f;
            c[FAI, FIR] = 0.5f; c[FAI, PSN] = 0.5f; c[FAI, STL] = 0.5f;

            return c;
        }

        // ── Debug Utility ─────────────────────────────────────────────────────

        /// <summary>
        /// Logs the full row of effectiveness values for a given attacker type.
        /// Useful for playtesting and balance review.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void LogAttackerRow(PokemonType attacker)
        {
            var sb = new StringBuilder($"[TypeTable] {attacker} attacking:\n");
            foreach (PokemonType defender in System.Enum.GetValues(typeof(PokemonType)))
            {
                if (defender == PokemonType.None) continue;
                float m = GetMultiplier(attacker, defender);
                if (!Mathf.Approximately(m, 1f))
                    sb.AppendLine($"  vs {defender,-16}: {m}×");
            }
            Debug.Log(sb.ToString());
        }
    }

    // ==========================================================================
    // Supporting enum for UI effectiveness display
    // ==========================================================================

    public enum EffectivenessHint
    {
        Immune,             // 0×
        NotVeryEffective,   // 0.25× or 0.5×
        Normal,             // 1×
        SuperEffective      // 2× or 4×
    }
}
