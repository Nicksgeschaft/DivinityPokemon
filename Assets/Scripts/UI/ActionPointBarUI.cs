using UnityEngine;
using UnityEngine.UI;
using PokemonAdventure.Core;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // Displays the tracked unit's Action Points as 6 icon slots (Green = full, Gray = empty).
    // Sprite references point to Assets/SkillIconPackage/buttons/concept03/Green.PNG and Gray.PNG.
    public class ActionPointBarUI : MonoBehaviour
    {
        [Header("AP Slot Images (left to right, 6 total)")]
        [SerializeField] private Image[] _apSlotImages = new Image[RuntimeUnitState.MaxAPCap];

        [Header("Sprites (optional — color tinting works without sprites)")]
        [Tooltip("concept03/Green.PNG — available AP")]
        [SerializeField] private Sprite _fullSprite;
        [Tooltip("concept03/Gray.PNG — unavailable AP")]
        [SerializeField] private Sprite _emptySprite;

        [Header("Colors")]
        [SerializeField] private Color _filledColor = new Color(0.20f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color _emptyColor  = new Color(0.28f, 0.28f, 0.28f, 0.60f);

        private BaseUnit _trackedUnit;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            GameEventBus.Subscribe<APChangedEvent>(OnAPChanged);
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            Refresh(); // Show current AP immediately when the bar becomes visible
        }

        private void OnDisable()
        {
            GameEventBus.Unsubscribe<APChangedEvent>(OnAPChanged);
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetUnit(BaseUnit unit)
        {
            _trackedUnit = unit;
            Refresh();
        }

        public void Refresh()
        {
            int ap = _trackedUnit != null ? _trackedUnit.RuntimeState.CurrentAP : 0;
            ApplyDisplay(ap);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnAPChanged(APChangedEvent evt)
        {
            if (_trackedUnit == null || evt.UnitId != _trackedUnit.UnitId) return;
            ApplyDisplay(evt.NewAP);
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (_trackedUnit == null || evt.ActiveUnitId != _trackedUnit.UnitId) return;
            Refresh();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void ApplyDisplay(int currentAP)
        {
            for (int i = 0; i < _apSlotImages.Length; i++)
            {
                if (_apSlotImages[i] == null) continue;
                bool filled = i < currentAP;

                // Color always applied — works even without sprite assets
                _apSlotImages[i].color = filled ? _filledColor : _emptyColor;

                // Sprite swap only when assets are assigned
                if (_fullSprite != null || _emptySprite != null)
                    _apSlotImages[i].sprite = filled ? _fullSprite : _emptySprite;
            }
        }
    }
}
