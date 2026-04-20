using System;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Grid;

namespace PokemonAdventure.Units
{
    // ==========================================================================
    // Base Unit
    // Abstract MonoBehaviour base class for all units. Contains:
    //   - Identity (ID, name, faction, role)
    //   - UnitStats (base values)
    //   - RuntimeUnitState (mutable in-play state)
    //   - Damage / heal resolution
    //   - Turn hooks
    //
    // Concrete subclasses: PlayerUnit, EnemyUnit, NeutralUnit
    // Driving logic (input / AI) lives in separate Controller classes.
    //
    // IMPORTANT: Do NOT put input reading or AI decision logic here.
    //            This class only represents the entity, not who controls it.
    // ==========================================================================

    public abstract class BaseUnit : MonoBehaviour, IUnit
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Identity")]
        [SerializeField] private string      _displayName = "Unknown Unit";
        [SerializeField] private UnitFaction _faction     = UnitFaction.Neutral;
        [SerializeField] private UnitRole    _role        = UnitRole.None;

        [Header("Base Stats")]
        [SerializeField] protected UnitStats _stats = new();

        [Header("Sight & Stealth")]
        [Tooltip("Sight range in grid cells. Shown as a gizmo.")]
        [SerializeField] private int _baseSightRange  = 8;
        [Tooltip("Higher value makes this unit harder for enemies to spot.")]
        [SerializeField] private int _baseStealthLevel = 0;

        [Header("Debug")]
        [SerializeField] private bool _drawDebugGizmos = true;

        // ── Runtime ───────────────────────────────────────────────────────────

        private RuntimeUnitState _runtimeState;
        private WorldGridManager  _gridManager;
        private string            _unitId;

        // ── IUnit Properties ──────────────────────────────────────────────────

        public string      UnitId       => _unitId;
        public string      DisplayName  => _displayName;
        public UnitFaction Faction      => _faction;
        public UnitRole    Role         => _role;

        /// <summary>
        /// Subclasses call this in their Awake() before base.Awake() to establish
        /// the correct default faction when spawned via AddComponent at runtime.
        /// (Serialized field defaults to Neutral; Inspector values still win on prefabs.)
        /// </summary>
        protected void SetFaction(UnitFaction faction) => _faction = faction;
        protected void SetDisplayName(string name)    => _displayName = name;
        public UnitStats   Stats        => _stats;

        public RuntimeUnitState RuntimeState => _runtimeState;

        public Vector2Int GridPosition  => _runtimeState?.GridPosition ?? Vector2Int.zero;
        public Vector3    WorldPosition => transform.position;
        public bool       IsAlive       => _runtimeState?.IsAlive ?? false;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _unitId       = Guid.NewGuid().ToString("N")[..8]; // Short unique ID
            _runtimeState = new RuntimeUnitState();
            _runtimeState.Initialize(_stats);
            _runtimeState.VisionRange   = _baseSightRange;
            _runtimeState.StealthLevel  = _baseStealthLevel;
        }

        protected virtual void Start()
        {
            _gridManager = ServiceLocator.Get<WorldGridManager>();
            RegisterOnGrid();

            // Register with the unit lookup service so other systems can find us by ID
            ServiceLocator.Get<UnitRegistry>()?.Register(this);
            GameEventBus.Publish(new UnitRegisteredEvent { UnitId = _unitId, Faction = _faction });
        }

        protected virtual void OnDestroy()
        {
            ServiceLocator.Get<UnitRegistry>()?.Unregister(_unitId);
            UnregisterFromGrid();
        }

        // ── Grid Registration ─────────────────────────────────────────────────

        private void RegisterOnGrid()
        {
            if (_gridManager == null) return;
            var cell = _gridManager.GetCellAtWorldPosition(transform.position);
            if (cell == null) return;
            _runtimeState.GridPosition = cell.GridPosition;
            cell.SetOccupied(this);
        }

        private void UnregisterFromGrid()
        {
            if (_gridManager == null) return;
            var cell = _gridManager.GetCell(GridPosition);
            if (cell?.OccupyingUnit == this)
                cell.ClearOccupant();
        }

        // ── IUnit: Damage & Healing ───────────────────────────────────────────

        public virtual void TakeDamage(float amount, DamageType damageType, IUnit source)
        {
            if (!IsAlive || amount <= 0f) return;

            switch (damageType)
            {
                case DamageType.True:
                    ApplyDirect(amount);
                    break;

                case DamageType.Physical:
                    ApplyThroughArmor(amount, ref _runtimeState.CurrentPhysicalArmor);
                    break;

                case DamageType.Special:
                    ApplyThroughArmor(amount, ref _runtimeState.CurrentSpecialArmor);
                    break;

                case DamageType.Healing:
                    Heal(amount);
                    return;
            }

            if (!IsAlive)
                OnDeath(source);
        }

        public virtual void Heal(float amount)
        {
            if (amount <= 0f) return;
            _runtimeState.CurrentHP = Mathf.Min(
                _runtimeState.CurrentHP + amount,
                _stats.MaxHP);
        }

        public virtual void RestorePhysicalArmor(float amount)
        {
            if (amount <= 0f) return;
            _runtimeState.CurrentPhysicalArmor = Mathf.Min(
                _runtimeState.CurrentPhysicalArmor + amount,
                _stats.MaxPhysicalArmor);
        }

        public virtual void RestoreSpecialArmor(float amount)
        {
            if (amount <= 0f) return;
            _runtimeState.CurrentSpecialArmor = Mathf.Min(
                _runtimeState.CurrentSpecialArmor + amount,
                _stats.MaxSpecialArmor);
        }

        // ── IUnit: Status Effects ─────────────────────────────────────────────

        public virtual void ApplyStatusEffect(StatusEffectInstance effect)
        {
            _runtimeState.ApplyStatus(effect);
            GameEventBus.Publish(new UnitStatusAppliedEvent
            {
                UnitId     = UnitId,
                EffectType = effect.EffectType
            });
        }

        public virtual void RemoveStatusEffect(StatusEffectType effectType) =>
            _runtimeState.RemoveStatus(effectType);

        // ── IUnit: Turn Hooks ─────────────────────────────────────────────────

        public virtual void OnTurnStart()
        {
            _runtimeState.GainTurnAP();
            _runtimeState.TickStatusEffects();
            _runtimeState.TickCooldowns();
            _runtimeState.HasActedThisTurn = false;
            _runtimeState.HasMovedThisTurn = false;

            // TODO: Apply on-turn-start status effects (burn damage, regen, etc.)
            //       via a StatusEffectResolver. Keep resolution logic outside BaseUnit.
        }

        public virtual void OnTurnEnd()
        {
            // TODO: Apply on-turn-end effects (delayed bombs, charge completion, etc.)
        }

        // ── Movement ──────────────────────────────────────────────────────────

        /// <summary>
        /// Teleports this unit to the target grid cell, updating occupancy data.
        /// Does NOT validate AP cost — callers (GridMovementHandler) must do that.
        /// </summary>
        public void PlaceOnCell(Vector2Int targetCell)
        {
            if (_gridManager == null) return;

            UnregisterFromGrid();

            _runtimeState.GridPosition = targetCell;
            transform.position = _gridManager.GetWorldPosition(targetCell);

            var cell = _gridManager.GetCell(targetCell);
            cell?.SetOccupied(this);
        }

        /// <summary>
        /// Snaps the visual position to the exact centre of the unit's current grid cell.
        /// Fixes drift caused by movement coroutines interrupted mid-step.
        /// </summary>
        public void SnapToGridPosition()
        {
            if (_gridManager == null) return;
            transform.position = _gridManager.GetWorldPosition(_runtimeState.GridPosition);
        }

        // ── Protected Helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Damage flows through an armor bar first. Overflow carries to HP.
        /// </summary>
        private void ApplyThroughArmor(float damage, ref float armorBar)
        {
            float absorbed = Mathf.Min(armorBar, damage);
            armorBar                   = Mathf.Max(0f, armorBar - absorbed);
            _runtimeState.CurrentHP   = Mathf.Max(0f, _runtimeState.CurrentHP - (damage - absorbed));
        }

        private void ApplyDirect(float damage)
        {
            _runtimeState.CurrentHP = Mathf.Max(0f, _runtimeState.CurrentHP - damage);
        }

        protected virtual void OnDeath(IUnit killer)
        {
            Debug.Log($"[BaseUnit] {DisplayName} ({UnitId}) died.");
            _runtimeState.IsInCombat = false;

            GameEventBus.Publish(new UnitDiedEvent
            {
                UnitId       = UnitId,
                KillerUnitId = killer?.UnitId,
                UnitFaction  = Faction
            });

            // TODO: Trigger death animation, loot drop, remove from turn queue, etc.
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!_drawDebugGizmos) return;

            var gridManager = FindAnyObjectByType<WorldGridManager>();
            float cellSize  = gridManager != null ? gridManager.CellSize : 1f;

            // Faction-coloured sight radius
            Gizmos.color = _faction switch
            {
                UnitFaction.Friendly => new Color(0.1f, 0.9f, 0.1f, 0.12f),
                UnitFaction.Neutral  => new Color(0.9f, 0.9f, 0.0f, 0.12f),
                UnitFaction.Hostile  => new Color(0.9f, 0.1f, 0.1f, 0.12f),
                _                    => new Color(1f,   1f,   1f,   0.12f)
            };
            Gizmos.DrawWireSphere(transform.position, _baseSightRange * cellSize);

            // Unit position indicator
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.4f);
        }
    }
}
