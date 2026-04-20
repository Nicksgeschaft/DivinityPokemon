using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

        [Header("Health Bar – HP Colours")]
        [SerializeField] private Color _hpColorFull = new Color(0.20f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color _hpColorMid  = new Color(0.90f, 0.80f, 0.10f, 1f);
        [SerializeField] private Color _hpColorLow  = new Color(0.90f, 0.20f, 0.20f, 1f);
        [SerializeField] [Range(0f, 1f)] private float _hpMidThreshold = 0.50f;
        [SerializeField] [Range(0f, 1f)] private float _hpLowThreshold = 0.25f;

        [Header("Health Bar – Armor Colours")]
        [SerializeField] private Color _physArmorColor = new Color(0.25f, 0.55f, 1.00f, 1f);
        [SerializeField] private Color _specArmorColor = new Color(0.75f, 0.30f, 0.90f, 1f);

        [Header("Health Bar – Text")]
        [SerializeField] private TMP_FontAsset _barFont;
        [SerializeField] private float         _barFontSize = 10f;

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
        //
        // Layout (canvas 120 × 32 px, scale 0.012):
        //   Top row  (y 18–30): [PhysArmor 56px] [4px gap] [SpecArmor 56px]
        //   Bot row  (y  2–14): [HP bar 116px — same total width as armor row]
        //
        // Each bar: dark background strip + horizontal fill slider + centred label.
        // Canvas renders in front of all sprites via overrideSorting + high order.

        private void BuildHealthBarCanvas(string unitName)
        {
            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = Vector3.zero;
            canvasGo.transform.localScale    = Vector3.one * 0.012f;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode      = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder    = 100;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(120f, 32f);

            // Top row: armor bars side by side
            var (physFill, physText) = MakeBarWithLabel(canvasGo, "PhysArmor",
                new Vector2(2f, 18f), new Vector2(56f, 12f),
                _physArmorColor, _barFont, _barFontSize);

            var (specFill, specText) = MakeBarWithLabel(canvasGo, "SpecArmor",
                new Vector2(62f, 18f), new Vector2(56f, 12f),
                _specArmorColor, _barFont, _barFontSize);

            // Bottom row: HP bar, same total width as both armor bars
            var (hpFill, hpText) = MakeBarWithLabel(canvasGo, "HP",
                new Vector2(2f, 2f), new Vector2(116f, 12f),
                _hpColorFull, _barFont, _barFontSize);

            var healthBar = gameObject.AddComponent<EnemyHealthBar>();
            healthBar.Initialize(
                canvas,
                hpFill, hpText, _hpColorFull, _hpColorMid, _hpColorLow,
                _hpMidThreshold, _hpLowThreshold,
                physFill, physText,
                specFill, specText);

            canvasGo.SetActive(false);
        }

        // Each bar = container → dark bg strip + fill slider + centred label.
        // The dark bg makes the "empty" portion always visible, so the bar
        // behaves like a proper slider (1/7 HP shows a small strip, not a full bar).
        private static (Image fill, TextMeshProUGUI label) MakeBarWithLabel(
            GameObject canvas, string barName,
            Vector2 pos, Vector2 size,
            Color fillColor, TMP_FontAsset font, float fontSize)
        {
            // Container (no Image — just a positioned rect)
            var barGo = new GameObject(barName);
            barGo.transform.SetParent(canvas.transform, false);
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin        = Vector2.zero;
            barRt.anchorMax        = Vector2.zero;
            barRt.pivot            = Vector2.zero;
            barRt.anchoredPosition = pos;
            barRt.sizeDelta        = size;

            // Dark background strip (makes empty portion visible)
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(barGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.10f, 1f);

            // Fill rect — width driven by anchorMax.x (0=empty … 1=full).
            // Using a plain Simple image so no sprite is required.
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;   // full at start
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fill = fillGo.AddComponent<Image>();
            fill.color = fillColor;

            // Centred label — child of container so it sits above fill
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(barGo.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.fontSize         = fontSize;
            tmp.fontStyle        = FontStyles.Bold;
            tmp.color            = Color.white;
            tmp.enableAutoSizing = false;
            tmp.text             = "–";
            if (font != null) tmp.font = font;

            return (fill, tmp);
        }
    }
}
