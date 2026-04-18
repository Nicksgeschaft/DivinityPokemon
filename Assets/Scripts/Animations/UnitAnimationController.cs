using System.Collections;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.ScriptableObjects;

namespace PokemonAdventure.Animations
{
    // ==========================================================================
    // Unit Animation Controller
    // Bridges game events to PokemonSpriteAnimator.
    // Attach to the same GameObject as BaseUnit.
    //
    // Each animation event is configurable in the Inspector so you can tweak
    // per-unit which animation plays for Hurt, Death, etc.
    //
    // Non-looping animations (Hurt, Attack, Faint...) automatically return to
    // Idle via OnAnimationComplete — except for KO which locks on the KO idle.
    // ==========================================================================

    [RequireComponent(typeof(Units.BaseUnit))]
    public class UnitAnimationController : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Child GameObject that has the PokemonSpriteAnimator component.")]
        [SerializeField] private PokemonSpriteAnimator _animator;

        [Header("Automatic Event Animations")]
        [Tooltip("Default looping animation when nothing is happening.")]
        public PokemonAnimId IdleAnim      = PokemonAnimId.Idle;

        [Tooltip("Played while the unit is walking between cells.")]
        public PokemonAnimId WalkAnim      = PokemonAnimId.Walk;

        [Tooltip("Played when the unit takes damage.")]
        public PokemonAnimId HurtAnim      = PokemonAnimId.Hurt;

        [Tooltip("First animation played on KO (one-shot).")]
        public PokemonAnimId FaintAnim     = PokemonAnimId.Faint;

        [Tooltip("Looping animation after KO is confirmed (stays on this until revived).")]
        public PokemonAnimId KOIdleAnim    = PokemonAnimId.Laying;

        [Tooltip("Played at the start of this unit's turn.")]
        public PokemonAnimId TurnStartAnim = PokemonAnimId.Idle;

        // ── Private state ─────────────────────────────────────────────────────

        private Units.BaseUnit _unit;
        private bool           _isKO;
        private bool           _isMoving;
        private Coroutine      _returnToIdleCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _unit = GetComponent<Units.BaseUnit>();

            if (_animator == null)
                _animator = GetComponentInChildren<PokemonSpriteAnimator>();

            if (_animator != null)
                _animator.OnAnimationComplete += OnCurrentAnimDone;
        }

        private void Start()
        {
            // Second chance — EnemySetup creates the animator child in Start(),
            // which may run before or after this Start(). Re-wire if needed.
            if (_animator == null)
            {
                _animator = GetComponentInChildren<PokemonSpriteAnimator>();
                if (_animator != null)
                    _animator.OnAnimationComplete += OnCurrentAnimDone;
            }

            GameEventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<MovementStartedEvent>(OnMovementStarted);
            GameEventBus.Subscribe<MovementCompletedEvent>(OnMovementCompleted);
            GameEventBus.Subscribe<ActionExecutedEvent>(OnActionExecuted);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<MovementStartedEvent>(OnMovementStarted);
            GameEventBus.Unsubscribe<MovementCompletedEvent>(OnMovementCompleted);
            GameEventBus.Unsubscribe<ActionExecutedEvent>(OnActionExecuted);

            if (_animator != null)
                _animator.OnAnimationComplete -= OnCurrentAnimDone;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Plays a one-shot animation then returns to Idle.
        /// If returnToIdle is false, the animator stays on the last frame
        /// (use this for KO or permanent state changes).
        /// </summary>
        public void PlayOnce(PokemonAnimId anim, bool returnToIdle = true)
        {
            if (_animator == null || _isKO && anim != FaintAnim && anim != KOIdleAnim) return;

            StopReturnToIdle();
            _animator.PlayForced(anim);

            if (returnToIdle)
                _returnToIdleCoroutine = StartCoroutine(WaitAndReturnToIdle());
        }

        public void PlayIdle()
        {
            if (_animator == null || _isKO) return;
            StopReturnToIdle();
            _animator.Play(IdleAnim);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_unit == null || evt.DefenderUnitId != _unit.UnitId) return;
            if (_isKO) return;
            PlayOnce(HurtAnim, returnToIdle: true);
        }

        private void OnUnitDied(UnitDiedEvent evt)
        {
            if (_unit == null || evt.UnitId != _unit.UnitId) return;
            _isKO = true;
            StopReturnToIdle();

            // Play Faint once, then lock into KOIdle
            if (_animator == null) return;
            _animator.PlayForced(FaintAnim);
            _returnToIdleCoroutine = StartCoroutine(WaitThenPlay(KOIdleAnim));
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (_unit == null || evt.ActiveUnitId != _unit.UnitId) return;
            if (_isKO || _isMoving) return;

            _animator?.Play(TurnStartAnim);
        }

        private void OnMovementStarted(MovementStartedEvent evt)
        {
            if (_unit == null || evt.UnitId != _unit.UnitId) return;
            if (_isKO) return;

            _isMoving = true;
            StopReturnToIdle();
            _animator?.Play(WalkAnim);
        }

        private void OnMovementCompleted(MovementCompletedEvent evt)
        {
            if (_unit == null || evt.UnitId != _unit.UnitId) return;

            _isMoving = false;
            if (!_isKO)
                _animator?.Play(IdleAnim);
        }

        private void OnActionExecuted(ActionExecutedEvent evt)
        {
            if (_unit == null || evt.ActorUnitId != _unit.UnitId) return;
            if (_isKO) return;

            // Resolve skill cast animation from SkillRegistry if possible,
            // otherwise fall back to a generic Attack animation.
            var skillAnim = ResolveSkillAnimation(evt.ActionName);
            PlayOnce(skillAnim, returnToIdle: true);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void OnCurrentAnimDone()
        {
            // Called by PokemonSpriteAnimator when a non-loop anim finishes.
            // WaitAndReturnToIdle handles the actual transition; this is a hook
            // for any extra logic needed on completion.
        }

        private IEnumerator WaitAndReturnToIdle()
        {
            // Wait until the animator signals the current anim is done
            yield return new WaitUntil(() => _animator == null || _animator.IsFinished);
            if (!_isKO)
                _animator?.Play(IdleAnim);
            _returnToIdleCoroutine = null;
        }

        private IEnumerator WaitThenPlay(PokemonAnimId anim)
        {
            yield return new WaitUntil(() => _animator == null || _animator.IsFinished);
            _animator?.Play(anim);
            _returnToIdleCoroutine = null;
        }

        private void StopReturnToIdle()
        {
            if (_returnToIdleCoroutine != null)
            {
                StopCoroutine(_returnToIdleCoroutine);
                _returnToIdleCoroutine = null;
            }
        }

        private PokemonAnimId ResolveSkillAnimation(string skillName)
        {
            var registry = ServiceLocator.Get<SkillRegistry>();
            if (registry != null)
            {
                foreach (var skill in registry.All)
                {
                    if (skill != null && skill.SkillName == skillName)
                        return skill.CastAnimation;
                }
            }
            return PokemonAnimId.Attack;
        }
    }
}
