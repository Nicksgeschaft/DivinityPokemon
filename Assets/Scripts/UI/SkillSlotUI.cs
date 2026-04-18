using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PokemonAdventure.Core;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // One skill slot in the skill bar.
    // Manages background sprite, ability icon, hotkey label, selected highlight,
    // and a radial clock-style cooldown overlay that ticks down turn by turn.
    [RequireComponent(typeof(Button))]
    public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Slot Visuals")]
        [SerializeField] private Image           _slotBackground;
        [SerializeField] private Image           _abilityIcon;
        [SerializeField] private TextMeshProUGUI _hotkeyText;

        [Header("Cooldown Overlay")]
        [Tooltip("Full-slot Image set to Radial360 / Clockwise fill. Sits above icon.")]
        [SerializeField] private Image           _cooldownOverlay;
        [Tooltip("TMP label centred on the slot showing remaining turns.")]
        [SerializeField] private TextMeshProUGUI _cooldownText;

        [Header("Selected State")]
        [SerializeField] private Color _defaultTint  = Color.white;
        [SerializeField] private Color _selectedTint = new Color(1f, 0.92f, 0.4f, 1f);

        public int             SlotIndex     { get; private set; }
        public SkillDefinition AssignedSkill { get; private set; }

        private int _cooldownMax;

        public event System.Action<SkillSlotUI> OnSlotClicked;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            var btn = GetComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked?.Invoke(this));
            ClearCooldown();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Init(int slotIndex, KeyCode hotkey)
        {
            SlotIndex = slotIndex;
            if (_hotkeyText != null)
                _hotkeyText.text = HotkeyLabel(hotkey);
        }

        public void SetSkill(SkillDefinition skill, Sprite backgroundSprite)
        {
            AssignedSkill = skill;
            _cooldownMax  = skill?.Cooldown ?? 0;

            if (_slotBackground != null && backgroundSprite != null)
                _slotBackground.sprite = backgroundSprite;

            if (_abilityIcon != null)
            {
                _abilityIcon.sprite  = skill?.SkillIcon;
                _abilityIcon.enabled = skill?.SkillIcon != null;
            }

            ClearCooldown();
        }

        public void SetSelected(bool selected)
        {
            if (_slotBackground != null)
                _slotBackground.color = selected ? _selectedTint : _defaultTint;
        }

        /// <summary>
        /// Refreshes the clock-style cooldown overlay.
        /// Call with <paramref name="remaining"/> = 0 (or negative) to hide the overlay.
        /// </summary>
        public void RefreshCooldown(int remaining, int maxCooldown)
        {
            if (remaining <= 0 || maxCooldown <= 0)
            {
                ClearCooldown();
                return;
            }

            _cooldownMax = maxCooldown;

            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.gameObject.SetActive(true);
                // fillAmount 1 = full (skill just used); 0 = ready
                _cooldownOverlay.fillAmount = (float)remaining / maxCooldown;
            }

            if (_cooldownText != null)
            {
                _cooldownText.gameObject.SetActive(true);
                _cooldownText.text = remaining.ToString();
            }
        }

        public void ClearCooldown()
        {
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = 0f;
                _cooldownOverlay.gameObject.SetActive(false);
            }

            if (_cooldownText != null)
            {
                _cooldownText.gameObject.SetActive(false);
                _cooldownText.text = string.Empty;
            }
        }

        // ── Pointer Events (overworld skill preview) ──────────────────────────

        public void OnPointerEnter(PointerEventData _)
        {
            if (AssignedSkill == null) return;
            var caster = FindAnyObjectByType<PlayerUnit>();
            GameEventBus.Publish(new SkillPreviewEvent
            {
                CasterUnitId = caster != null ? caster.UnitId : string.Empty,
                SkillId      = AssignedSkill.SkillId,
            });
        }

        public void OnPointerExit(PointerEventData _)
        {
            GameEventBus.Publish(new SkillPreviewEvent { SkillId = string.Empty });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string HotkeyLabel(KeyCode key) => key switch
        {
            KeyCode.Alpha1 => "1", KeyCode.Alpha2 => "2", KeyCode.Alpha3 => "3",
            KeyCode.Alpha4 => "4", KeyCode.Alpha5 => "5", KeyCode.Alpha6 => "6",
            KeyCode.Alpha7 => "7", KeyCode.Alpha8 => "8", KeyCode.Alpha9 => "9",
            KeyCode.Alpha0 => "0",
            _ => key.ToString()
        };
    }
}
