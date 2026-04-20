using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PokemonAdventure.Core;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;
using PokemonAdventure.World;

namespace PokemonAdventure.UI
{
    // One character card in the left party sidebar.
    // Self-subscribes to DamageDealtEvent and TurnStartedEvent so bars and
    // portrait stay in sync without needing PartySidebarUI to drive refreshes.
    public class PartyMemberEntryUIController : MonoBehaviour, IPointerClickHandler
    {
        [Header("Click Target")]
        [Tooltip("Assign the Button on this entry. If left empty, GetComponentInChildren is used.")]
        [SerializeField] private Button _clickButton;

        [Header("Portrait")]
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Image _backgroundFrame;

        [Header("Mini Bars (Sliders — same setup as bottom HUD)")]
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private Slider _physArmorSlider;
        [SerializeField] private Slider _specArmorSlider;

        [Header("Active Highlight")]
        [Tooltip("GameObject shown when this character is the active turn unit.")]
        [SerializeField] private GameObject _activeHighlight;

        // ── Runtime ───────────────────────────────────────────────────────────

        private BaseUnit          _unit;
        private PokemonDefinition _definition;

        public BaseUnit Unit => _unit;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Wire button — works whether the Button is on this GO or a child
            if (_clickButton == null)
                _clickButton = GetComponentInChildren<Button>();

            if (_clickButton != null)
                _clickButton.onClick.AddListener(SwitchToThisUnit);
            else
                Debug.LogWarning("[PartyMemberEntryUIController] No Button found — add one to the prefab or assign _clickButton.");
        }

        private void OnEnable()
        {
            GameEventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        public void Setup(BaseUnit unit, Color frameColor)
        {
            _unit       = unit;
            _definition = (unit as PlayerUnit)?.Definition;

            if (_backgroundFrame != null)
                _backgroundFrame.color = frameColor;

            SetActive(false);
            RefreshAll();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetActive(bool isActive)
        {
            if (_activeHighlight != null)
                _activeHighlight.SetActive(isActive);
        }

        public void RefreshBars()
        {
            if (_unit == null) return;
            var state = _unit.RuntimeState;
            var stats = _unit.Stats;

            SetSlider(_hpSlider,        state.CurrentHP,            stats.MaxHP);
            SetSlider(_physArmorSlider, state.CurrentPhysicalArmor, stats.MaxPhysicalArmor);
            SetSlider(_specArmorSlider, state.CurrentSpecialArmor,  stats.MaxSpecialArmor);
        }

        // ── Portrait (same emotion logic as PlayerPortraitUI) ─────────────────

        private void RefreshPortrait()
        {
            if (_portraitImage == null || _unit == null || _definition == null) return;

            var state = _unit.RuntimeState;
            var stats = _unit.Stats;
            float pct = stats.MaxHP > 0f ? state.CurrentHP / stats.MaxHP : 0f;

            PortraitEmotion emotion;
            if (pct <= 0f)        emotion = PortraitEmotion.Dizzy;
            else if (pct < 0.25f) emotion = PortraitEmotion.Stunned;
            else if (pct < 0.50f) emotion = PortraitEmotion.Pain;
            else if (pct < 0.75f) emotion = PortraitEmotion.Worried;
            else                  emotion = PortraitEmotion.Normal;

            var sprite = _definition.GetPortrait(emotion);
            if (sprite != null)
                _portraitImage.sprite = sprite;
        }

        private void RefreshAll()
        {
            RefreshPortrait();
            RefreshBars();
        }

        // ── Click ─────────────────────────────────────────────────────────────

        // Called by Button.onClick (wired in Awake) AND by IPointerClickHandler as fallback
        public void SwitchToThisUnit()
        {
            if (_unit == null) return;
            GameEventBus.Publish(new ActiveUnitChangedEvent { UnitId = _unit.UnitId });
            var cam = FindAnyObjectByType<OverworldCameraController>();
            cam?.SetTarget(_unit.transform);
        }

        public void OnPointerClick(PointerEventData eventData) => SwitchToThisUnit();

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_unit == null || evt.DefenderUnitId != _unit.UnitId) return;
            RefreshAll();
        }

        private void OnTurnStarted(TurnStartedEvent _) => RefreshAll();

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetSlider(Slider slider, float current, float max)
        {
            if (slider == null) return;
            slider.value = max > 0f ? current / max : 0f;
        }
    }
}
