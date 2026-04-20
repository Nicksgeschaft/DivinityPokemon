using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // Displays active status effects for one unit as colored text labels.
    // Attach to any RectTransform inside a Canvas (Screen Space or World Space).
    // Call SetUnit() to bind. Self-assembles slots at runtime — no child prefab needed.
    //
    // Slot layout: up to (_maxVisible - 1) named icons, last slot shows "+N" overflow.
    // Each icon shows a 3-letter abbreviation + remaining turns below.
    public class StatusIconStrip : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Max slots shown. Last slot becomes overflow '+N' when effects exceed this.")]
        [SerializeField] private int   _maxVisible = 5;
        [SerializeField] private float _slotWidth  = 34f;
        [SerializeField] private float _slotHeight = 22f;
        [SerializeField] private int   _fontSize   = 9;
        [SerializeField] private float _spacing    = 2f;

        // ── State ─────────────────────────────────────────────────────────────

        private BaseUnit _unit;
        private string   _unitId;
        private bool     _subscribed;

        private readonly List<TextMeshProUGUI> _slots = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            var hlg = GetComponent<HorizontalLayoutGroup>()
                   ?? gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing              = _spacing;
            hlg.childAlignment       = TextAnchor.MiddleLeft;
            hlg.childControlWidth    = false;
            hlg.childControlHeight   = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;

            BuildSlots();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetUnit(BaseUnit unit)
        {
            _unit   = unit;
            _unitId = unit?.UnitId;
            Rebuild();
        }

        // ── Event Subscriptions ───────────────────────────────────────────────

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameEventBus.Subscribe<UnitStatusAppliedEvent>(OnStatusApplied);
            GameEventBus.Subscribe<UnitStatusRemovedEvent>(OnStatusRemoved);
            GameEventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            GameEventBus.Unsubscribe<UnitStatusAppliedEvent>(OnStatusApplied);
            GameEventBus.Unsubscribe<UnitStatusRemovedEvent>(OnStatusRemoved);
            GameEventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnStatusApplied(UnitStatusAppliedEvent evt)
        {
            if (evt.UnitId == _unitId) Rebuild();
        }

        private void OnStatusRemoved(UnitStatusRemovedEvent evt)
        {
            if (evt.UnitId == _unitId) Rebuild();
        }

        private void OnUnitDied(UnitDiedEvent evt)
        {
            if (evt.UnitId == _unitId) ClearSlots();
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState == GameState.Overworld) ClearSlots();
        }

        // ── Slot Construction ─────────────────────────────────────────────────

        private void BuildSlots()
        {
            for (int i = 0; i < _maxVisible; i++)
            {
                var go = new GameObject($"StatusSlot_{i}", typeof(RectTransform));
                go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(_slotWidth, _slotHeight);

                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize           = _fontSize;
                tmp.alignment          = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.overflowMode       = TextOverflowModes.Overflow;

                go.SetActive(false);
                _slots.Add(tmp);
            }
        }

        // ── Rebuild / Clear ───────────────────────────────────────────────────

        private void Rebuild()
        {
            ClearSlots();
            if (_unit == null) return;

            var effects = _unit.RuntimeState.ActiveStatusEffects;
            int total   = effects.Count;
            if (total == 0) return;

            for (int i = 0; i < _maxVisible && i < total; i++)
            {
                var slot      = _slots[i];
                bool overflow = i == _maxVisible - 1 && total > _maxVisible;

                slot.gameObject.SetActive(true);

                if (overflow)
                {
                    slot.text  = $"+{total - (_maxVisible - 1)}";
                    slot.color = Color.white;
                }
                else
                {
                    var effect = effects[i];
                    slot.text  = FormatLabel(effect);
                    slot.color = ColorFor(effect.EffectType);
                }
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in _slots)
                slot.gameObject.SetActive(false);
        }

        // ── Formatting ────────────────────────────────────────────────────────

        private static string FormatLabel(StatusEffectInstance effect)
        {
            string abbrev = Abbrev(effect.EffectType);
            return effect.RemainingTurns < 0
                ? abbrev
                : $"{abbrev}\n{effect.RemainingTurns}t";
        }

        private static string Abbrev(StatusEffectType t) => t switch
        {
            StatusEffectType.Burn       => "BRN",
            StatusEffectType.Freeze     => "FRZ",
            StatusEffectType.Paralysis  => "PAR",
            StatusEffectType.Poison     => "PSN",
            StatusEffectType.BadPoison  => "TOX",
            StatusEffectType.Sleep      => "SLP",
            StatusEffectType.Confusion  => "CNF",
            StatusEffectType.Flinch     => "FLN",
            StatusEffectType.Blind      => "BLD",
            StatusEffectType.Taunt      => "TNT",
            StatusEffectType.Cursed     => "CRS",
            StatusEffectType.Blessed    => "BLS",
            StatusEffectType.Rooted     => "ROT",
            StatusEffectType.Silenced   => "SIL",
            StatusEffectType.Stunned    => "STN",
            StatusEffectType.Wet        => "WET",
            StatusEffectType.Oiled      => "OIL",
            _                           => "???"
        };

        private static Color ColorFor(StatusEffectType t) => t switch
        {
            StatusEffectType.Burn                                           => new Color(1.0f, 0.35f, 0.1f),
            StatusEffectType.Freeze                                         => new Color(0.2f, 0.80f, 1.0f),
            StatusEffectType.Paralysis                                      => new Color(0.9f, 0.80f, 0.1f),
            StatusEffectType.Poison or StatusEffectType.BadPoison           => new Color(0.7f, 0.20f, 0.9f),
            StatusEffectType.Sleep                                          => new Color(0.3f, 0.50f, 0.9f),
            StatusEffectType.Stunned or StatusEffectType.Flinch             => new Color(1.0f, 0.60f, 0.1f),
            StatusEffectType.Confusion                                      => new Color(0.9f, 0.30f, 0.8f),
            StatusEffectType.Cursed                                         => new Color(0.4f, 0.10f, 0.6f),
            StatusEffectType.Blessed                                        => new Color(0.9f, 0.90f, 0.3f),
            _                                                               => new Color(0.7f, 0.70f, 0.7f)
        };
    }
}
