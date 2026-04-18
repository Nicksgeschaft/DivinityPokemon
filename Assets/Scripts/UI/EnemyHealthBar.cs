using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PokemonAdventure.Core;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // ==========================================================================
    // Enemy Health Bar
    // World-space health bar shown above an enemy unit.
    //
    //   IN COMBAT:  always visible
    //   OVERWORLD:  visible only while the mouse hovers over the unit
    //
    // Setup:
    //   1. Add this component to your enemy unit root GameObject.
    //   2. Wire up _fillImage (the green bar), _backgroundImage, and optionally
    //      _nameText.  The root Canvas should be set to World Space.
    //   3. The unit must have a Collider for hover detection to work.
    //
    // The bar auto-billboards toward the main camera every frame.
    // ==========================================================================

    [RequireComponent(typeof(BaseUnit))]
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("Bar References")]
        [Tooltip("The Canvas root (World Space). Positioned above the unit in the prefab.")]
        [SerializeField] private Canvas          _canvas;
        [SerializeField] private Image           _fillImage;
        [SerializeField] private Image           _backgroundImage;
        [SerializeField] private TextMeshProUGUI _nameText;

        [Header("Colours")]
        [SerializeField] private Color _fullColor = new Color(0.20f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color _midColor  = new Color(0.90f, 0.80f, 0.10f, 1f);
        [SerializeField] private Color _lowColor  = new Color(0.90f, 0.20f, 0.20f, 1f);

        [Header("Thresholds")]
        [SerializeField] private float _midThreshold = 0.50f;
        [SerializeField] private float _lowThreshold = 0.25f;

        // ── Private state ─────────────────────────────────────────────────────

        private BaseUnit _unit;
        private bool     _inCombat;
        private bool     _hovered;

        // ── Public Init (for code-spawned prefabs) ────────────────────────────

        /// <summary>
        /// Wire canvas and images when they are created in code rather than in the prefab.
        /// Call this before Start() runs (i.e. from EnemyPrefabController.Awake).
        /// </summary>
        public void Initialize(Canvas canvas, Image fillImage, Image backgroundImage,
                               TextMeshProUGUI nameText = null)
        {
            _canvas          = canvas;
            _fillImage       = fillImage;
            _backgroundImage = backgroundImage;
            _nameText        = nameText;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _unit = GetComponent<BaseUnit>();
        }

        private void Start()
        {
            if (_nameText != null)
                _nameText.text = _unit != null ? _unit.DisplayName : string.Empty;

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
            // Hover detection (overworld only — combat uses always-on)
            if (!_inCombat)
            {
                bool nowHovered = CheckHover();
                if (nowHovered != _hovered)
                {
                    _hovered = nowHovered;
                    SetVisible(_hovered);
                }
            }

            // Billboard: rotate bar to always face camera
            Billboard();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Refresh()
        {
            if (_unit == null || _fillImage == null) return;

            var state = _unit.RuntimeState;
            float maxHP = _unit.Stats.MaxHP;
            if (maxHP <= 0f) return;

            float pct = Mathf.Clamp01(state.CurrentHP / maxHP);
            _fillImage.fillAmount = pct;
            _fillImage.color      = pct > _midThreshold ? _fullColor
                                  : pct > _lowThreshold ? _midColor
                                  : _lowColor;
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

            if (_inCombat)
            {
                _hovered = false;
                SetVisible(true);
            }
            else
            {
                SetVisible(false);
            }
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

            // Check if the hit object belongs to this unit's hierarchy
            return hit.transform.GetComponentInParent<BaseUnit>() == _unit;
        }

        private void Billboard()
        {
            if (_canvas == null || Camera.main == null) return;
            _canvas.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
