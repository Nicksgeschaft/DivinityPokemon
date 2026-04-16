using System.Collections.Generic;
using System.Linq;
using PokemonAdventure.Combat;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.AI
{
    // ==========================================================================
    // AI Target Selector
    // Provides sorted target lists based on AITargetPriority strategies.
    // Pure logic class — no MonoBehaviour, no state.
    // ==========================================================================

    public class AITargetSelector
    {
        /// <summary>
        /// Returns all living enemy units sorted by the given priority strategy.
        /// "Enemy" means units NOT of the same faction as the selector unit.
        /// </summary>
        public List<BaseUnit> GetPrioritisedTargets(
            BaseUnit selectorUnit,
            CombatEncounter encounter,
            AITargetPriority priority)
        {
            var enemies = encounter.Participants
                .Where(u => u.IsAlive && u.Faction != selectorUnit.Faction)
                .ToList();

            if (enemies.Count == 0)
                return enemies;

            return priority switch
            {
                AITargetPriority.LowestHP =>
                    enemies.OrderBy(u => u.RuntimeState.CurrentHP).ToList(),

                AITargetPriority.HighestHP =>
                    enemies.OrderByDescending(u => u.RuntimeState.CurrentHP).ToList(),

                AITargetPriority.LowestArmor =>
                    enemies.OrderBy(u =>
                        u.RuntimeState.CurrentPhysicalArmor +
                        u.RuntimeState.CurrentSpecialArmor).ToList(),

                AITargetPriority.HighestThreat =>
                    // Approximation: highest effective attack = most threatening
                    enemies.OrderByDescending(u => u.Stats.EffectiveAttack +
                                                   u.Stats.EffectiveSpecialAttack).ToList(),

                AITargetPriority.Nearest =>
                    enemies.OrderBy(u => GridUtility.ManhattanDistance(
                        selectorUnit.RuntimeState.GridPosition,
                        u.RuntimeState.GridPosition)).ToList(),

                AITargetPriority.Random =>
                    enemies.OrderBy(_ => UnityEngine.Random.value).ToList(),

                _ =>
                    enemies
            };
        }

        /// <summary>
        /// Returns the single best target based on priority, or null if none available.
        /// </summary>
        public BaseUnit GetBestTarget(
            BaseUnit selectorUnit,
            CombatEncounter encounter,
            AITargetPriority priority)
        {
            var targets = GetPrioritisedTargets(selectorUnit, encounter, priority);
            return targets.Count > 0 ? targets[0] : null;
        }
    }
}
