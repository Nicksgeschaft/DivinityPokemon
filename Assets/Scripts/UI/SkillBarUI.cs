using System;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    [Serializable]
    public class TypeBackground
    {
        public PokemonType Type;
        public Sprite      Background;
    }

    // Manages all 10 skill slots: populates from unit definition, handles
    // hotkey input (1-0) and click selection, drives background type colouring,
    // and refreshes per-slot cooldown overlays on turn transitions.
    public class SkillBarUI : MonoBehaviour
    {
        [Header("Slot References (Slot_01 → Slot_10)")]
        [SerializeField] private SkillSlotUI[] _slots = new SkillSlotUI[10];

        [Header("Type → Background Mapping")]
        [Tooltip("Map each PokemonType to its background sprite from concept01/.")]
        [SerializeField] private TypeBackground[] _typeBackgrounds;

        [Tooltip("Fallback background when type is None/Normal or no skill is assigned.")]
        [SerializeField] private Sprite _defaultBackground;

        // ── Private State ─────────────────────────────────────────────────────

        private IPlayerInput     _input;
        private GameStateManager _stateManager;
        private int              _selectedIndex = -1;
        private BaseUnit         _trackedUnit;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private static readonly KeyCode[] _displayHotkeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
            KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.Alpha0
        };

        private void Awake()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].Init(i, _displayHotkeys[i]);
                _slots[i].OnSlotClicked += HandleSlotClicked;
            }
        }

        private void Start()
        {
            _input        = ServiceLocator.Get<IPlayerInput>();
            _stateManager = ServiceLocator.Get<GameStateManager>();
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<ActionExecutedEvent>(OnActionExecuted);
            GameEventBus.Subscribe<SkillTargetingConfirmedEvent>(OnTargetingConfirmed);
            GameEventBus.Subscribe<WorldTickEvent>(OnWorldTick);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<ActionExecutedEvent>(OnActionExecuted);
            GameEventBus.Unsubscribe<SkillTargetingConfirmedEvent>(OnTargetingConfirmed);
            GameEventBus.Unsubscribe<WorldTickEvent>(OnWorldTick);
        }

        private void Update()
        {
            if (_input == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_input.GetSkillSlotPressed(i))
                    SelectSlot(i);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetUnit(BaseUnit unit)
        {
            _trackedUnit = unit;
            PopulateSkills();
            RefreshCooldowns();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void PopulateSkills()
        {
            var playerUnit = _trackedUnit as PlayerUnit;
            var skills     = playerUnit?.Definition?.StartingSkills;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;

                bool hasSkill = skills != null && i < skills.Count && skills[i] != null;

                if (hasSkill)
                {
                    // Prefer per-skill background; fall back to type-based colour
                    var bg = skills[i].SkillBarBackground != null
                        ? skills[i].SkillBarBackground
                        : GetBackground(skills[i].SkillType);
                    _slots[i].SetSkill(skills[i], bg);
                }
                else
                {
                    _slots[i].SetSkill(null, _defaultBackground);
                }

                _slots[i].SetSelected(i == _selectedIndex);
            }
        }

        private void RefreshCooldowns()
        {
            if (_trackedUnit == null) return;
            var cooldowns = _trackedUnit.RuntimeState.SkillCooldowns;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                var skill = _slots[i].AssignedSkill;
                if (skill == null || skill.Cooldown <= 0)
                {
                    _slots[i].ClearCooldown();
                    continue;
                }

                cooldowns.TryGetValue(skill.SkillId, out int remaining);
                _slots[i].RefreshCooldown(remaining, skill.Cooldown);
            }
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return;

            var slot = _slots[index];
            if (slot == null) return;

            // Block selection of empty or on-cooldown slots
            if (slot.AssignedSkill == null) return;
            if (_trackedUnit != null && _trackedUnit.RuntimeState.IsOnCooldown(slot.AssignedSkill.SkillId)) return;

            // Second press on the same slot → deselect and cancel targeting
            if (_selectedIndex == index)
            {
                _slots[index]?.SetSelected(false);
                PublishTargetingCancelled(_slots[index]?.AssignedSkill);
                _selectedIndex = -1;
                return;
            }

            // Cancel previous targeting before switching
            if (_selectedIndex >= 0 && _selectedIndex < _slots.Length)
            {
                _slots[_selectedIndex]?.SetSelected(false);
                PublishTargetingCancelled(_slots[_selectedIndex]?.AssignedSkill);
            }

            _selectedIndex = index;
            _slots[index]?.SetSelected(true);

            var skill = _slots[index]?.AssignedSkill;
            Debug.Log($"[SkillBarUI] Slot {index + 1} selected — " +
                      $"{(skill != null ? skill.SkillName : "empty")}");

            // Start targeting overlay (works in combat and overworld)
            if (skill != null && _trackedUnit != null)
            {
                GameEventBus.Publish(new SkillTargetingStartedEvent
                {
                    CasterUnitId = _trackedUnit.UnitId,
                    SkillId      = skill.SkillId
                });
            }
        }

        private void PublishTargetingCancelled(SkillDefinition skill)
        {
            if (skill == null || _trackedUnit == null) return;
            GameEventBus.Publish(new SkillTargetingCancelledEvent
            {
                CasterUnitId = _trackedUnit.UnitId,
                SkillId      = skill.SkillId
            });
        }

        private void HandleSlotClicked(SkillSlotUI slot) => SelectSlot(slot.SlotIndex);

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnWorldTick(WorldTickEvent evt) => RefreshCooldowns();

        private void OnTargetingConfirmed(SkillTargetingConfirmedEvent evt)
        {
            // Auto-deselect skill slot after casting so the player can move again
            if (_selectedIndex < 0 || _selectedIndex >= _slots.Length) return;
            _slots[_selectedIndex]?.SetSelected(false);
            _selectedIndex = -1;
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (_trackedUnit == null || evt.ActiveUnitId != _trackedUnit.UnitId) return;
            RefreshCooldowns();
        }

        private void OnActionExecuted(ActionExecutedEvent evt)
        {
            if (_trackedUnit == null || evt.ActorUnitId != _trackedUnit.UnitId) return;
            RefreshCooldowns();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Sprite GetBackground(PokemonType type)
        {
            if (_typeBackgrounds != null)
            {
                foreach (var entry in _typeBackgrounds)
                {
                    if (entry.Type == type && entry.Background != null)
                        return entry.Background;
                }
            }
            return _defaultBackground;
        }
    }
}
