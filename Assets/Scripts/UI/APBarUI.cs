using UnityEngine;
using UnityEngine.UI;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // ==========================================================================
    // AP Bar UI
    // Displays the active player's Action Points as 6 coloured circles.
    // First 3 slots are green (base turn AP), last 3 are bonus carry-over.
    // Filled = current AP available, empty = spent.
    //
    // Creates its own Screen Space Canvas + circles at runtime — no prefab needed.
    // Show/hide driven by GameStateChangedEvent (visible only in combat).
    // ==========================================================================

    public class APBarUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float _circleSize    = 28f;
        [SerializeField] private float _spacing       = 6f;
        [SerializeField] private Vector2 _screenAnchor = new Vector2(0.5f, 0.04f);

        [Header("Colours")]
        [SerializeField] private Color _greenColor = new Color(0.20f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color _grayColor  = new Color(0.28f, 0.28f, 0.28f, 1f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private Image[]    _circles;
        private Canvas     _canvas;
        private BaseUnit   _trackedUnit;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            BuildUI();

            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnChanged);
            GameEventBus.Subscribe<TurnEndedEvent>(OnTurnChanged);
            GameEventBus.Subscribe<MovementCompletedEvent>(OnMovementCompleted);

            // Try to find the player unit immediately
            TryFindPlayerUnit();

            SetVisible(false);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnChanged);
            GameEventBus.Unsubscribe<TurnEndedEvent>(OnTurnChanged);
            GameEventBus.Unsubscribe<MovementCompletedEvent>(OnMovementCompleted);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetTrackedUnit(BaseUnit unit)
        {
            _trackedUnit = unit;
            Refresh();
        }

        public void Refresh()
        {
            if (_circles == null) return;
            int ap = _trackedUnit != null ? _trackedUnit.RuntimeState.CurrentAP : 0;
            for (int i = 0; i < _circles.Length; i++)
                _circles[i].color = i < ap ? _greenColor : _grayColor;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            bool inCombat = evt.NewState == GameState.Combat;
            SetVisible(inCombat);
            if (inCombat) TryFindPlayerUnit();
        }

        private void OnTurnChanged<T>(T _) => Refresh();

        private void OnMovementCompleted(MovementCompletedEvent _) => Refresh();

        // ── UI Construction ───────────────────────────────────────────────────

        private void BuildUI()
        {
            // Reuse existing Screen Space Overlay canvas or create one
            _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                var cgo    = new GameObject("APBarCanvas");
                _canvas    = cgo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 10;
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
            }

            // Container
            var container = new GameObject("APBarContainer");
            container.transform.SetParent(_canvas.transform, false);

            var rt = container.AddComponent<RectTransform>();
            rt.anchorMin = _screenAnchor;
            rt.anchorMax = _screenAnchor;
            rt.pivot     = new Vector2(0.5f, 0.5f);

            float totalWidth = RuntimeUnitState.MaxAPCap * _circleSize
                             + (RuntimeUnitState.MaxAPCap - 1) * _spacing;
            rt.sizeDelta     = new Vector2(totalWidth, _circleSize);
            rt.anchoredPosition = Vector2.zero;

            // Horizontal layout
            var layout          = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing      = _spacing;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment         = TextAnchor.MiddleCenter;

            // Background panel (subtle dark)
            var bg         = container.AddComponent<Image>();
            bg.color       = new Color(0f, 0f, 0f, 0.35f);
            bg.raycastTarget = false;

            // Padding
            layout.padding = new RectOffset(8, 8, 4, 4);
            rt.sizeDelta   = new Vector2(totalWidth + 16, _circleSize + 8);

            // 6 circle slots
            _circles = new Image[RuntimeUnitState.MaxAPCap];
            var circleSprite = GetCircleSprite();

            for (int i = 0; i < RuntimeUnitState.MaxAPCap; i++)
            {
                var slotGo = new GameObject($"AP_{i}");
                slotGo.transform.SetParent(container.transform, false);

                var slotRt       = slotGo.AddComponent<RectTransform>();
                slotRt.sizeDelta = Vector2.one * _circleSize;

                var img          = slotGo.AddComponent<Image>();
                img.sprite       = circleSprite;
                img.color        = i < 3 ? _greenColor : _grayColor;
                img.raycastTarget = false;

                _circles[i] = img;
            }
        }

        private static Sprite GetCircleSprite()
        {
            // Generate a filled circle texture at runtime (no asset dependency)
            const int size = 64;
            var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = size * 0.5f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx  = x - center + 0.5f;
                float dy  = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Anti-alias edge over 1 pixel
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size);
        }

        private void TryFindPlayerUnit()
        {
            if (_trackedUnit != null) return;
            var pu = FindFirstObjectByType<PlayerUnit>();
            if (pu != null) SetTrackedUnit(pu);
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null)
                gameObject.SetActive(visible); // the container follows the GO
            // Actually toggle the container child
            if (_circles != null && _circles.Length > 0 && _circles[0] != null)
                _circles[0].transform.parent.gameObject.SetActive(visible);
        }
    }
}
