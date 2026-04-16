using UnityEngine;
using PokemonAdventure.Core;
using PokemonAdventure.Grid;

namespace PokemonAdventure.World
{
    // ==========================================================================
    // Debug Grid Renderer
    // Draws a visible grid overlay directly into the Game view using GL lines.
    // No extra GameObjects per cell — one draw call for the entire grid.
    //
    // Features:
    //   - Thin grid lines over the ground plane
    //   - Coordinate labels every N cells (toggleable)
    //   - Distinct highlight colour for every 5th line (major grid)
    //   - Stays visible in both Overworld and Combat
    //
    // Attach to any persistent GameObject or let DebugSceneSetup create it.
    // Remove from production builds.
    // ==========================================================================

    public class DebugGridRenderer : MonoBehaviour
    {
        [Header("Line Colours")]
        [SerializeField] private Color _minorLineColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color _majorLineColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private int   _majorInterval  = 5;   // Every Nth line is major

        [Header("Height")]
        [Tooltip("Y offset above the ground to prevent z-fighting.")]
        [SerializeField] private float _yOffset = 0.02f;

        [Header("Coordinate Labels")]
        [SerializeField] private bool  _showLabels       = true;
        [SerializeField] private int   _labelInterval    = 5;   // Label every Nth cell
        [SerializeField] private Color _labelColor       = new Color(1f, 1f, 0.4f, 0.85f);
        [SerializeField] private int   _labelFontSize    = 11;

        // ── Internal ──────────────────────────────────────────────────────────

        private WorldGridManager _gridManager;
        private Material         _lineMat;

        // GUIStyle created once
        private GUIStyle _labelStyle;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            _gridManager = ServiceLocator.Get<WorldGridManager>();

            // "Hidden/Internal-Colored" is always compiled into Unity builds
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            _lineMat = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMat.SetInt("_ZWrite",   0);
            _lineMat.SetInt("_Cull",     0); // Double-sided
        }

        private void OnDestroy()
        {
            if (_lineMat != null)
                Destroy(_lineMat);
        }

        // ── GL Rendering ──────────────────────────────────────────────────────

        // OnRenderObject is called after the camera renders the scene.
        // Using it here instead of OnPostRender so any camera in the scene picks it up.
        private void OnRenderObject()
        {
            if (_gridManager == null || _lineMat == null) return;

            _lineMat.SetPass(0);

            int   w      = _gridManager.GridWidth;
            int   h      = _gridManager.GridHeight;
            float cs     = _gridManager.CellSize;
            var   origin = _gridManager.GridOrigin;
            float y      = origin.y + _yOffset;

            GL.Begin(GL.LINES);

            // ── Vertical lines (along Z axis, varying X) ──────────────────────
            for (int x = 0; x <= w; x++)
            {
                bool major = (x % _majorInterval == 0);
                GL.Color(major ? _majorLineColor : _minorLineColor);

                float wx = origin.x + x * cs;
                GL.Vertex3(wx, y, origin.z);
                GL.Vertex3(wx, y, origin.z + h * cs);
            }

            // ── Horizontal lines (along X axis, varying Z) ────────────────────
            for (int z = 0; z <= h; z++)
            {
                bool major = (z % _majorInterval == 0);
                GL.Color(major ? _majorLineColor : _minorLineColor);

                float wz = origin.z + z * cs;
                GL.Vertex3(origin.x,        y, wz);
                GL.Vertex3(origin.x + w * cs, y, wz);
            }

            GL.End();
        }

        // ── Coordinate Labels (OnGUI) ─────────────────────────────────────────

        private void OnGUI()
        {
            if (!_showLabels || _gridManager == null) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = _labelFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
                _labelStyle.normal.textColor = _labelColor;
            }

            var cam = Camera.main;
            if (cam == null) return;

            int w  = _gridManager.GridWidth;
            int h  = _gridManager.GridHeight;
            int iv = Mathf.Max(1, _labelInterval);

            for (int x = 0; x < w; x += iv)
            for (int z = 0; z < h; z += iv)
            {
                var worldPos   = _gridManager.GetWorldPosition(new Vector2Int(x, z));
                var screenPos  = cam.WorldToScreenPoint(worldPos);

                // Skip cells behind the camera or far off-screen
                if (screenPos.z < 0f) continue;
                if (screenPos.x < -40 || screenPos.x > Screen.width  + 40) continue;
                if (screenPos.y < -20 || screenPos.y > Screen.height + 20) continue;

                // GUI Y is flipped relative to screen space
                var rect = new Rect(screenPos.x + 2, Screen.height - screenPos.y + 2, 50, 18);
                GUI.Label(rect, $"{x},{z}", _labelStyle);
            }
        }
    }
}
