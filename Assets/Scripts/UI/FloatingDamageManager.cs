using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Combat;
using PokemonAdventure.Data;

namespace PokemonAdventure.UI
{
    // ==========================================================================
    // Floating Damage Manager
    // Listens to DamageDealtEvent and spawns pooled floating damage numbers
    // above the defending unit's world position.
    //
    // Setup:
    //   1. Place this on a scene manager GameObject.
    //   2. Assign the FloatingDamageText prefab (a world-space TMP object).
    //   3. Set pool size (10 is enough for most encounters).
    // ==========================================================================

    public class FloatingDamageManager : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("Prefab with FloatingDamageText + TextMeshPro component.")]
        [SerializeField] private FloatingDamageText _textPrefab;

        [Header("Spawn")]
        [Tooltip("World-space offset from the unit's pivot. For top-down: small Y to layer above sprite, e.g. (0, 0.1, -0.5).")]
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 0.1f, -0.5f);
        [SerializeField] private int     _poolSize    = 12;

        // ── Pool ──────────────────────────────────────────────────────────────

        private readonly List<FloatingDamageText> _pool = new();
        private UnitRegistry _unitRegistry;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _unitRegistry = ServiceLocator.Get<UnitRegistry>();

            // Pre-warm pool
            for (int i = 0; i < _poolSize; i++)
            {
                if (_textPrefab == null) break;
                var obj = Instantiate(_textPrefab, transform);
                obj.gameObject.SetActive(false);
                _pool.Add(obj);
            }

            GameEventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Subscribe<UnitStatusAppliedEvent>(OnStatusApplied);
            GameEventBus.Subscribe<UnitStatusRemovedEvent>(OnStatusRemoved);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            GameEventBus.Unsubscribe<UnitStatusAppliedEvent>(OnStatusApplied);
            GameEventBus.Unsubscribe<UnitStatusRemovedEvent>(OnStatusRemoved);
        }

        // ── Event Handler ─────────────────────────────────────────────────────

        private static readonly Color ColorMiss    = new(0.80f, 0.80f, 0.80f, 1f); // light gray
        private static readonly Color ColorBlocked = new(0.40f, 0.70f, 1.00f, 1f); // blue

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_textPrefab == null || _unitRegistry == null) return;
            if (!_unitRegistry.TryGet(evt.DefenderUnitId, out var defender)) return;

            var pos    = defender.WorldPosition + _spawnOffset;
            var pooled = GetPooled();
            if (pooled == null) return;

            if (evt.IsMiss)
            {
                pooled.ShowText("MISS", ColorMiss, pos);
            }
            else if (evt.FinalDamage <= 0 && evt.HPDamage <= 0 && evt.ArmorAbsorbed <= 0)
            {
                pooled.ShowText("BLOCKED", ColorBlocked, pos);
            }
            else
            {
                pooled.ShowDamage(evt.HPDamage, evt.ArmorAbsorbed, evt.Effectiveness, pos);
            }
        }

        private void OnStatusApplied(UnitStatusAppliedEvent evt)
        {
            if (_textPrefab == null || _unitRegistry == null) return;
            if (!_unitRegistry.TryGet(evt.UnitId, out var unit)) return;

            var pooled = GetPooled();
            if (pooled == null) return;

            pooled.ShowText($"+{StatusName(evt.EffectType)}",
                            StatusColor(evt.EffectType),
                            unit.WorldPosition + _spawnOffset);
        }

        private void OnStatusRemoved(UnitStatusRemovedEvent evt)
        {
            if (_textPrefab == null || _unitRegistry == null) return;
            if (!_unitRegistry.TryGet(evt.UnitId, out var unit)) return;

            var pooled = GetPooled();
            if (pooled == null) return;

            // Desaturated version of the status color to signal removal
            var col = Color.Lerp(StatusColor(evt.EffectType), new Color(0.6f, 0.6f, 0.6f), 0.55f);
            pooled.ShowText($"-{StatusName(evt.EffectType)}", col,
                            unit.WorldPosition + _spawnOffset);
        }

        // ── Status Helpers ────────────────────────────────────────────────────

        private static string StatusName(StatusEffectType t) => t switch
        {
            StatusEffectType.Burn       => "Burn",
            StatusEffectType.Freeze     => "Freeze",
            StatusEffectType.Paralysis  => "Paralysis",
            StatusEffectType.Poison     => "Poison",
            StatusEffectType.BadPoison  => "Toxic",
            StatusEffectType.Sleep      => "Sleep",
            StatusEffectType.Confusion  => "Confuse",
            StatusEffectType.Flinch     => "Flinch",
            StatusEffectType.Blind      => "Blind",
            StatusEffectType.Taunt      => "Taunt",
            StatusEffectType.Cursed     => "Curse",
            StatusEffectType.Blessed    => "Blessed",
            StatusEffectType.Rooted     => "Root",
            StatusEffectType.Silenced   => "Silence",
            StatusEffectType.Stunned    => "Stun",
            StatusEffectType.Wet        => "Wet",
            StatusEffectType.Oiled      => "Oil",
            _                           => t.ToString()
        };

        private static Color StatusColor(StatusEffectType t) => t switch
        {
            StatusEffectType.Burn                                                 => new Color(1.0f, 0.35f, 0.1f),
            StatusEffectType.Freeze                                               => new Color(0.2f, 0.80f, 1.0f),
            StatusEffectType.Paralysis                                            => new Color(0.9f, 0.80f, 0.1f),
            StatusEffectType.Poison or StatusEffectType.BadPoison                 => new Color(0.7f, 0.20f, 0.9f),
            StatusEffectType.Sleep                                                => new Color(0.3f, 0.50f, 0.9f),
            StatusEffectType.Stunned or StatusEffectType.Flinch                   => new Color(1.0f, 0.60f, 0.1f),
            StatusEffectType.Confusion                                            => new Color(0.9f, 0.30f, 0.8f),
            StatusEffectType.Cursed                                               => new Color(0.5f, 0.15f, 0.7f),
            StatusEffectType.Blessed                                              => new Color(0.9f, 0.90f, 0.3f),
            _                                                                     => new Color(0.7f, 0.70f, 0.7f)
        };

        // ── Pool Helper ───────────────────────────────────────────────────────

        private FloatingDamageText GetPooled()
        {
            foreach (var t in _pool)
                if (!t.gameObject.activeSelf) return t;

            // Grow pool if needed
            if (_textPrefab == null) return null;
            var obj = Instantiate(_textPrefab, transform);
            obj.gameObject.SetActive(false);
            _pool.Add(obj);
            return obj;
        }
    }
}
