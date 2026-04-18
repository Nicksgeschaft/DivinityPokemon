using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Combat;

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
        [Tooltip("World-space Y offset above the unit's position.")]
        [SerializeField] private float _yOffset    = 1.8f;
        [SerializeField] private int   _poolSize   = 12;

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
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
        }

        // ── Event Handler ─────────────────────────────────────────────────────

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            if (_textPrefab == null) return;
            if (_unitRegistry == null) return;
            if (evt.FinalDamage <= 0 && evt.HPDamage <= 0 && evt.ArmorAbsorbed <= 0) return;

            if (!_unitRegistry.TryGet(evt.DefenderUnitId, out var defender)) return;

            var pos = defender.WorldPosition + Vector3.up * _yOffset;
            GetPooled()?.ShowDamage(evt.HPDamage, evt.ArmorAbsorbed, evt.Effectiveness, pos);
        }

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
