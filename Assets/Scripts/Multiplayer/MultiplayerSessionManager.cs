using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Units;

namespace PokemonAdventure.Multiplayer
{
    // ==========================================================================
    // Multiplayer Session Manager
    // Manages the 4-player cooperative session data. Authority model:
    //   - Host (Slot 0) is the authoritative game state source.
    //   - Clients (Slots 1–3) send input intents; host validates and rebroadcasts.
    //
    // Current state: Architecture scaffolding only. No real netcode wired.
    // Ready to integrate with Unity Netcode for GameObjects (NGO) or Fishnet.
    //
    // TODO: Implement INetworkSessionAdapter interface once transport is chosen.
    // TODO: Implement state synchronisation for RuntimeUnitState.
    // TODO: Handle client disconnection and AI-fill takeover.
    // ==========================================================================

    public class MultiplayerSessionManager : MonoBehaviour
    {
        [Header("Session Settings")]
        [Tooltip("Max number of concurrent players (including host).")]
        [SerializeField] private int _maxPlayers = PlayerSlot.MaxSlots;

        [Tooltip("If true, empty slots are filled by AI companions.")]
        [SerializeField] private bool _fillEmptySlotsWithAI = true;

        [Header("Debug")]
        [SerializeField] private bool _simulateLocalMultiplayer; // For offline testing

        // ── Slot Data ─────────────────────────────────────────────────────────

        private readonly PlayerSlot[] _slots = new PlayerSlot[PlayerSlot.MaxSlots];

        public IReadOnlyList<PlayerSlot> Slots => _slots;

        // ── Queries ───────────────────────────────────────────────────────────

        public bool IsHost { get; private set; } = true; // Offline default: always host

        public int   FilledSlotCount => _slots.Count(s => s.IsFilled);
        public int   OpenSlotCount   => _slots.Count(s => s.IsOpen);
        public bool  SessionFull     => FilledSlotCount >= _maxPlayers;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            for (int i = 0; i < PlayerSlot.MaxSlots; i++)
                _slots[i] = PlayerSlot.CreateOpenSlot(i);
        }

        private void Start()
        {
            // Local host always occupies Slot 0
            FillSlotAsLocalPlayer(0, "Player 1");

            if (_fillEmptySlotsWithAI)
            {
                for (int i = 1; i < _maxPlayers; i++)
                    FillSlotWithAI(i);
            }
        }

        // ── Slot Management ───────────────────────────────────────────────────

        public bool TryAddPlayer(string networkId, string playerName, out PlayerSlot slot)
        {
            slot = _slots.FirstOrDefault(s => s.IsOpen);
            if (slot == null)
            {
                Debug.LogWarning("[MultiplayerSessionManager] Session is full. Cannot add player.");
                return false;
            }

            slot.Status          = PlayerSlotStatus.Filled;
            slot.NetworkPlayerId = networkId;
            slot.PlayerName      = playerName;

            Debug.Log($"[MultiplayerSessionManager] Player '{playerName}' joined slot {slot.SlotIndex}.");
            return true;
        }

        public void RemovePlayer(string networkId)
        {
            var slot = _slots.FirstOrDefault(s => s.NetworkPlayerId == networkId);
            if (slot == null) return;

            Debug.Log($"[MultiplayerSessionManager] Player '{slot.PlayerName}' disconnected from slot {slot.SlotIndex}.");

            if (_fillEmptySlotsWithAI)
                FillSlotWithAI(slot.SlotIndex);
            else
                slot.Status = PlayerSlotStatus.Open;
        }

        public PlayerSlot GetSlotForUnit(BaseUnit unit) =>
            _slots.FirstOrDefault(s => s.ActiveUnit == unit);

        public PlayerSlot GetSlotByIndex(int index) =>
            index >= 0 && index < _slots.Length ? _slots[index] : null;

        // ── Authority Checks ──────────────────────────────────────────────────

        /// <summary>
        /// In an authoritative multiplayer model, only the host should execute
        /// game-state-altering logic. Check this before modifying shared state.
        /// </summary>
        public bool CanAuthorise(PlayerSlot slot) =>
            IsHost || slot?.SlotIndex == 0;

        // ── Internal ─────────────────────────────────────────────────────────

        private void FillSlotAsLocalPlayer(int index, string name)
        {
            _slots[index].Status    = PlayerSlotStatus.Filled;
            _slots[index].PlayerName = name;
            _slots[index].NetworkPlayerId = "local";
        }

        private void FillSlotWithAI(int index)
        {
            _slots[index].Status    = PlayerSlotStatus.AIFilled;
            _slots[index].PlayerName = $"Companion {index}";
            // TODO: Spawn AI companion unit and assign to slot.ActiveUnit
        }
    }
}
