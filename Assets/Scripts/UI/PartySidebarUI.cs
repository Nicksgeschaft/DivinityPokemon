using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;
using PokemonAdventure.Multiplayer;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // ==========================================================================
    // Party Sidebar UI
    // Left party sidebar (Divinity-style).
    //
    // Finds all PlayerUnit instances in the scene, groups them by PlayerSlot
    // if a MultiplayerSessionManager is present, otherwise puts all in one group.
    // One OwnerGroupUI per human player, one PartyMemberEntryUIController per unit.
    // ==========================================================================

    [Serializable]
    public class TypeColorEntry
    {
        public PokemonType Type;
        public Color       Color;
    }

    public class PartySidebarUI : MonoBehaviour
    {
        [Header("Hierarchy")]
        [Tooltip("PartyGroupContainer — OwnerGroupContainers are spawned here.")]
        [SerializeField] private Transform  _partyGroupContainer;

        [Tooltip("OwnerGroupContainer prefab (has OwnerGroupUI component).")]
        [SerializeField] private GameObject _ownerGroupPrefab;

        [Tooltip("PartyMemberEntryUI prefab (has PartyMemberEntryUIController component).")]
        [SerializeField] private GameObject _entryPrefab;

        [Header("Type → Portrait Frame Color")]
        [SerializeField] private TypeColorEntry[] _typeColors;
        [SerializeField] private Color _defaultFrameColor = Color.white;

        // ── Runtime ───────────────────────────────────────────────────────────

        private readonly List<OwnerGroupUI> _groups = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Subscribe in Awake so we never miss events fired in Start()
            GameEventBus.Subscribe<UnitRegisteredEvent>(OnUnitRegistered);
            GameEventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Subscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);
            // DamageDealt is handled by each PartyMemberEntryUIController directly
        }

        private void Start()
        {
            StartCoroutine(BuildNextFrame());
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<UnitRegisteredEvent>(OnUnitRegistered);
            GameEventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            GameEventBus.Unsubscribe<ActiveUnitChangedEvent>(OnActiveUnitChanged);
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private IEnumerator BuildNextFrame()
        {
            yield return null;
            RebuildSidebar();
        }

        private void RebuildSidebar()
        {
            foreach (var g in _groups)
                if (g != null) Destroy(g.gameObject);
            _groups.Clear();

            // Find all live PlayerUnit instances — same approach as BottomHudController
            var allPlayerUnits = FindObjectsByType<PlayerUnit>(FindObjectsSortMode.None);
            if (allPlayerUnits.Length == 0)
            {
                Debug.Log("[PartySidebarUI] No PlayerUnit instances found in scene.");
                return;
            }

            // Try to group by PlayerSlot via MultiplayerSessionManager
            MultiplayerSessionManager session = null;
            ServiceLocator.TryGet(out session);

            if (session != null)
                BuildGroupedBySessions(allPlayerUnits, session);
            else
                BuildSingleGroup("Party", allPlayerUnits);

            // Set the first entry as the default active character
            ActivateFirstEntry();
        }

        private void ActivateFirstEntry()
        {
            foreach (var group in _groups)
            {
                if (group == null || group.Entries.Count == 0) continue;
                var first = group.Entries[0];
                if (first?.Unit == null) continue;

                GameEventBus.Publish(new ActiveUnitChangedEvent { UnitId = first.Unit.UnitId });
                return;
            }
        }

        private void BuildGroupedBySessions(PlayerUnit[] units, MultiplayerSessionManager session)
        {
            // Bucket units by slot index
            var buckets = new Dictionary<int, (string name, List<PlayerUnit> units)>();

            foreach (var unit in units)
            {
                var slot = session.GetSlotForUnit(unit);
                int idx  = slot?.SlotIndex ?? 0;
                string name = slot?.PlayerName ?? "Player 1";

                if (!buckets.ContainsKey(idx))
                    buckets[idx] = (name, new List<PlayerUnit>());
                buckets[idx].units.Add(unit);
            }

            // Create one OwnerGroupUI per bucket, sorted by slot index
            var sortedKeys = new List<int>(buckets.Keys);
            sortedKeys.Sort();

            foreach (var key in sortedKeys)
            {
                var (name, slotUnits) = buckets[key];
                SpawnGroup(name, slotUnits);
            }
        }

        private void BuildSingleGroup(string label, PlayerUnit[] units)
        {
            SpawnGroup(label, new List<PlayerUnit>(units));
        }

        private void SpawnGroup(string ownerName, List<PlayerUnit> units)
        {
            if (_ownerGroupPrefab == null || _partyGroupContainer == null) return;

            var go      = Instantiate(_ownerGroupPrefab, _partyGroupContainer);
            var groupUI = go.GetComponent<OwnerGroupUI>();
            if (groupUI == null) return;

            groupUI.Setup(ownerName, _entryPrefab);
            foreach (var unit in units)
                groupUI.AddEntry(unit, GetFrameColor(unit));

            _groups.Add(groupUI);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private bool _rebuildPending;

        private void OnUnitRegistered(UnitRegisteredEvent evt)
        {
            if (evt.Faction != UnitFaction.Friendly) return;
            if (_rebuildPending) return;
            _rebuildPending = true;
            StartCoroutine(RebuildAfterFrame());
        }

        private IEnumerator RebuildAfterFrame()
        {
            yield return null;
            _rebuildPending = false;
            RebuildSidebar();
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            foreach (var g in _groups)
                g?.SetActiveUnit(evt.ActiveUnitId);
        }

        private void OnActiveUnitChanged(ActiveUnitChangedEvent evt)
        {
            foreach (var g in _groups)
                g?.SetActiveUnit(evt.UnitId);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Color GetFrameColor(PlayerUnit unit)
        {
            if (unit.Definition == null) return _defaultFrameColor;
            var type = unit.Definition.PrimaryType;
            if (_typeColors != null)
                foreach (var entry in _typeColors)
                    if (entry.Type == type) return entry.Color;
            return _defaultFrameColor;
        }
    }
}
