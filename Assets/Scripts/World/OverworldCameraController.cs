using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Data;

namespace PokemonAdventure.World
{
    // ==========================================================================
    // Overworld Camera Controller — RTS / Top-Down Pan Camera
    // Free-panning camera decoupled from any unit.
    //
    // Controls:
    //   Arrow keys         — pan in world X/Z
    //   Mouse edge scroll  — pan when cursor is within _edgeThickness pixels of edge
    //   Scroll wheel       — zoom (adjusts height)
    //
    // Attach to the Main Camera.
    // Call SetTarget(transform) once after the player spawns to set initial
    // focus point — the camera won't follow after that.
    // ==========================================================================

    public class OverworldCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [Tooltip("World units per second when panning.")]
        [SerializeField] private float _panSpeed = 12f;

        [Tooltip("Enable panning when the cursor is near the screen edge.")]
        [SerializeField] private bool _edgeScrollEnabled = true;

        [Tooltip("Pixel thickness of the edge-scroll zone.")]
        [SerializeField] private float _edgeThickness = 24f;

        [Header("Zoom (Height)")]
        [SerializeField] private float _defaultHeight = 10f;
        [SerializeField] private float _minHeight      = 4f;
        [SerializeField] private float _maxHeight      = 22f;
        [SerializeField] private float _zoomSpeed      = 6f;
        [SerializeField] private float _zoomSmoothTime = 0.12f;

        [Header("Tilt")]
        [Tooltip("Degrees from horizontal. 90 = straight down. 72 = PMD-style angled.")]
        [SerializeField] private float _tiltAngle = 90f;

        [Header("World Bounds")]
        [Tooltip("Clamp the focus point inside these world X/Z bounds.")]
        [SerializeField] private float _minX =  0f;
        [SerializeField] private float _maxX = 60f;
        [SerializeField] private float _minZ =  0f;
        [SerializeField] private float _maxZ = 60f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Vector3 _focusPoint;          // Ground point the camera looks at
        private float   _currentHeight;
        private float   _targetHeight;
        private float   _heightVelocity;
        private bool    _isActive = true;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _currentHeight = _defaultHeight;
            _targetHeight  = _defaultHeight;

            // Default focus: centre of the 60×60 grid
            _focusPoint = new Vector3(
                (_minX + _maxX) * 0.5f, 0f,
                (_minZ + _maxZ) * 0.5f);

            ApplyPosition();

            GameEventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy()
        {
            GameEventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        private void LateUpdate()
        {
            if (!_isActive) return;

            HandlePan();
            HandleZoom();
            ApplyPosition();
        }

        // ── Pan ───────────────────────────────────────────────────────────────

        private void HandlePan()
        {
            var dir = Vector3.zero;

            // Arrow keys
            if (Input.GetKey(KeyCode.LeftArrow)  || Input.GetKey(KeyCode.A)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) dir.x += 1f;
            if (Input.GetKey(KeyCode.UpArrow)    || Input.GetKey(KeyCode.W)) dir.z += 1f;
            if (Input.GetKey(KeyCode.DownArrow)  || Input.GetKey(KeyCode.S)) dir.z -= 1f;

            // Edge scrolling (only when cursor is inside the window)
            if (_edgeScrollEnabled && Application.isFocused)
            {
                var m = Input.mousePosition;
                if (m.x >= 0 && m.x <= Screen.width &&
                    m.y >= 0 && m.y <= Screen.height)
                {
                    if (m.x < _edgeThickness)                    dir.x -= 1f;
                    if (m.x > Screen.width  - _edgeThickness)    dir.x += 1f;
                    if (m.y < _edgeThickness)                    dir.z -= 1f;
                    if (m.y > Screen.height - _edgeThickness)    dir.z += 1f;
                }
            }

            if (dir.sqrMagnitude < 0.001f) return;

            _focusPoint += dir.normalized * (_panSpeed * Time.deltaTime);
            _focusPoint.x = Mathf.Clamp(_focusPoint.x, _minX, _maxX);
            _focusPoint.z = Mathf.Clamp(_focusPoint.z, _minZ, _maxZ);
        }

        // ── Zoom ──────────────────────────────────────────────────────────────

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _targetHeight = Mathf.Clamp(
                    _targetHeight - scroll * _zoomSpeed,
                    _minHeight, _maxHeight);

            _currentHeight = Mathf.SmoothDamp(
                _currentHeight, _targetHeight,
                ref _heightVelocity, _zoomSmoothTime);
        }

        // ── Position ──────────────────────────────────────────────────────────

        private void ApplyPosition()
        {
            // At exactly 90° the camera sits directly above the focus point.
            // For any other angle it sits behind the focus point by backDist so
            // it looks at the focus point at the chosen tilt from horizontal.
            float tiltRad  = _tiltAngle * Mathf.Deg2Rad;
            float tanTilt  = Mathf.Tan(tiltRad);
            float backDist = Mathf.Abs(tanTilt) > 0.0001f
                ? _currentHeight / tanTilt
                : 0f;

            transform.position = new Vector3(
                _focusPoint.x,
                _currentHeight,
                _focusPoint.z - backDist);

            transform.rotation = Quaternion.Euler(_tiltAngle, 0f, 0f);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the initial focus point to the target's position.
        /// The camera does NOT follow after this call.
        /// </summary>
        public void SetTarget(Transform target)
        {
            if (target != null)
                _focusPoint = new Vector3(target.position.x, 0f, target.position.z);
        }

        /// <summary>
        /// Override the tilt angle and height at runtime.
        /// Use this from DebugSceneSetup to bypass stale scene-serialised values.
        /// </summary>
        public void ConfigureView(float tiltDegrees, float height)
        {
            _tiltAngle     = Mathf.Clamp(tiltDegrees, 0f, 90f);
            _defaultHeight = height;
            _currentHeight = height;
            _targetHeight  = height;
            ApplyPosition();
        }

        public void SetActive(bool active) => _isActive = active;

        // ── Events ────────────────────────────────────────────────────────────

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            // Keep the camera active in both Overworld and Combat
            _isActive = evt.NewState == GameState.Overworld ||
                        evt.NewState == GameState.Combat;
        }
    }
}
