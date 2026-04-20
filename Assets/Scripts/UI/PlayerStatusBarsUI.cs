using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // Displays HP / PhysArmor / SpecArmor for the tracked unit.
    // Armor bars are hidden outside combat and shown in combat.
    public class PlayerStatusBarsUI : MonoBehaviour
    {
        [Header("HP Bar")]
        [SerializeField] private Slider          _hpSlider;
        [SerializeField] private TextMeshProUGUI _hpText;

        [Header("Physical Armor Bar")]
        [SerializeField] private Slider          _physArmorSlider;
        [SerializeField] private TextMeshProUGUI _physArmorText;

        [Header("Special Armor Bar")]
        [SerializeField] private Slider          _specArmorSlider;
        [SerializeField] private TextMeshProUGUI _specArmorText;

        [Header("Visibility")]
        [Tooltip("ArmorRow root — hidden outside combat, shown during combat.")]
        [SerializeField] private GameObject _armorRowRoot;

        [Header("Status Effects")]
        [Tooltip("StatusIconStrip child panel. Optional — leave empty to skip.")]
        [SerializeField] private StatusIconStrip _statusIconStrip;

        private BaseUnit _trackedUnit;

        // ── Public API ────────────────────────────────────────────────────────

        public void SetUnit(BaseUnit unit)
        {
            _trackedUnit = unit;
            _statusIconStrip?.SetUnit(unit);
            Refresh();
        }

        public void SetCombatMode(bool inCombat)
        {
            if (_armorRowRoot != null)
                _armorRowRoot.SetActive(inCombat);
        }

        public void Refresh()
        {
            if (_trackedUnit == null) return;

            var state = _trackedUnit.RuntimeState;
            var stats = _trackedUnit.Stats;

            // HP
            SetBar(_hpSlider, _hpText,
                state.CurrentHP, stats.MaxHP);

            // Physical Armor
            SetBar(_physArmorSlider, _physArmorText,
                state.CurrentPhysicalArmor, stats.MaxPhysicalArmor);

            // Special Armor
            SetBar(_specArmorSlider, _specArmorText,
                state.CurrentSpecialArmor, stats.MaxSpecialArmor);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetBar(Slider slider, TextMeshProUGUI label, float current, float max)
        {
            if (slider != null)
                slider.value = max > 0f ? current / max : 0f;

            if (label != null)
                label.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        }
    }
}
