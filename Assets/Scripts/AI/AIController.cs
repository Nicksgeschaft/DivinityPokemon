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

            var state    = Unit.RuntimeState;
            var encounter = GetCombatController()?.ActiveEncounter;
            if (encounter == null) { RequestEndTurn(); yield break; }

            // Gather potential targets
            var targets = _targetSelector.GetPrioritisedTargets(
                Unit, encounter, _archetype?.TargetPriority
                    ?? AITargetPriority.LowestHP);

            if (targets.Count == 0) { RequestEndTurn(); yield break; }

            var primaryTarget = targets[0];

            // Spend AP: try to act until AP runs out or no valid actions remain
            int maxIterations = RuntimeUnitState.MaxAPCap; // Safety limit
            for (int i = 0; i < maxIterations && state.CurrentAP > 0; i++)
            {
                bool acted = false;

                // 1. Try to use a skill
                // TODO: Implement skill selection via archetype.SkillPreference
                // For now, attempt a basic attack if target is adjacent
                if (_gridManager != null)
                {
                    int dist = GridUtility.ManhattanDistance(
                        Unit.RuntimeState.GridPosition,
                        primaryTarget.RuntimeState.GridPosition);

                    if (dist <= 1 && state.CanAfford(1))
                    {
                        ExecuteBasicAttack(primaryTarget);
                        state.TrySpendAP(1);
                        acted = true;
                    }
                    // 2. Otherwise, move toward target
                    else if (!state.HasMovedThisTurn && _gridManager != null)
                    {
                        yield return StartCoroutine(MoveTowardTarget(primaryTarget));
                        acted = true;
                    }
                }

                if (!acted) break;
                yield return new WaitForSeconds(_actionDelay);
            }

            RequestEndTurn();
        }

        private IEnumerator MoveTowardTarget(BaseUnit target)
        {
            if (_gridManager == null) yield break;

            // Find a cell adjacent to the target
            var targetPos = target.RuntimeState.GridPosition;
            Vector2Int? bestMoveTarget = null;
            int bestDist = int.MaxValue;

            foreach (var neighbour in _gridManager.GetNeighbours(targetPos, false))
            {
                if (!neighbour.IsPassable) continue;
                int dist = GridUtility.ManhattanDistance(
                    Unit.RuntimeState.GridPosition, neighbour.GridPosition);
                if (dist < bestDist)
                {
                    bestDist       = dist;
                    bestMoveTarget = neighbour.GridPosition;
                }
            }

            if (!bestMoveTarget.HasValue) yield break;

            var request = GridMovementHandler.BuildRequest(Unit, bestMoveTarget.Value, _gridManager);
            if (request.IsValid)
                yield return StartCoroutine(GridMovementHandler.ExecuteMovement(request, _gridManager));
        }

        private void ExecuteBasicAttack(BaseUnit target)
        {
            // TODO: Replace with proper SkillResolver once SkillDefinition resolution is implemented
            float rawDamage = Unit.Stats.EffectiveAttack;
            target.TakeDamage(rawDamage, Data.DamageType.Physical, Unit);

            Debug.Log($"[AIController] {Unit.DisplayName} attacks {target.DisplayName} " +
                      $"for {rawDamage:F0} Physical damage.");

            GameEventBus.Publish(new ActionExecutedEvent
            {
                ActorUnitId = Unit.UnitId,
                ActionName  = "BasicAttack",
                APSpent     = 1
            });
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
        }
    }
}
