using System;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Skill Execution Handler
    // Bridges the targeting UI and the damage pipeline.
    //
    // Flow:
    //   SkillTargetingConfirmedEvent
    //     → resolve SkillDefinition (SkillRegistry)
    //     → resolve caster BaseUnit (UnitRegistry)
    //     → re-validate AP + cooldown + alive
    //     → spend AP, set cooldown, mark HasActedThisTurn
    //     → GatherTargets() — single unit OR circle AoE OR faction-wide
    //     → SkillResolver.Resolve(skill, caster, target) per target
    //     → publish ActionExecutedEvent
    //
    // AoE skills (CircleAoE, GroundTarget with AoERadius > 0) hit every live
    // unit occupying cells within AoERadius of the confirmed TargetCell.
    //
    // Attach to the same GameObject as CombatManager, or any scene object that
    // persists for the duration of combat.
    // ==========================================================================

    public class SkillExecutionHandler : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _verboseLogging = true;

        // ── Services ──────────────────────────────────────────────────────────

        private UnitRegistry     _unitRegistry;
        private SkillRegistry    _skillRegistry;
        private WorldGridManager _gridManager;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _unitRegistry  = ServiceLocator.Get<UnitRegistry>();
            _skillRegistry = ServiceLocator.Get<SkillRegistry>();
            _gridManager   = ServiceLocator.Get<WorldGridManager>();

            if (_unitRegistry  == null) Debug.LogError("[SkillExecutionHandler] UnitRegistry not registered.");
            if (_skillRegistry == null) Debug.LogError("[SkillExecutionHandler] SkillRegistry not registered.");

            GameEventBus.Subscribe<SkillTargetingConfirmedEvent>(OnSkillConfirmed);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<SkillTargetingConfirmedEvent>(OnSkillConfirmed);
        }

        // ── Event Handler ─────────────────────────────────────────────────────

        private void OnSkillConfirmed(SkillTargetingConfirmedEvent evt)
        {
            if (_unitRegistry == null || _skillRegistry == null) return;

            // ── Resolve caster ─────────────────────────────────────────────────
            if (!_unitRegistry.TryGet(evt.CasterUnitId, out var caster))
            {
                Debug.LogWarning($"[SkillExecutionHandler] Caster '{evt.CasterUnitId}' not found.");
                return;
            }

            // ── Resolve skill ──────────────────────────────────────────────────
            if (!_skillRegistry.TryGet(evt.SkillId, out var skill))
            {
                Debug.LogWarning($"[SkillExecutionHandler] Skill '{evt.SkillId}' not in registry.");
                return;
            }

            // ── Re-validate (state may have changed since targeting began) ─────
            if (!caster.IsAlive)                                 return;
            if (!caster.RuntimeState.CanAfford(skill.APCost))   return;
            if (caster.RuntimeState.IsOnCooldown(skill.SkillId)) return;

            // ── Spend resources (before resolving so UI reflects immediately) ───
            caster.RuntimeState.TrySpendAP(skill.APCost);
            if (skill.Cooldown > 0)
                caster.RuntimeState.SetCooldown(skill.SkillId, skill.Cooldown);
            caster.RuntimeState.HasActedThisTurn = true;

            // ── Gather targets and resolve ─────────────────────────────────────
            var targets = GatherTargets(skill, caster, evt.TargetCell, evt.TargetUnitId);

            int resolvedCount = 0;
            foreach (var target in targets)
            {
                SkillResolver.Resolve(skill, caster, target);
                resolvedCount++;
            }

            if (_verboseLogging)
                Debug.Log($"[SkillExecutionHandler] {caster.DisplayName} used {skill.SkillName} " +
                          $"— {resolvedCount} target(s). AP remaining: {caster.RuntimeState.CurrentAP}");

            GameEventBus.Publish(new ActionExecutedEvent
            {
                ActorUnitId = caster.UnitId,
                ActionName  = skill.SkillName,
                APSpent     = skill.APCost
            });
        }

        // ── Target Resolution ─────────────────────────────────────────────────

        /// <summary>
        /// Returns all live IUnit targets for this skill use.
        /// </summary>
        private List<IUnit> GatherTargets(
            SkillDefinition skill,
            BaseUnit        caster,
            Vector2Int      targetCell,
            string          targetUnitId)
        {
            var targets = new List<IUnit>();

            switch (skill.Targeting)
            {
                // ── Faction-wide auto-targets ──────────────────────────────────
                case TargetingType.AllEnemies:
                    AddMatching(targets, u => u.Faction != caster.Faction && u.IsAlive);
                    break;

                case TargetingType.AllAllies:
                    // Excludes self — allies are same faction, not the caster
                    AddMatching(targets, u =>
                        u.Faction == caster.Faction &&
                        u.IsAlive &&
                        u.UnitId != caster.UnitId);
                    break;

                case TargetingType.All:
                    AddMatching(targets, u => u.IsAlive && u.UnitId != caster.UnitId);
                    break;

                case TargetingType.Self:
                    targets.Add(caster);
                    break;

                // ── Circle AoE: hit all units in radius around target cell ─────
                case TargetingType.CircleAoE:
                    AddUnitsInCircle(targets, targetCell, skill.AoERadius);
                    break;

                // ── Ground target: if AoERadius > 0 treat as splash ───────────
                case TargetingType.GroundTarget:
                    if (skill.AoERadius > 0)
                        AddUnitsInCircle(targets, targetCell, skill.AoERadius);
                    else
                    {
                        var groundHit = ResolveCell(targetCell, targetUnitId);
                        if (groundHit != null) targets.Add(groundHit);
                    }
                    break;

                // ── Single-unit targeting (enemy, ally, any) ──────────────────
                default:
                    var single = ResolveCell(targetCell, targetUnitId);
                    if (single != null) targets.Add(single);
                    break;
            }

            return targets;
        }

        /// <summary>Add every unit on cells within <paramref name="radius"/> of <paramref name="center"/>.</summary>
        private void AddUnitsInCircle(List<IUnit> targets, Vector2Int center, int radius)
        {
            if (_gridManager == null) return;

            var cells = GridUtility.GetCellsInCircle(center, radius);
            foreach (var pos in cells)
            {
                var cell = _gridManager.GetCell(pos);
                if (cell?.OccupyingUnit != null && cell.OccupyingUnit.IsAlive)
                    targets.Add(cell.OccupyingUnit);
            }
        }

        /// <summary>Add all registered live units matching <paramref name="predicate"/>.</summary>
        private void AddMatching(List<IUnit> targets, Func<IUnit, bool> predicate)
        {
            foreach (var kv in _unitRegistry.All)
            {
                if (kv.Value != null && predicate(kv.Value))
                    targets.Add(kv.Value);
            }
        }

        /// <summary>
        /// Resolve a single-cell target. Prefers the explicit TargetUnitId;
        /// falls back to whoever is occupying the cell.
        /// </summary>
        private IUnit ResolveCell(Vector2Int cell, string targetUnitId)
        {
            if (!string.IsNullOrEmpty(targetUnitId) &&
                _unitRegistry.TryGet(targetUnitId, out var byId))
                return byId.IsAlive ? byId : null;

            if (_gridManager == null) return null;
            var gridCell = _gridManager.GetCell(cell);
            return gridCell?.OccupyingUnit?.IsAlive == true ? gridCell.OccupyingUnit : null;
        }
    }
}
