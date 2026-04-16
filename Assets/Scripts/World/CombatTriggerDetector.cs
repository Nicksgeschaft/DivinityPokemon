using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Combat;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.World
{
    // ==========================================================================
    // Combat Trigger Detector
    // Replaces SightTrigger. Attach to every hostile unit in the overworld.
    //
    // No physics colliders needed. OverworldMovementController polls all
    // detectors after each grid step and calls FireCombat() when the player
    // has walked into range.
    //
    // HasFired prevents re-triggering during a single approach. It resets when
    // the game returns to Overworld so the enemy can trigger again next time.
    //
    // Requires: a BaseUnit (EnemyUnit) on the same GameObject.
    // ==========================================================================

    public class CombatTriggerDetector : MonoBehaviour
    {
        [Header("Sight")]
        [Tooltip("Sight radius in world units. Match to unit VisionRange × cellSize.")]
        [SerializeField] private float _sightRadius = 6f;

        [Tooltip("Gather all hostile units within this world-unit radius into the encounter.")]
        [SerializeField] private float _allyGatherRadius = 10f;

        public float SightRadius => _sightRadius;

        /// <summary>
        /// True once this detector has initiated an encounter this approach.
        /// Prevents double-triggering. Resets when game returns to Overworld.
        /// </summary>
        public bool HasFired { get; private set; }

        // ── Services ──────────────────────────────────────────────────────────

        private BaseUnit              _ownerUnit;
        private GameStateManager      _stateManager;
        private CombatStateController _combatController;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _ownerUnit = GetComponent<BaseUnit>();
        }

        private void Start()
        {
            _stateManager     = ServiceLocator.Get<GameStateManager>();
            _combatController = ServiceLocator.Get<CombatStateController>();

            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the player's world position is within this detector's sight radius.
        /// Called by OverworldMovementController once per cell step.
        /// </summary>
        public bool IsPlayerInRange(Vector3 playerWorldPos)
        {
            return Vector3.Distance(playerWorldPos, transform.position) <= _sightRadius;
        }

        /// <summary>
        /// Initiate combat. Called by OverworldMovementController when it
        /// confirms the player has stepped into range.
        /// </summary>
        public void FireCombat(BaseUnit playerUnit)
        {
            if (HasFired) return;
            if (_ownerUnit == null || !_ownerUnit.IsAlive) return;
            if (_stateManager != null && !_stateManager.IsInOverworld) return;
            if (_combatController == null)
            {
                Debug.LogWarning("[CombatTriggerDetector] CombatStateController not found in ServiceLocator.");
                return;
            }

            HasFired = true;

            Debug.Log($"[CombatTriggerDetector] {_ownerUnit.DisplayName} spotted {playerUnit.DisplayName}!");

            // Gather participants: this hostile + nearby allies + triggering player
            var participants = GatherParticipants(playerUnit);
            var zoneCenter   = Vector3.Lerp(transform.position, playerUnit.transform.position, 0.5f);

            _combatController.InitiateEncounter(participants, zoneCenter);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private List<BaseUnit> GatherParticipants(BaseUnit playerUnit)
        {
            var list = new List<BaseUnit> { _ownerUnit, playerUnit };

            // Include nearby hostile allies
            var allUnits = Object.FindObjectsByType<BaseUnit>(FindObjectsSortMode.None);
            foreach (var u in allUnits)
            {
                if (u == _ownerUnit || u == playerUnit) continue;
                if (u.Faction != UnitFaction.Hostile)   continue;
                if (!u.IsAlive)                         continue;
                if (Vector3.Distance(transform.position, u.transform.position) <= _allyGatherRadius)
                    list.Add(u);
            }

            return list;
        }

        // ── Events ────────────────────────────────────────────────────────────

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            // Reset so the enemy can trigger again after the next overworld return
            if (evt.NewState == GameState.Overworld)
                HasFired = false;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.20f);
            Gizmos.DrawWireSphere(transform.position, _sightRadius);

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.08f);
            Gizmos.DrawWireSphere(transform.position, _allyGatherRadius);
        }
    }
}
