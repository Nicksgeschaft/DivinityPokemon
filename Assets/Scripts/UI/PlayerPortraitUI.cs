using UnityEngine;
using UnityEngine.UI;
using PokemonAdventure.Core;
using PokemonAdventure.Units;
using PokemonAdventure.ScriptableObjects;

namespace PokemonAdventure.UI
{
    // Displays the tracked unit's portrait and swaps emotion sprite based on HP:
    //   >= 75 % → Normal  |  >= 50 % → Worried  |  >= 25 % → Pain
    //   >  0 % → Stunned  |  0 %     → Dizzy (KO)
    public class PlayerPortraitUI : MonoBehaviour
    {
        [SerializeField] private Image _portraitImage;

        private BaseUnit       _trackedUnit;
        private PokemonDefinition _definition;

        // ── Lifecycle ─────────────────────────────────────────────────────────

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

        // ── Public API ────────────────────────────────────────────────────────

        public void SetUnit(BaseUnit unit)
        {
            _trackedUnit = unit;
            _definition  = (unit as PlayerUnit)?.Definition;
            RefreshPortrait();
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        public void RefreshPortrait()
        {
            if (_portraitImage == null) return;
            _portraitImage.enabled = true;

            if (_trackedUnit == null || _definition == null) return;

            var state = _trackedUnit.RuntimeState;
            var stats = _trackedUnit.Stats;
            float pct  = stats.MaxHP > 0f ? state.CurrentHP / stats.MaxHP : 0f;

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

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_trackedUnit == null || evt.DefenderUnitId != _trackedUnit.UnitId) return;
            RefreshPortrait();
        }

        private void OnTurnStarted(TurnStartedEvent _) => RefreshPortrait();
    }
}
