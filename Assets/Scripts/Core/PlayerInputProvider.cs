using UnityEngine;

namespace PokemonAdventure.Core
{
    // ==========================================================================
    // Player Input Provider
    // Implements IPlayerInput using Unity's built-in legacy input (no package).
    //
    // Default bindings:
    //   Move          — WASD / arrow keys          (Input Manager axes)
    //   CameraRotate  — right-mouse drag            (handled in CameraController)
    //   Interact      — E
    //   Confirm       — Left mouse button / Return
    //   Cancel        — Right mouse button / Escape
    //   Pause         — Escape
    //   EndTurn       — Space
    //   DelayTurn     — Z
    //   Skill 1–4     — Q / W / E / R
    //   Cursor        — mouse screen position
    //   Scroll        — mouse scroll wheel
    //
    // To swap to the new Input System later: replace this class with a version
    // that uses UnityEngine.InputSystem — the IPlayerInput contract stays the same.
    // ==========================================================================

    public class PlayerInputProvider : MonoBehaviour, IPlayerInput
    {
        // ── IPlayerInput — Movement ───────────────────────────────────────────

        public Vector2 MoveDirection =>
            new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        // Camera rotation is handled directly in OverworldCameraController via mouse delta.
        // Return zero here — wire up gamepad axes in the Unity Input Manager if needed.
        public Vector2 CameraRotateDirection => Vector2.zero;

        // ── IPlayerInput — Single-Frame Button Presses ────────────────────────

        public bool InteractPressed  => Input.GetKeyDown(KeyCode.E);

        public bool ConfirmPressed   => Input.GetMouseButtonDown(0)
                                     || Input.GetKeyDown(KeyCode.Return);

        public bool CancelPressed    => Input.GetMouseButtonDown(1)
                                     || Input.GetKeyDown(KeyCode.Escape);

        public bool PausePressed     => Input.GetKeyDown(KeyCode.Escape);

        public bool EndTurnPressed   => Input.GetKeyDown(KeyCode.Space);

        public bool DelayTurnPressed => Input.GetKeyDown(KeyCode.Z);

        public bool Skill1Pressed    => Input.GetKeyDown(KeyCode.Q);
        public bool Skill2Pressed    => Input.GetKeyDown(KeyCode.Alpha1);
        public bool Skill3Pressed    => Input.GetKeyDown(KeyCode.Alpha2);
        public bool Skill4Pressed    => Input.GetKeyDown(KeyCode.R);

        // ── IPlayerInput — Cursor & Scroll ────────────────────────────────────

        public Vector2 CursorScreenPosition =>
            new Vector2(Input.mousePosition.x, Input.mousePosition.y);

        public float ScrollDelta => Input.GetAxis("Mouse ScrollWheel");
    }
}
