using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.Combat
{
    // ==========================================================================
    // Combat State Controller
    // Replaces CombatManager. Owns encounter lifecycle only — not the turn loop.
    //
    // Responsibilities:
    //   1. Initiate encounter: collect participants, transition to Combat state
    //   2. Hand the turn loop to TurnManager
    //   3. Bridge TurnEndedEvent → TurnManager.ForceAdvance()
    //   4. Tear down the encounter on CombatEndedEvent, return to Overworld
    //
    // Registered with ServiceLocator by GameBootstrapper.
    // ==========================================================================

    public class CombatStateController : MonoBehaviour
    {
        [Header("Transitions")]
        [Tooltip("Pause before first turn (freeze + visual transition placeholder).")]
        [SerializeField] private float _transitionInDuration  = 0.4f;

        [Tooltip("Pause after last turn before returning to overworld.")]
        [SerializeField] private float _transitionOutDuration = 0.6f;

        [Header("References")]
        [SerializeField] private Grid.GridOverlay _gridOverlay;

        // ── State ─────────────────────────────────────────────────────────────

        private GameStateManager _stateManager;
        private TurnManager      _turnManager;
        private CombatEncounter  _activeEncounter;

        public CombatEncounter ActiveEncounter => _activeEncounter;
        public bool            IsInCombat      => _stateManager?.IsInCombat ?? false;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _turnManager = new TurnManager();
        }

        private void Start()
        {
            _stateManager = ServiceLocator.Get<GameStateManager>();

            if (_gridOverlay == null)
                _gridOverlay = FindAnyObjectByType<Grid.GridOverlay>();

            // Turn loop events
            GameEventBus.Subscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Subscribe<TurnDelayRequestedEvent>(OnDelayRequested);
            GameEventBus.Subscribe<CombatEndedEvent>(OnCombatEnded);

            // Unit lifecycle
            GameEventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<TurnEndedEvent>(OnTurnEnded);
            GameEventBus.Unsubscribe<TurnDelayRequestedEvent>(OnDelayRequested);
            GameEventBus.Unsubscribe<CombatEndedEvent>(OnCombatEnded);
            GameEventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by CombatTriggerDetector when a player unit walks into range.
        /// If an encounter is already running, new participants are merged in.
        /// </summary>
        public void InitiateEncounter(List<BaseUnit> participants, Vector3 zoneCenter)
        {
            if (_activeEncounter != null && _activeEncounter.IsActive)
            {
                foreach (var u in participants)
                {
                    _turnManager.AddUnit(u);
                    GameEventBus.Publish(new UnitEnteredCombatEvent
                    {
                        UnitId      = u.UnitId,
                        EncounterId = _activeEncounter.EncounterId
                    });
                }
                return;
            }

            StartCoroutine(BeginEncounterRoutine(participants, zoneCenter));
        }

        /// <summary>
        /// Called when a free-moving friendly unit walks into the combat zone.
        /// Adds them at the end of the current round's initiative.
        /// </summary>
        public void JoinCombat(BaseUnit unit)
        {
            if (_activeEncounter == null || !_activeEncounter.IsActive) return;
            if (_activeEncounter.Participants.Contains(unit)) return;

            _turnManager.AddUnitAtEndOfRound(unit);

            GameEventBus.Publish(new UnitEnteredCombatEvent
            {
                UnitId      = unit.UnitId,
                EncounterId = _activeEncounter.EncounterId
            });

            Debug.Log($"[CombatStateController] {unit.DisplayName} walked into the combat zone and joined.");
        }

        // ── Encounter Lifecycle ───────────────────────────────────────────────

        private IEnumerator BeginEncounterRoutine(List<BaseUnit> participants, Vector3 zoneCenter)
        {
            // TODO: Screen flash / battle fanfare here
            yield return new WaitForSeconds(_transitionInDuration);

            _activeEncounter = new CombatEncounter(participants);

            // Notify each participant so they can disable overworld movement
            foreach (var p in participants)
                GameEventBus.Publish(new UnitEnteredCombatEvent
                {
                    UnitId      = p.UnitId,
                    EncounterId = _activeEncounter.EncounterId
                });

            // Grid overlay is managed per-cell by CombatMovementController
            _gridOverlay?.HideAll();

            // Transition game state — OverworldMovementController and
            // camera both listen to this and handle themselves
            _stateManager?.TransitionTo(GameState.Combat);

            GameEventBus.Publish(new CombatStartedEvent
            {
                EncounterId = _activeEncounter.EncounterId
            });

            _turnManager.Begin(_activeEncounter);
        }

        private IEnumerator EndEncounterRoutine(bool playerVictory)
        {
            Debug.Log($"[CombatStateController] Encounter ended. PlayerVictory={playerVictory}");

            _activeEncounter?.Close(playerVictory);

            _gridOverlay?.HideAll();

            // TODO: Results screen / fanfare
            yield return new WaitForSeconds(_transitionOutDuration);

            _activeEncounter = null;
            _stateManager?.TransitionTo(GameState.Overworld);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnTurnEnded(TurnEndedEvent evt)
        {
            if (!IsInCombat || !_turnManager.IsRunning) return;
            _turnManager.ForceAdvance();
        }

        private void OnDelayRequested(TurnDelayRequestedEvent evt)
        {
            if (!IsInCombat || !_turnManager.IsRunning) return;
            if (_turnManager.ActiveUnit?.UnitId != evt.UnitId) return;
            _turnManager.DelayCurrentTurn();
        }

        private void OnUnitDied(UnitDiedEvent evt)
        {
            if (_activeEncounter == null || !_activeEncounter.IsActive) return;

            var dead = _activeEncounter.Participants
                       .FirstOrDefault(u => u.UnitId == evt.UnitId);
            if (dead == null) return;

            // TurnManager.RemoveUnit handles end-condition check internally
            _turnManager.RemoveUnit(dead);
            _activeEncounter.RemoveUnit(dead);

            // Only destroy hostile units — friendly deaths stay for the results screen
            if (dead.Faction == UnitFaction.Hostile)
                StartCoroutine(DestroyAfterDelay(dead.gameObject, _transitionOutDuration));
        }

        private static IEnumerator DestroyAfterDelay(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go != null) Destroy(go);
        }

        private void OnCombatEnded(CombatEndedEvent evt)
        {
            if (_activeEncounter == null ||
                _activeEncounter.EncounterId != evt.EncounterId) return;

            StartCoroutine(EndEncounterRoutine(evt.PlayerVictory));
        }
    }
}
