using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PokemonAdventure.Core;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    [RequireComponent(typeof(BaseUnit))]
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("Bar References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image  _hpFillImage;
        [SerializeField] private Image  _physArmorFillImage;
        [SerializeField] private Image  _specArmorFillImage;

        [Header("Label References")]
        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private TextMeshProUGUI _physArmorText;
        [SerializeField] private TextMeshProUGUI _specArmorText;

        [Header("HP Colours")]
        [SerializeField] private Color _fullColor = new Color(0.20f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color _midColor  = new Color(0.90f, 0.80f, 0.10f, 1f);
        [SerializeField] private Color _lowColor  = new Color(0.90f, 0.20f, 0.20f, 1f);

        [Header("Thresholds")]
        [SerializeField] private float _midThreshold = 0.50f;
        [SerializeField] private float _lowThreshold = 0.25f;

        private BaseUnit _unit;
        private bool     _inCombat;
        private bool     _hovered;

        // ── Public Init ───────────────────────────────────────────────────────

        public void Initialize(
            Canvas canvas,
            Image hpFill, TextMeshProUGUI hpText,
            Color hpFull, Color hpMid, Color hpLow,
            float midThreshold, float lowThreshold,
            Image physArmorFill, TextMeshProUGUI physText,
            Image specArmorFill, TextMeshProUGUI specText)
        {
            _canvas             = canvas;
            _hpFillImage        = hpFill;
            _hpText             = hpText;
            _fullColor          = hpFull;
            _midColor           = hpMid;
            _lowColor           = hpLow;
            _midThreshold       = midThreshold;
            _lowThreshold       = lowThreshold;
            _physArmorFillImage = physArmorFill;
            _physArmorText      = physText;
            _specArmorFillImage = specArmorFill;
            _specArmorText      = specText;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _unit = GetComponent<BaseUnit>();
        }

        private void Start()
        {
            GameEventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

            SetVisible(false);
            Refresh();
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void Update()
        {
            if (!_inCombat)
            {
                bool nowHovered = CheckHover();
                if (nowHovered != _hovered)
                {
                    _hovered = nowHovered;
                    SetVisible(_hovered);
                }
            }

            UpdateCanvasTransform();
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        public void Refresh()
        {
            if (_unit == null) return;
            var state = _unit.RuntimeState;

            // HP
            float maxHP = _unit.Stats.MaxHP;
            float hpPct = maxHP > 0f ? Mathf.Clamp01(state.CurrentHP / maxHP) : 0f;
            SetFill(_hpFillImage, hpPct);
            if (_hpFillImage != null)
                _hpFillImage.color = hpPct > _midThreshold ? _fullColor
                                   : hpPct > _lowThreshold ? _midColor
                                   : _lowColor;
            if (_hpText != null)
                _hpText.text = $"{Mathf.RoundToInt(state.CurrentHP)}/{Mathf.RoundToInt(maxHP)}";

            // Physical armor
            float maxPhys = _unit.Stats.MaxPhysicalArmor;
            float physPct = maxPhys > 0f ? Mathf.Clamp01(state.CurrentPhysicalArmor / maxPhys) : 0f;
            SetFill(_physArmorFillImage, physPct);
            if (_physArmorText != null)
                _physArmorText.text = $"{Mathf.RoundToInt(state.CurrentPhysicalArmor)}/{Mathf.RoundToInt(maxPhys)}";

            // Special armor
            float maxSpec = _unit.Stats.MaxSpecialArmor;
            float specPct = maxSpec > 0f ? Mathf.Clamp01(state.CurrentSpecialArmor / maxSpec) : 0f;
            SetFill(_specArmorFillImage, specPct);
            if (_specArmorText != null)
                _specArmorText.text = $"{Mathf.RoundToInt(state.CurrentSpecialArmor)}/{Mathf.RoundToInt(maxSpec)}";
        }

        // Resize the fill rect from the left by setting anchorMax.x.
        // No sprite needed — this is pure RectTransform geometry.
        private static void SetFill(Image fill, float pct)
        {
            if (fill == null) return;
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(pct), rt.anchorMax.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_unit == null || evt.DefenderUnitId != _unit.UnitId) return;
            Refresh();
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            _inCombat = evt.NewState == Data.GameState.Combat;
            _hovered  = false;
            SetVisible(_inCombat);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(visible);
        }

        private bool CheckHover()
        {
            if (Camera.main == null) return false;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 200f)) return false;
            return hit.transform.GetComponentInParent<BaseUnit>() == _unit;
        }

        private void UpdateCanvasTransform()
        {
            if (_canvas == null || Camera.main == null) return;

            var cam = Camera.main;

            // Use the full camera up vector — for isometric cameras it naturally
            // includes a positive Y component that keeps the canvas in front of
            // ground tiles in the depth buffer.
            var up = cam.transform.up;

            // For a nearly top-down camera the up vector is horizontal (Y ≈ 0).
            // Add a floor so the canvas always sits above ground geometry.
            if (up.y < 0.2f)
                up = (up + Vector3.up * 0.5f).normalized;

            _canvas.transform.position = transform.position + up * 0.7f;
            _canvas.transform.rotation = cam.transform.rotation;
        }
    }
}
