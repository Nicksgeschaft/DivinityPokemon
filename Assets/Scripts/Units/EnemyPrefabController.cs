using UnityEngine;
using UnityEngine.UI;
using PokemonAdventure.AI;
using PokemonAdventure.Animations;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.UI;
using PokemonAdventure.World;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Enemy Prefab Controller
    // Drop this on an empty GameObject, assign an EnemyArchetypeDefinition,
    // and everything is built automatically at runtime.
    //
    // Also callable from code (LevelLoader):
    //   var ctrl = go.AddComponent<EnemyPrefabController>();
    //   ctrl.Build(archetype);   ← call immediately after AddComponent
    //
    // Built components:
    //   ✓ EnemyUnit            (stats, faction, display name)
    //   ✓ ActionPointController
    //   ✓ AIController
    //   ✓ UnitAnimationController
    //   ✓ CombatTriggerDetector
    //   ✓ BoxCollider          (hover detection)
    //   ✓ Sprite child         (SpriteRenderer + PokemonSpriteAnimator)
    //   ✓ HealthBarCanvas child (world-space bar)
    // ==========================================================================

    public class EnemyPrefabController : MonoBehaviour
    {
        [Header("Archetype")]
        [Tooltip("Drag the EnemyArchetypeDefinition here. Everything else is automatic.")]
        [SerializeField] private EnemyArchetypeDefinition _archetype;

        private bool _built;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // When archetype is pre-assigned in Inspector, build immediately.
            if (_archetype != null && !_built)
                DoBuild();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Code-path entry point. Call directly after AddComponent when
        /// the archetype is known at runtime (e.g. from LevelLoader).
        /// Safe to call multiple times — only the first call builds.
        /// </summary>
        public void Build(EnemyArchetypeDefinition archetype)
        {
            _archetype = archetype;
            if (!_built)
                DoBuild();
        }

        // ── Build Logic ───────────────────────────────────────────────────────

        private void DoBuild()
        {
            if (_archetype == null)
            {
                Debug.LogWarning($"[EnemyPrefabController] {name}: No archetype assigned.");
                return;
            }

            _built = true;

            // 1. Core unit — must come before anything that requires BaseUnit
            var enemy = gameObject.AddComponent<EnemyUnit>();
            enemy.Initialize(_archetype);

            // 2. Combat components
            gameObject.AddComponent<ActionPointController>();
            gameObject.AddComponent<AIController>();
            gameObject.AddComponent<UnitAnimationController>();
            gameObject.AddComponent<CombatTriggerDetector>();

            // 3. Physics collider for hover detection
            var col = gameObject.AddComponent<BoxCollider>();
            col.size   = new Vector3(0.8f, 0.8f, 0.8f);
            col.center = new Vector3(0f, 0.4f, 0f);

            // 4. Sprite animator
            if (_archetype.AnimationSet != null)
                BuildSpriteChild(_archetype.AnimationSet);

            // 5. World-space health bar
            BuildHealthBarCanvas(enemy.DisplayName);
        }

        // ── Sprite Child ──────────────────────────────────────────────────────

        private void BuildSpriteChild(PokemonAnimationSet animSet)
        {
            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(transform, false);
            spriteGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);

            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1;

            var animator = spriteGo.AddComponent<PokemonSpriteAnimator>();
            animator.AnimSet = animSet;
        }

        // ── Health Bar Canvas ─────────────────────────────────────────────────

        private void BuildHealthBarCanvas(string unitName)
        {
            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            canvasGo.transform.localScale    = Vector3.one * 0.01f;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(100f, 14f);

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

            // Fill bar
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color      = new Color(0.20f, 0.85f, 0.25f, 1f);
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            var healthBar = gameObject.AddComponent<EnemyHealthBar>();
            healthBar.Initialize(canvas, fillImg, bgImg);

            canvasGo.SetActive(false);
        }
    }
}
