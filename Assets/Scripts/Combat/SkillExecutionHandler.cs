using System;
using System.Collections;
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
        [Header("VFX / Audio")]
        [Tooltip("Global fallback height offset when a skill's VFXOffset is zero.")]
        [SerializeField] private float _vfxHeightOffset = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _verboseLogging = true;

        // ── Services ──────────────────────────────────────────────────────────

        private UnitRegistry     _unitRegistry;
        private SkillRegistry    _skillRegistry;
        private WorldGridManager _gridManager;
        private GameStateManager _stateManager;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _unitRegistry  = ServiceLocator.Get<UnitRegistry>();
            _skillRegistry = ServiceLocator.Get<SkillRegistry>();
            _gridManager   = ServiceLocator.Get<WorldGridManager>();
            _stateManager  = ServiceLocator.Get<GameStateManager>();

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

            // ── Re-validate ────────────────────────────────────────────────────
            if (!caster.IsAlive) return;
            if (caster.RuntimeState.IsOnCooldown(skill.SkillId)) return;

            bool inCombat = _stateManager?.IsInCombat ?? false;

            // AP is only relevant in combat — overworld use is free
            if (inCombat && !caster.RuntimeState.CanAfford(skill.APCost)) return;

            // ── Spend resources ─────────────────────────────────────────────────
            if (inCombat)
            {
                caster.RuntimeState.TrySpendAP(skill.APCost);
                caster.RuntimeState.HasActedThisTurn = true;
            }
            if (skill.Cooldown > 0)
                caster.RuntimeState.SetCooldown(skill.SkillId, skill.Cooldown);

            // ── Face target before executing ───────────────────────────────────
            if (_gridManager != null)
            {
                var targetWorld = _gridManager.GetWorldPosition(evt.TargetCell);
                var dir = targetWorld - caster.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    caster.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            // ── Gather targets and resolve ─────────────────────────────────────
            var targets  = GatherTargets(skill, caster, evt.TargetCell, evt.TargetUnitId);
            var hitCells = GatherHitCells(skill, evt.TargetCell);

            // Apply surface effects once for all hit cells
            SkillResolver.ResolveGridEffects(skill, hitCells);

            int resolvedCount = 0;
            foreach (var target in targets)
            {
                SkillResolver.Resolve(skill, caster, target);
                resolvedCount++;
            }

            if (_verboseLogging)
                Debug.Log($"[SkillExecutionHandler] {caster.DisplayName} used {skill.SkillName} " +
                          $"— {resolvedCount} target(s). AP remaining: {caster.RuntimeState.CurrentAP}");

            // Spawn VFX + play audio at target cell
            if (_gridManager != null)
            {
                var worldPos = _gridManager.GetWorldPosition(evt.TargetCell);
                SpawnVFXAndAudio(skill, worldPos);
            }

            GameEventBus.Publish(new ActionExecutedEvent
            {
                ActorUnitId = caster.UnitId,
                ActionName  = skill.SkillName,
                SkillId     = skill.SkillId,
                APSpent     = skill.APCost
            });
        }

        // ── Direct Execution (no event / no registry lookup) ─────────────────

        /// <summary>
        /// Execute a skill directly against a single target unit.
        /// Used by BasicAttackController so the basic attack goes through the
        /// same AP / cooldown / VFX / event pipeline as every other skill.
        /// </summary>
        public void ExecuteDirect(SkillDefinition skill, BaseUnit caster, BaseUnit target)
        {
            if (skill == null || caster == null || target == null) return;
            if (!caster.IsAlive || !target.IsAlive) return;

            bool inCombat = _stateManager?.IsInCombat ?? false;

            if (inCombat)
            {
                if (!caster.RuntimeState.CanAfford(skill.APCost)) return;
                if (caster.RuntimeState.IsOnCooldown(skill.SkillId)) return;

                // Use ActionPointController so APChangedEvent fires → UI refreshes.
                var apCtrl = caster.GetComponent<ActionPointController>();
                if (apCtrl != null)
                {
                    if (!apCtrl.SpendAP(skill.APCost)) return;
                }
                else
                {
                    if (!caster.RuntimeState.TrySpendAP(skill.APCost)) return;
                }

                caster.RuntimeState.HasActedThisTurn = true;
            }

            if (skill.Cooldown > 0)
                caster.RuntimeState.SetCooldown(skill.SkillId, skill.Cooldown);

            // Face caster toward target
            var dir = target.transform.position - caster.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                caster.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            // Resolve damage / effects
            SkillResolver.Resolve(skill, caster, target);

            // VFX + audio at target position
            SpawnVFXAndAudio(skill, target.transform.position);

            GameEventBus.Publish(new ActionExecutedEvent
            {
                ActorUnitId = caster.UnitId,
                ActionName  = skill.SkillName,
                SkillId     = skill.SkillId,
                APSpent     = skill.APCost
            });

            if (_verboseLogging)
                Debug.Log($"[SkillExecutionHandler] ExecuteDirect: {caster.DisplayName} → " +
                          $"{skill.SkillName} → {target.DisplayName}. AP left: {caster.RuntimeState.CurrentAP}");
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

        // ── Hit Cell Collection ───────────────────────────────────────────────

        /// <summary>
        /// Returns all GridCells hit by the skill. Used to apply terrain surface effects.
        /// For single-target skills this is just the target cell; for AoE it's the full radius.
        /// </summary>
        private List<Grid.GridCell> GatherHitCells(SkillDefinition skill, Vector2Int targetCell)
        {
            var cells = new List<Grid.GridCell>();
            if (_gridManager == null) return cells;

            if (skill.AoERadius > 0)
            {
                var positions = GridUtility.GetCellsInCircle(targetCell, skill.AoERadius);
                foreach (var pos in positions)
                {
                    var cell = _gridManager.GetCell(pos);
                    if (cell != null) cells.Add(cell);
                }
            }
            else
            {
                var cell = _gridManager.GetCell(targetCell);
                if (cell != null) cells.Add(cell);
            }

            return cells;
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

        // ── VFX + Audio ───────────────────────────────────────────────────────

        /// <summary>
        /// Spawns the skill's VFX prefab (if any) at <paramref name="basePos"/> plus
        /// the skill's VFXOffset, and plays the sound effect for up to SoundEffectDuration
        /// seconds (0 = full clip).
        /// </summary>
        private void SpawnVFXAndAudio(SkillDefinition skill, Vector3 basePos)
        {
            // Use skill's own VFXOffset; fall back to global height offset when offset is zero
            Vector3 offset  = skill.VFXOffset != Vector3.zero
                ? skill.VFXOffset
                : new Vector3(0f, _vfxHeightOffset, 0f);
            Vector3 spawnPos = basePos + offset;

            if (skill.VFXPrefab != null)
                UnityEngine.Object.Instantiate(skill.VFXPrefab, spawnPos, Quaternion.identity);

            if (skill.SoundEffect != null)
            {
                if (skill.SoundEffectDuration <= 0f || skill.SoundEffectDuration >= skill.SoundEffect.length)
                {
                    AudioSource.PlayClipAtPoint(skill.SoundEffect, spawnPos);
                }
                else
                {
                    StartCoroutine(PlayClipForDuration(skill.SoundEffect, spawnPos, skill.SoundEffectDuration));
                }
            }
        }

        private static IEnumerator PlayClipForDuration(AudioClip clip, Vector3 pos, float duration)
        {
            var go  = new GameObject("SkillAudio_Temp");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip        = clip;
            src.spatialBlend = 1f;
            src.Play();

            yield return new WaitForSeconds(duration);

            src.Stop();
            Destroy(go);
        }

        // ── Target Resolution ─────────────────────────────────────────────────

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
