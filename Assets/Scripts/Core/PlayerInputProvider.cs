using UnityEngine;
using UnityEngine.InputSystem;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Player Input Provider
    // Implements IPlayerInput using Unity's new Input System package.
    //
    // Default bindings:
    //   Move/CameraPan — WASD / arrow keys
    //   Interact        — E
    //   Confirm         — Left mouse button / Enter
    //   Cancel          — Right mouse button / Escape
    //   Pause           — Escape
    //   EndTurn         — Space
    //   DelayTurn       — Z
    //   Skill 1–4       — Q / 1 / 2 / R
    //   Skill slots 1–0 — 1 through 0 (GetSkillSlotPressed)
    //   Cursor          — mouse screen position
    //   Scroll          — mouse scroll wheel
    // ==========================================================================

    public class PlayerInputProvider : MonoBehaviour, IPlayerInput
    {
        // ── IPlayerInput — Movement ───────────────────────────────────────────

        public Vector2 MoveDirection
        {
            get
            {
                var kb = Keyboard.current;
                if (kb == null) return Vector2.zero;
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                        - (kb.aKey.isPressed || kb.leftArrowKey.isPressed  ? 1f : 0f);
                float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed    ? 1f : 0f)
                        - (kb.sKey.isPressed || kb.downArrowKey.isPressed   ? 1f : 0f);
                return new Vector2(x, y);
            }
        }

        public Vector2 CameraRotateDirection => Vector2.zero;

        // ── IPlayerInput — Single-Frame Button Presses ────────────────────────

        public bool InteractPressed =>
            Keyboard.current?.eKey.wasPressedThisFrame ?? false;

        public bool ConfirmPressed =>
            (Mouse.current?.leftButton.wasPressedThisFrame ?? false) ||
            (Keyboard.current?.enterKey.wasPressedThisFrame ?? false);

        public bool CancelPressed =>
            (Mouse.current?.rightButton.wasPressedThisFrame ?? false) ||
            (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false);

        public bool PausePressed =>
            Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;

        public bool EndTurnPressed =>
            Keyboard.current?.spaceKey.wasPressedThisFrame ?? false;

        public bool DelayTurnPressed =>
            Keyboard.current?.zKey.wasPressedThisFrame ?? false;

        public bool Skill1Pressed =>
            Keyboard.current?.qKey.wasPressedThisFrame ?? false;

        public bool Skill2Pressed =>
            Keyboard.current?.digit1Key.wasPressedThisFrame ?? false;

        public bool Skill3Pressed =>
            Keyboard.current?.digit2Key.wasPressedThisFrame ?? false;

        public bool Skill4Pressed =>
            Keyboard.current?.rKey.wasPressedThisFrame ?? false;

        public bool GetSkillSlotPressed(int slotIndex)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return slotIndex switch
            {
                0 => kb.digit1Key.wasPressedThisFrame,
                1 => kb.digit2Key.wasPressedThisFrame,
                2 => kb.digit3Key.wasPressedThisFrame,
                3 => kb.digit4Key.wasPressedThisFrame,
                4 => kb.digit5Key.wasPressedThisFrame,
                5 => kb.digit6Key.wasPressedThisFrame,
                6 => kb.digit7Key.wasPressedThisFrame,
                7 => kb.digit8Key.wasPressedThisFrame,
                8 => kb.digit9Key.wasPressedThisFrame,
                9 => kb.digit0Key.wasPressedThisFrame,
                _ => false
            };
        }

        // ── IPlayerInput — Cursor & Scroll ────────────────────────────────────

        public Vector2 CursorScreenPosition =>
            Mouse.current?.position.ReadValue() ?? Vector2.zero;

        // Normalize: new Input System reports scroll in pixels (~120/click on Windows).
        // Divide by 120 to match legacy Input.GetAxis("Mouse ScrollWheel") scale (~1/click).
        public float ScrollDelta =>
            (Mouse.current?.scroll.ReadValue().y ?? 0f) / 120f;

        public bool SwitchCharacterPressed =>
            Keyboard.current?.tabKey.wasPressedThisFrame ?? false;

    }
}
