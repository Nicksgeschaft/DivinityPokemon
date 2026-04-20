using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PokemonAdventure.Units;

namespace PokemonAdventure.UI
{
    // One ownership group (= one human player's characters) in the party sidebar.
    // Attach to the OwnerGroupContainer prefab root.
    // Wire OwnerHeader TMP label and EntriesContainer transform via Inspector.
    public class OwnerGroupUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _ownerHeaderLabel;
        [SerializeField] private Transform       _entriesContainer;

        private readonly List<PartyMemberEntryUIController> _entries = new();
        private GameObject _entryPrefab;

        // ── Setup ─────────────────────────────────────────────────────────────

        public void Setup(string ownerName, GameObject entryPrefab)
        {
            _entryPrefab = entryPrefab;
            if (_ownerHeaderLabel != null)
                _ownerHeaderLabel.text = ownerName;
        }

        public PartyMemberEntryUIController AddEntry(BaseUnit unit, Color frameColor)
        {
            if (_entryPrefab == null || _entriesContainer == null) return null;

            var go    = Instantiate(_entryPrefab, _entriesContainer);
            var entry = go.GetComponent<PartyMemberEntryUIController>();
            if (entry != null)
            {
                entry.Setup(unit, frameColor);
                _entries.Add(entry);
            }
            return entry;
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        public void RefreshAll()
        {
            foreach (var e in _entries)
                e?.RefreshBars();
        }

        public void SetActiveUnit(string unitId)
        {
            foreach (var e in _entries)
                e?.SetActive(e.Unit?.UnitId == unitId);
        }

        public IReadOnlyList<PartyMemberEntryUIController> Entries => _entries;
    }
}
