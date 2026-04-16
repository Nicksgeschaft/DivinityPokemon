using System.Collections.Generic;
using UnityEngine;

namespace PokemonAdventure.ScriptableObjects
{
    // ==========================================================================
    // Skill Registry (ScriptableObject)
    // Maps SkillId → SkillDefinition at runtime.
    //
    // Setup:
    //   1. Create via: Assets → Create → PokemonAdventure → SkillRegistry
    //   2. Drag all SkillDefinition assets into the _skills list.
    //   3. Assign this asset to GameBootstrapper._skillRegistry in the Inspector.
    //
    // The dictionary is built lazily on first access and invalidated on
    // OnValidate() so in-Editor changes are reflected without restarting.
    // ==========================================================================

    [CreateAssetMenu(
        menuName = "PokemonAdventure/SkillRegistry",
        fileName = "SkillRegistry",
        order    = 10)]
    public class SkillRegistry : ScriptableObject
    {
        [Tooltip("All SkillDefinitions available to be used at runtime. " +
                 "Every skill that can be equipped or triggered must be listed here.")]
        [SerializeField] private List<SkillDefinition> _skills = new();

        private Dictionary<string, SkillDefinition> _lookup;

        // ── Lookup ────────────────────────────────────────────────────────────

        public bool TryGet(string skillId, out SkillDefinition skill)
        {
            skill = null;
            if (string.IsNullOrEmpty(skillId)) return false;
            BuildIfNeeded();
            return _lookup.TryGetValue(skillId, out skill);
        }

        /// <summary>Returns the skill or null if not registered.</summary>
        public SkillDefinition Get(string skillId) =>
            TryGet(skillId, out var s) ? s : null;

        public IReadOnlyList<SkillDefinition> All => _skills;

        // ── Internal ──────────────────────────────────────────────────────────

        private void BuildIfNeeded()
        {
            if (_lookup != null) return;

            _lookup = new Dictionary<string, SkillDefinition>(_skills.Count);
            foreach (var skill in _skills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.SkillId))
                {
                    Debug.LogWarning("[SkillRegistry] Null or ID-less entry skipped.");
                    continue;
                }
                if (!_lookup.TryAdd(skill.SkillId, skill))
                    Debug.LogWarning($"[SkillRegistry] Duplicate SkillId '{skill.SkillId}' — " +
                                     "second entry ignored.");
            }
        }

        // Invalidate cache when the asset is modified in the Editor
        private void OnValidate() => _lookup = null;
    }
}
