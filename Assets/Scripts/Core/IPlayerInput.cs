using UnityEngine;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // IPlayerInput
    // Abstraction layer between Unity's Input System and gameplay code.
    // Concrete implementation: PlayerInputProvider (new Input System).
    // Test/mock implementation: can be swapped for automated testing.
    //
    // IMPORTANT: Only describes RAW intent — validation (AP, range, etc.) is
    // handled by the consuming system, not here.
    // ==========================================================================

    public interface IPlayerInput
    {
        // ── Overworld ─────────────────────────────────────────────────────────

        /// <summary>WASD / left stick — normalised 2D movement direction.</summary>
        Vector2 MoveDirection { get; }

        /// <summary>Camera pan / right stick.</summary>
        Vector2 CameraRotateDirection { get; }

        /// <summary>Interact with NPC / object (E / South button).</summary>
        bool InteractPressed { get; }

        // ── Universal ─────────────────────────────────────────────────────────

        /// <summary>Confirm selection / left click / A button.</summary>
        bool ConfirmPressed { get; }

        /// <summary>Cancel / back / right click / B button.</summary>
        bool CancelPressed { get; }

        /// <summary>Open/close pause menu.</summary>
        bool PausePressed { get; }

        // ── Combat ────────────────────────────────────────────────────────────

        /// <summary>End the active unit's turn voluntarily.</summary>
        bool EndTurnPressed { get; }

        /// <summary>Delay the active unit's turn to end-of-round.</summary>
        bool DelayTurnPressed { get; }

        /// <summary>Activate skill slot 1 (Q / L-bumper).</summary>
        bool Skill1Pressed { get; }

        /// <summary>Activate skill slot 2 (W / R-bumper equivalent).</summary>
        bool Skill2Pressed { get; }

        /// <summary>Activate skill slot 3 (E).</summary>
        bool Skill3Pressed { get; }

        /// <summary>Activate skill slot 4 (R).</summary>
        bool Skill4Pressed { get; }

        /// <summary>
        /// Activate skill bar slot by index (0 = slot 1 … 9 = slot 0).
        /// Maps to keyboard keys 1–0 by default.
        /// </summary>
        bool GetSkillSlotPressed(int slotIndex);

        // ── Cursor / Targeting ────────────────────────────────────────────────

        /// <summary>Current cursor position in screen space.</summary>
        Vector2 CursorScreenPosition { get; }

        /// <summary>Mouse wheel / right-stick Y for camera zoom.</summary>
        float ScrollDelta { get; }

        /// <summary>Cycle to the next party member (Tab).</summary>
        bool SwitchCharacterPressed { get; }
    }
}
