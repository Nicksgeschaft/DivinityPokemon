using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Combat;
using PokemonAdventure.Grid;
using PokemonAdventure.Movement;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.AI
{
    // ==========================================================================
    // AI Controller
    // Concrete UnitController implementation for enemy/AI-driven units.
    // Reads EnemyArchetypeDefinition to decide actions each turn.
    //
    // Current behaviour (MVP):
    //   1. Find target using AITargetSelector.
    //   2. Move toward target if not in range.
    //   3. Use best available skill, or basic attack.
    //   4. End turn when AP is exhausted.
    //
    // TODO: Replace with a proper behaviour tree or utility AI system.
    // ==========================================================================

    [RequireComponent(typeof(EnemyUnit))]
    public class AIController : UnitController
    {
        [Header("AI Configuration")]
        [Tooltip("Archetype data that governs this unit's decision-making.")]
        [SerializeField] private EnemyArchetypeDefinition _archetype;

        [Tooltip("Seconds of artificial delay between actions (for readability).")]
        [SerializeField] private float _actionDelay = 0.5f;

        private WorldGridManager      _gridManager;
        private CombatStateController _combatController;
        private AITargetSelector      _targetSelector;
        private Coroutine             _aiRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _targetSelector = new AITargetSelector();

            // Auto-read archetype from EnemyUnit if not explicitly set in Inspector
            if (_archetype == null)
                _archetype = GetComponent<EnemyUnit>()?.Archetype;
        }

        private void Start()
        {
            _gridManager = ServiceLocator.Get<WorldGridManager>();
            // CombatStateController is optional — not present in every scene.
            // Resolved lazily in GetCombatController() when the turn actually starts.
        }

        private CombatStateController GetCombatController()
        {
            if (_combatController != null) return _combatController;
            ServiceLocator.TryGet(out _combatController);
            return _combatController;
        }

        // ── Turn Hooks ────────────────────────────────────────────────────────

        protected override void OnTurnStarted()
        {
            _aiRoutine = StartCoroutine(ExecuteAITurn());
        }

        protected override void OnTurnEnded()
        {
            if (_aiRoutine != null)
            {
                StopCoroutine(_aiRoutine);
                _aiRoutine = null;
            }
        }

        // ── AI Turn Execution ─────────────────────────────────────────────────

        private IEnumerator ExecuteAITurn()
        {
            yield return new WaitForSeconds(_actionDelay);

            var state     = Unit.RuntimeState;
            var encounter = GetCombatController()?.ActiveEncounter;
            if (encounter == null) { RequestEndTurn(); yield break; }

            var targets = _targetSelector.GetPrioritisedTargets(
                Unit, encounter, _archetype?.TargetPriority ?? AITargetPriority.LowestHP);

            if (targets.Count == 0) { RequestEndTurn(); yield break; }

            var primaryTarget = targets[0];

            // ── Step 1: Move toward target up to Initiative-based movement range ──
            if (_gridManager != null)
            {
                yield return StartCoroutine(MoveTowardTarget(primaryTarget));
                yield return new WaitForSeconds(_actionDelay);
            }

            // ── Step 2: Use a skill if target is in range, else end turn ──────
            if (_gridManager != null && state.CanAfford(1))
            {
                int dist = GridUtility.ManhattanDistance(
                    state.GridPosition, primaryTarget.RuntimeState.GridPosition);

                var skill = GetUsableSkillInRange(dist);

                if (skill != null)
                {
                    var handler = GetComponent<Combat.SkillExecutionHandler>() ??
                                  FindAnyObjectByType<Combat.SkillExecutionHandler>();
                    if (handler != null)
                    {
                        handler.ExecuteDirect(skill, Unit, primaryTarget);
                        yield return new WaitForSeconds(_actionDelay);
                    }
                }
                else if (dist <= 1)
                {
                    ExecuteBasicAttack(primaryTarget);
                    state.TrySpendAP(1);
                    yield return new WaitForSeconds(_actionDelay);
                }
            }

            RequestEndTurn();
        }

        private SkillDefinition GetUsableSkillInRange(int dist)
        {
            if (_archetype == null) return null;
            foreach (var skill in _archetype.StartingSkills)
            {
                if (skill == null) continue;
                if (!Unit.RuntimeState.IsOnCooldown(skill.SkillId) &&
                    dist <= skill.Range &&
                    Unit.RuntimeState.CanAfford(skill.APCost))
                    return skill;
            }
            return null;
        }

        private IEnumerator MoveTowardTarget(BaseUnit target)
        {
            if (_gridManager == null) yield break;

            // Get all cells reachable within Initiative-based movement tier
            var reachable = GridMovementHandler.GetMovementRange(Unit, _gridManager);
            if (reachable.Count == 0) yield break;

            // Pick the reachable cell closest to the target
            var targetPos = target.RuntimeState.GridPosition;
            Vector2Int? bestCell = null;
            int bestDist = int.MaxValue;

            foreach (var pos in reachable)
            {
                if (pos == Unit.RuntimeState.GridPosition) continue;
                var cell = _gridManager.GetCell(pos);
                if (cell == null || !cell.IsPassable || cell.OccupyingUnit != null) continue;

                int dist = GridUtility.ManhattanDistance(pos, targetPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCell = pos;
                }
            }

            if (!bestCell.HasValue) yield break;

            var request = GridMovementHandler.BuildRequest(Unit, bestCell.Value, _gridManager);
            if (request.IsValid)
                yield return StartCoroutine(GridMovementHandler.ExecuteMovement(request, _gridManager));
        }

        private void ExecuteBasicAttack(BaseUnit target)
        {
            // Face the target before attacking
            var dir = target.transform.position - Unit.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                Unit.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            float rawDamage  = Unit.Stats.EffectiveAttack;

            // Snapshot pre-damage values so DamageDealtEvent carries correct deltas
            float hpBefore   = target.RuntimeState.CurrentHP;
            float physBefore = target.RuntimeState.CurrentPhysicalArmor;
            float specBefore = target.RuntimeState.CurrentSpecialArmor;

            target.TakeDamage(rawDamage, Data.DamageType.Physical, Unit);

            float armorAbsorbed = (physBefore - target.RuntimeState.CurrentPhysicalArmor)
                                + (specBefore - target.RuntimeState.CurrentSpecialArmor);
            float hpDamage      = hpBefore - target.RuntimeState.CurrentHP;

            GameEventBus.Publish(new DamageDealtEvent
            {
                AttackerUnitId = Unit.UnitId,
                DefenderUnitId = target.UnitId,
                SkillId        = "BasicAttack",
                FinalDamage    = rawDamage,
                ArmorAbsorbed  = armorAbsorbed,
                HPDamage       = hpDamage,
                Effectiveness  = Combat.EffectivenessCategory.Normal
            });

            GameEventBus.Publish(new ActionExecutedEvent
            {
                ActorUnitId = Unit.UnitId,
                ActionName  = "BasicAttack",
                APSpent     = 1
            });

            Debug.Log($"[AIController] {Unit.DisplayName} → {target.DisplayName}: " +
                      $"{hpDamage:F0} HP dmg, {armorAbsorbed:F0} armor absorbed.");
        }
    }

    // ==========================================================================
    // Enemy Unit
    // Concrete BaseUnit for AI-controlled enemies. Minimal subclass.
    // ==========================================================================

    public class EnemyUnit : BaseUnit
    {
        [Header("Enemy Configuration")]
        [SerializeField] private EnemyArchetypeDefinition _archetype;

        public EnemyArchetypeDefinition Archetype     => _archetype;
        public PokemonType              PrimaryType   => _archetype?.PrimaryType   ?? PokemonType.Normal;
        public PokemonType              SecondaryType => _archetype?.SecondaryType ?? PokemonType.None;

        protected override void Awake()
        {
            SetFaction(UnitFaction.Hostile);

            if (_archetype != null)
            {
                _stats = _archetype.BaseStats;
                SetDisplayName(_archetype.EnemyName);
            }

            base.Awake();
        }

        /// <summary>
        /// Applies an archetype at runtime (used by EnemyPrefabController after AddComponent).
        /// Re-initializes RuntimeState so HP/armor reflect the archetype's stats.
        /// </summary>
        public void Initialize(EnemyArchetypeDefinition archetype)
        {
            _archetype = archetype;
            if (archetype == null) return;
            _stats = archetype.BaseStats;
            SetFaction(UnitFaction.Hostile);
            SetDisplayName(archetype.EnemyName);
            RuntimeState?.Initialize(_stats);
            if (RuntimeState != null)
                RuntimeState.MaxMoveRange = archetype.MovementRange;
        }
    }
}
