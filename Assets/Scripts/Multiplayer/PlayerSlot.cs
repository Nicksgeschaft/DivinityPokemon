using UnityEngine;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.Units;

namespace PokemonAdventure.Multiplayer
{
    // ==========================================================================
    // Player Slot
    // Represents one of up to 4 player slots in a cooperative session.
    // The host's slot is always index 0. Clients occupy indices 1–3.
    //
    // A slot can be:
    //   - Filled     — a human player is connected and controlling a unit
    //   - Open       — available for another player to join
    //   - AI-filled  — an NPC companion controls the slot (when < 4 humans join)
    // ==========================================================================

    [System.Serializable]
    public class PlayerSlot
    {
        public const int MaxSlots = 4;

        // ── Identity ──────────────────────────────────────────────────────────

        /// <summary>0 = Host, 1–3 = Clients.</summary>
        public int SlotIndex;

        public PlayerSlotStatus Status = PlayerSlotStatus.Open;

        /// <summary>Network player ID assigned by the transport layer (TODO).</summary>
        public string NetworkPlayerId;

        /// <summary>Display name chosen by the player.</summary>
        public string PlayerName = "Player";

        // ── Unit ──────────────────────────────────────────────────────────────

        /// <summary>The Pokémon definition chosen by this player.</summary>
        public PokemonDefinition SelectedPokemon;

        /// <summary>The runtime unit instance in the current scene. Null between scenes.</summary>
        [System.NonSerialized]
        public BaseUnit ActiveUnit;

        // ── Queries ───────────────────────────────────────────────────────────

        public bool IsFilled    => Status == PlayerSlotStatus.Filled;
        public bool IsOpen      => Status == PlayerSlotStatus.Open;
        public bool IsAIFilled  => Status == PlayerSlotStatus.AIFilled;
        public bool IsLocalHost => SlotIndex == 0 && IsFilled;

        // ── Factory ───────────────────────────────────────────────────────────

        public static PlayerSlot CreateOpenSlot(int index) => new()
        {
            SlotIndex = index,
            Status    = PlayerSlotStatus.Open
        };

        public override string ToString() =>
            $"Slot[{SlotIndex}] {Status} | {PlayerName} | {SelectedPokemon?.PokemonName ?? "None"}";
    }

    // ==========================================================================

    public enum PlayerSlotStatus
    {
        Open,       // No player connected
        Filled,     // Human player connected
        AIFilled    // Controlled by AI companion (placeholder for missing player)
    }
}
