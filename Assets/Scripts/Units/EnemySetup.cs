using UnityEngine;
using PokemonAdventure.AI;
using PokemonAdventure.Animations;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Enemy Setup
    // Reads the EnemyArchetypeDefinition assigned to this unit's EnemyUnit
    // component and initialises sprite animation at runtime.
    //
    // Place this on every enemy prefab alongside EnemyUnit.
    // Assign the Sprite child (the GameObject with SpriteRenderer) to _spriteRoot.
    // EnemySetup will assign the AnimationSet from the archetype automatically.
    //
    // Prefab hierarchy expected:
    //   EnemyRoot  ← EnemyUnit, AIController, ActionPointController,
    //                CombatTriggerDetector, UnitAnimationController,
    //                EnemySetup, EnemyHealthBar, BoxCollider
    //     └─ Sprite ← SpriteRenderer, PokemonSpriteAnimator
    //     └─ HealthBarCanvas ← Canvas (World Space), EnemyHealthBar-child UIs
    // ==========================================================================

    [RequireComponent(typeof(EnemyUnit))]
    public class EnemySetup : MonoBehaviour
    {
        [Header("Sprite")]
        [Tooltip("Child GameObject that has the PokemonSpriteAnimator. " +
                 "If left empty the first PokemonSpriteAnimator in children is used.")]
        [SerializeField] private PokemonSpriteAnimator _spriteAnimator;

        private void Start()
        {
            var enemyUnit = GetComponent<EnemyUnit>();
            var archetype = enemyUnit?.Archetype;
            if (archetype == null) return;

            // Name in Hierarchy
            gameObject.name = archetype.EnemyName;

            // Wire AnimationSet into the sprite animator
            if (_spriteAnimator == null)
                _spriteAnimator = GetComponentInChildren<PokemonSpriteAnimator>();

            if (_spriteAnimator != null && archetype.AnimationSet != null)
                _spriteAnimator.AnimSet = archetype.AnimationSet;

            // If UnitAnimationController is present but has no animator yet, give it this one
            var animCtrl = GetComponent<UnitAnimationController>();
            // UnitAnimationController's Start() also re-looks for the animator — no extra wiring needed.
        }
    }
}
