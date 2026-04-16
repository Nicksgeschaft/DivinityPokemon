using UnityEngine;

// ==========================================================================
// Pokemon Sprite Animator
// Plays PMD-style 8-directional sprite sheet animations on a billboard quad.
// Attach to a child GameObject of the unit (not the unit root itself).
// The parent's world-space forward is used to pick the correct direction row.
//
// Sprite sheet layout (PMD standard):
//   Rows    = 8 directions: Down, DownRight, Right, UpRight,
//                            Up,  UpLeft,    Left,  DownLeft
//   Columns = animation frames (left → right)
//   Sprite index = directionRow * frameCount + frameIndex
//
// Durations from AnimData are in game ticks (1 tick = 1/60 s at 60 fps).
// ==========================================================================

[RequireComponent(typeof(SpriteRenderer))]
public class PokemonSpriteAnimator : MonoBehaviour
{
    [SerializeField] private PokemonAnimationSet _animSet;
    [SerializeField] private SpriteRenderer      _shadowRenderer;

    [Tooltip("Seconds per PMD tick (default 1/60).")]
    [SerializeField] private float _tickSeconds = 1f / 60f;

    // ── 8 compass directions matching PMD row order ────────────────────────
    // Row 0 = Down (south), going clockwise.
    private static readonly Vector3[] s_dirs =
    {
        new Vector3( 0, 0, -1).normalized, // 0 Down
        new Vector3( 1, 0, -1).normalized, // 1 DownRight
        new Vector3( 1, 0,  0),            // 2 Right
        new Vector3( 1, 0,  1).normalized, // 3 UpRight
        new Vector3( 0, 0,  1),            // 4 Up
        new Vector3(-1, 0,  1).normalized, // 5 UpLeft
        new Vector3(-1, 0,  0),            // 6 Left
        new Vector3(-1, 0, -1).normalized, // 7 DownLeft
    };

    // ── State ─────────────────────────────────────────────────────────────

    private SpriteRenderer              _body;
    private PokemonAnimationDefinition  _current;
    private int                         _frame;
    private float                       _timer;
    private int                         _row;    // direction row 0–7
    private Vector3                     _originalLocalPosition;

    // ── Public API ────────────────────────────────────────────────────────

    public PokemonAnimationSet AnimSet
    {
        get => _animSet;
        set
        {
            _animSet = value;
            _current = null;
            if (_animSet != null) Play(PokemonAnimId.Idle);
        }
    }

    /// <summary>Switch to a different animation. Ignored if already playing.</summary>
    public void Play(PokemonAnimId id)
    {
        if (_animSet == null) return;

        var def = _animSet.Get(id);
        if (def == null || def.bodyFrames == null || def.bodyFrames.Length == 0) return;
        if (def == _current) return;

        _current = def;
        _frame   = 0;
        _timer   = 0f;
    }

    /// <summary>Squish/restore the sprite vertically to visualise crouching.</summary>
    public void SetCrouched(bool crouched, float crouchScaleY = 0.55f)
    {
        var s = transform.localScale;
        s.y = crouched ? crouchScaleY : 1f;
        transform.localScale = s;
    }

    /// <summary>Explicitly set facing direction (world-space XZ).</summary>
    public void SetFacing(Vector3 worldDir)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.001f) return;
        worldDir.Normalize();

        int   best    = 0;
        float bestDot = float.MinValue;
        for (int i = 0; i < s_dirs.Length; i++)
        {
            float d = Vector3.Dot(worldDir, s_dirs[i]);
            if (d > bestDot) { bestDot = d; best = i; }
        }
        _row = best;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        _body = GetComponent<SpriteRenderer>();
        _originalLocalPosition = transform.localPosition;
    }

    private void Start()
    {
        if (_animSet != null)
            Play(PokemonAnimId.Idle);
    }

    private void LateUpdate()
    {
        // Animation (optional — Billboard still runs without it)
        if (_animSet != null && _current != null)
        {
            if (transform.parent != null)
                SetFacing(transform.parent.forward);
            Tick();
            ApplyFrame();
        }

        // Billboard always runs so the sprite stays centred on the unit's
        // grid position even when no animation is assigned yet.
        Billboard();
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private void Tick()
    {
        if (_current.durations == null || _current.durations.Count == 0) return;

        _timer += Time.deltaTime;

        while (true)
        {
            if (_frame >= _current.FrameCount)
            {
                _frame = _current.loop ? 0 : _current.FrameCount - 1;
                if (!_current.loop) break;
            }

            float dur = _current.durations[_frame] * _tickSeconds;
            if (_timer < dur) break;

            _timer -= dur;
            _frame++;
        }
    }

    private void ApplyFrame()
    {
        int n   = _current.FrameCount;
        int idx = _row * n + _frame;

        if (_body != null && _current.bodyFrames != null && idx < _current.bodyFrames.Length)
            _body.sprite = _current.bodyFrames[idx];

        if (_shadowRenderer != null && _current.shadowFrames != null &&
            idx < _current.shadowFrames.Length)
            _shadowRenderer.sprite = _current.shadowFrames[idx];
    }

    /// <summary>
    /// Spherical billboard: sprite adopts the camera's exact rotation.
    /// At 90° top-down this makes the sprite lie flat on the ground — correct.
    /// At shallower angles the sprite tilts with the camera and appears upright.
    ///
    /// After rotating, the world position is shifted so the sprite's visual
    /// centre aligns with the unit's grid position, regardless of where the
    /// sprite's pivot point is set in the import settings.
    /// </summary>
    private void Billboard()
    {
        if (Camera.main == null) return;

        transform.rotation = Camera.main.transform.rotation;

        // Compensate for sprites whose pivot is not at the visual centre.
        // bounds.center is the offset from the pivot to the visual centre
        // in sprite-local space (world units at scale 1).
        // After billboarding, sprite local X/Y = camera right/up in world space.
        // We set the world position directly so the visual centre lands exactly
        // on the unit's anchor point — no InverseTransformVector, no scale issues.
        var baseWorldPos = transform.parent != null
            ? transform.parent.TransformPoint(_originalLocalPosition)
            : (Vector3)_originalLocalPosition;

        // TransformVector maps sprite-local bounds.center to world space,
        // properly accounting for the full parent scale hierarchy.
        // This is subtracted so the sprite's visual centre lands on baseWorldPos.
        var sprite = _body != null ? _body.sprite : null;
        var worldCenterOffset = sprite != null
            ? transform.TransformVector(sprite.bounds.center)
            : Vector3.zero;

        transform.position = baseWorldPos - worldCenterOffset;
    }
}
