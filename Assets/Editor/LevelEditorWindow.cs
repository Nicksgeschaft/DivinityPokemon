using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PokemonAdventure.Data;
using PokemonAdventure.ScriptableObjects;
using PokemonAdventure.World;

namespace PokemonAdventure.Editor
{
    // ==========================================================================
    // Level Editor Window
    // Tools → PokemonAdventure → Level Editor
    //
    // Usage:
    //   1. Open the window from the menu above.
    //   2. Create a new LevelData asset (New button) or open an existing one.
    //   3. Select a terrain or entity in the palette and paint directly in the
    //      Scene view using left-click (paint) or right-click (erase).
    //   4. Save the LevelData asset (Save button or Ctrl+S).
    //   5. Assign the asset to a LevelLoader in your scene and press Play.
    //
    // Keyboard shortcuts (when scene view is focused):
    //   T  — switch to Tiles layer
    //   E  — switch to Entities layer
    //   P  — Paint mode
    //   X  — Erase mode
    //   F  — Fill mode  (floods contiguous area)
    // ==========================================================================

    public class LevelEditorWindow : EditorWindow
    {
        // ── Constants ─────────────────────────────────────────────────────────

        private const float SidebarWidth  = 210f;
        private const float ButtonHeight  = 30f;
        private const float PaletteCell   = 64f;
        private const string PrefKeyAsset = "LevelEditor_LastAssetPath";

        // ── State ─────────────────────────────────────────────────────────────

        private LevelData    _levelData;
        private Vector2      _sidebarScroll;

        // Editing state
        private enum PaintMode  { Paint, Erase, Fill }
        private enum ActiveLayer { Tiles, Entities }

        private PaintMode   _mode      = PaintMode.Paint;
        private ActiveLayer _layer     = ActiveLayer.Tiles;
        private bool        _isPainting;

        // Tile palette
        private TileTerrain  _selectedTerrain  = TileTerrain.Grass;
        private SurfaceType  _selectedSurface  = SurfaceType.Normal;
        private bool         _selectedWalkable = true;
        private Color        _selectedCustomColor = Color.magenta;

        // Entity palette
        private PlacedEntityType              _selectedEntityType     = PlacedEntityType.Enemy;
        private EnemyArchetypeDefinition      _selectedArchetype;
        private PokemonDefinition             _selectedPlayerDef;

        // Scene view state
        private Vector2Int? _hoveredCell;
        private Vector2Int? _selectedCell;

        // Grid display settings
        private bool  _showGrid      = true;
        private bool  _showEntities  = true;
        private float _gridAlpha     = 0.25f;

        // ── Menu ──────────────────────────────────────────────────────────────

        [MenuItem("Tools/PokemonAdventure/Level Editor")]
        public static void Open()
        {
            var win = GetWindow<LevelEditorWindow>("Level Editor");
            win.minSize = new Vector2(240f, 500f);
            win.Show();
        }

        // ── Unity Callbacks ───────────────────────────────────────────────────

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            TryLoadLastAsset();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // ── Editor Window GUI ─────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(2);
            DrawGridSettings();
            EditorGUILayout.Space(4);
            DrawLayerAndMode();
            EditorGUILayout.Space(4);

            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            if (_layer == ActiveLayer.Tiles)
                DrawTilePalette();
            else
                DrawEntityPalette();

            EditorGUILayout.Space(8);
            DrawSelectedCellInfo();
            EditorGUILayout.Space(4);
            DrawViewSettings();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            DrawStatusBar();
        }

        // ── Toolbar ───────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
                CreateNewLevel();

            if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(44)))
                OpenLevel();

            GUI.enabled = _levelData != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40)))
                SaveLevel();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Asset picker
            EditorGUI.BeginChangeCheck();
            var newData = (LevelData)EditorGUILayout.ObjectField(
                _levelData, typeof(LevelData), false,
                GUILayout.MinWidth(100f), GUILayout.MaxWidth(160f));
            if (EditorGUI.EndChangeCheck())
                SetLevelData(newData);

            EditorGUILayout.EndHorizontal();
        }

        // ── Grid Settings ─────────────────────────────────────────────────────

        private void DrawGridSettings()
        {
            if (_levelData == null) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel, GUILayout.Width(30));

            EditorGUI.BeginChangeCheck();
            var newSize = EditorGUILayout.Vector2IntField(GUIContent.none, _levelData.GridSize,
                GUILayout.Width(90));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_levelData, "Resize Grid");
                _levelData.GridSize = new Vector2Int(Mathf.Max(1, newSize.x), Mathf.Max(1, newSize.y));
                EditorUtility.SetDirty(_levelData);
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField("Cell", GUILayout.Width(28));
            EditorGUI.BeginChangeCheck();
            float newCS = EditorGUILayout.FloatField(_levelData.CellSize, GUILayout.Width(38));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_levelData, "Change Cell Size");
                _levelData.CellSize = Mathf.Max(0.1f, newCS);
                EditorUtility.SetDirty(_levelData);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Fill Grass", GUILayout.Width(68)))
                FillAll(TileTerrain.Grass, SurfaceType.Normal, true);

            if (GUILayout.Button("Clear", GUILayout.Width(44)))
                ClearAll();

            EditorGUILayout.EndHorizontal();
        }

        // ── Layer + Mode ──────────────────────────────────────────────────────

        private void DrawLayerAndMode()
        {
            // Layer toggle
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = _layer == ActiveLayer.Tiles ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("Tiles (T)", GUILayout.Height(24)))
                _layer = ActiveLayer.Tiles;
            GUI.backgroundColor = _layer == ActiveLayer.Entities ? new Color(1f, 0.8f, 0.4f) : Color.white;
            if (GUILayout.Button("Entities (E)", GUILayout.Height(24)))
                _layer = ActiveLayer.Entities;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Mode toggle
            EditorGUILayout.BeginHorizontal();
            DrawModeButton(PaintMode.Paint,  "Paint (P)");
            DrawModeButton(PaintMode.Erase,  "Erase (X)");
            DrawModeButton(PaintMode.Fill,   "Fill (F)");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModeButton(PaintMode mode, string label)
        {
            GUI.backgroundColor = _mode == mode ? new Color(0.7f, 1f, 0.7f) : Color.white;
            if (GUILayout.Button(label, GUILayout.Height(22)))
                _mode = mode;
            GUI.backgroundColor = Color.white;
        }

        // ── Tile Palette ──────────────────────────────────────────────────────

        private static readonly TileTerrain[] AllTerrains = (TileTerrain[])System.Enum.GetValues(typeof(TileTerrain));

        private void DrawTilePalette()
        {
            EditorGUILayout.LabelField("Terrain", EditorStyles.boldLabel);

            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 16f) / PaletteCell));
            int col     = 0;

            EditorGUILayout.BeginHorizontal();

            foreach (var terrain in AllTerrains)
            {
                if (col >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    col = 0;
                }

                bool selected = _selectedTerrain == terrain;
                Color tileCol = terrain == TileTerrain.Custom
                    ? _selectedCustomColor
                    : LevelLoader.TerrainColor(terrain);

                DrawTerrainButton(terrain, tileCol, selected);
                col++;
            }

            EditorGUILayout.EndHorizontal();

            // Custom colour picker (only shown when Custom is selected)
            if (_selectedTerrain == TileTerrain.Custom)
            {
                EditorGUI.BeginChangeCheck();
                var c = EditorGUILayout.ColorField("Custom Colour", _selectedCustomColor);
                if (EditorGUI.EndChangeCheck())
                    _selectedCustomColor = c;
            }

            EditorGUILayout.Space(6);

            // Surface effect
            EditorGUILayout.LabelField("Surface Effect", EditorStyles.boldLabel);
            _selectedSurface = (SurfaceType)EditorGUILayout.EnumPopup(_selectedSurface);

            // Walkability
            _selectedWalkable = EditorGUILayout.Toggle("Walkable", _selectedWalkable);
        }

        private void DrawTerrainButton(TileTerrain terrain, Color tileCol, bool selected)
        {
            var rect = GUILayoutUtility.GetRect(PaletteCell, PaletteCell + 14f,
                GUILayout.Width(PaletteCell));

            // Selection border — drawn first so swatch sits on top
            if (selected)
                EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 13),
                    new Color(1f, 0.95f, 0.1f, 0.9f));

            // Swatch
            float pad       = selected ? 4f : 3f;
            var   swatchRect = new Rect(rect.x + pad, rect.y + pad,
                                        rect.width - pad * 2f, rect.height - 14f - pad);
            EditorGUI.DrawRect(swatchRect, tileCol);

            // Label
            var labelRect = new Rect(rect.x, rect.y + rect.height - 14, rect.width, 14);
            GUI.Label(labelRect, terrain.ToString(),
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 8 });

            // Click
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedTerrain = terrain;
                Event.current.Use();
                Repaint();
            }
        }

        // ── Entity Palette ────────────────────────────────────────────────────

        private static readonly (PlacedEntityType type, string label, Color color)[] EntityDefs =
        {
            (PlacedEntityType.Player1, "Player 1", new Color(0.20f, 0.45f, 0.90f)),
            (PlacedEntityType.Player2, "Player 2", new Color(0.10f, 0.65f, 0.95f)),
            (PlacedEntityType.Player3, "Player 3", new Color(0.10f, 0.80f, 0.80f)),
            (PlacedEntityType.Player4, "Player 4", new Color(0.55f, 0.20f, 0.90f)),
            (PlacedEntityType.Enemy,   "Enemy",    new Color(0.90f, 0.20f, 0.20f)),
            (PlacedEntityType.NPC,     "NPC",      new Color(0.90f, 0.80f, 0.10f)),
        };

        private void DrawEntityPalette()
        {
            EditorGUILayout.LabelField("Entity Type", EditorStyles.boldLabel);

            foreach (var (type, label, color) in EntityDefs)
            {
                bool selected = _selectedEntityType == type;
                GUI.backgroundColor = selected ? color : Color.Lerp(color, Color.white, 0.6f);

                if (GUILayout.Button(label, GUILayout.Height(ButtonHeight)))
                    _selectedEntityType = type;

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(8);

            // Type-specific options
            if (_selectedEntityType == PlacedEntityType.Enemy)
            {
                EditorGUILayout.LabelField("Archetype", EditorStyles.boldLabel);
                _selectedArchetype = (EnemyArchetypeDefinition)EditorGUILayout.ObjectField(
                    _selectedArchetype, typeof(EnemyArchetypeDefinition), false);
            }
            else if (_selectedEntityType <= PlacedEntityType.Player4)
            {
                EditorGUILayout.LabelField("Pokemon Definition", EditorStyles.boldLabel);
                _selectedPlayerDef = (PokemonDefinition)EditorGUILayout.ObjectField(
                    _selectedPlayerDef, typeof(PokemonDefinition), false);
            }
        }

        // ── Selected Cell Info ────────────────────────────────────────────────

        private void DrawSelectedCellInfo()
        {
            if (_levelData == null || !_selectedCell.HasValue) return;

            EditorGUILayout.LabelField("Selected Cell", EditorStyles.boldLabel);
            var pos    = _selectedCell.Value;
            var tile   = _levelData.GetTile(pos);
            var entity = _levelData.GetEntity(pos);

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Position: ({pos.x}, {pos.y})");

            if (tile != null)
            {
                EditorGUILayout.LabelField($"Terrain:  {tile.Terrain}");
                EditorGUILayout.LabelField($"Surface:  {tile.Surface}");
                EditorGUILayout.LabelField($"Walkable: {tile.IsWalkable}");
            }
            else
            {
                EditorGUILayout.LabelField("No tile");
            }

            if (entity != null)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"Entity:   {entity.EntityType}");
                if (entity.EnemyArchetype != null)
                    EditorGUILayout.LabelField($"Archetype: {entity.EnemyArchetype.EnemyName}");
            }

            EditorGUI.indentLevel--;
        }

        // ── View Settings ─────────────────────────────────────────────────────

        private void DrawViewSettings()
        {
            EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
            bool newGrid = EditorGUILayout.Toggle("Show Grid",     _showGrid);
            bool newEnt  = EditorGUILayout.Toggle("Show Entities", _showEntities);
            float newAlpha = EditorGUILayout.Slider("Grid Alpha", _gridAlpha, 0.05f, 1f);

            if (newGrid != _showGrid || newEnt != _showEntities || !Mathf.Approximately(newAlpha, _gridAlpha))
            {
                _showGrid     = newGrid;
                _showEntities = newEnt;
                _gridAlpha    = newAlpha;
                SceneView.RepaintAll();
            }
        }

        // ── Status Bar ────────────────────────────────────────────────────────

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (_levelData != null)
            {
                GUILayout.Label($"Tiles: {_levelData.Tiles.Count}   Entities: {_levelData.Entities.Count}",
                    EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                if (_hoveredCell.HasValue)
                    GUILayout.Label($"({_hoveredCell.Value.x}, {_hoveredCell.Value.y})",
                        EditorStyles.miniLabel);
            }
            else
            {
                GUILayout.Label("No level loaded.  Create or open a LevelData asset.",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Scene View ────────────────────────────────────────────────────────

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_levelData == null) return;

            // Claim the scene view input so clicks don't deselect GameObjects
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            // Keyboard shortcuts
            HandleKeyboardShortcuts();

            // Compute hovered cell
            _hoveredCell = GetHoveredCell();

            // Draw
            if (_showGrid)   DrawGrid(sceneView);
            DrawPlacedTiles();
            if (_showEntities) DrawPlacedEntities();
            if (_hoveredCell.HasValue) DrawHoverHighlight(_hoveredCell.Value);
            DrawBoundsOutline();

            // Mouse interaction
            HandleMouseEvents();

            // Redraw continuously while inside the scene view
            sceneView.Repaint();
            Repaint(); // update status bar hover coords
        }

        // ── Grid Drawing ──────────────────────────────────────────────────────

        private void DrawGrid(SceneView sceneView)
        {
            if (_levelData == null) return;

            float cs    = _levelData.CellSize;
            int   w     = _levelData.GridSize.x;
            int   h     = _levelData.GridSize.y;
            var   orig  = _levelData.GridOrigin;

            Handles.color = new Color(0.6f, 0.6f, 0.6f, _gridAlpha);

            float totalW = w * cs;
            float totalH = h * cs;

            // Vertical lines
            for (int x = 0; x <= w; x++)
            {
                float wx = orig.x + x * cs;
                Handles.DrawLine(
                    new Vector3(wx, orig.y + 0.01f, orig.z),
                    new Vector3(wx, orig.y + 0.01f, orig.z + totalH));
            }

            // Horizontal lines
            for (int y = 0; y <= h; y++)
            {
                float wz = orig.z + y * cs;
                Handles.DrawLine(
                    new Vector3(orig.x,          orig.y + 0.01f, wz),
                    new Vector3(orig.x + totalW, orig.y + 0.01f, wz));
            }
        }

        private void DrawBoundsOutline()
        {
            if (_levelData == null) return;
            float cs   = _levelData.CellSize;
            var   orig = _levelData.GridOrigin;
            float w    = _levelData.GridSize.x * cs;
            float h    = _levelData.GridSize.y * cs;
            float y    = orig.y + 0.015f;

            Handles.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            Handles.DrawAAPolyLine(3f,
                new Vector3(orig.x,     y, orig.z),
                new Vector3(orig.x + w, y, orig.z),
                new Vector3(orig.x + w, y, orig.z + h),
                new Vector3(orig.x,     y, orig.z + h),
                new Vector3(orig.x,     y, orig.z));
        }

        // ── Tile Drawing ──────────────────────────────────────────────────────

        private void DrawPlacedTiles()
        {
            if (_levelData == null) return;

            foreach (var tile in _levelData.Tiles)
            {
                var color = tile.Terrain == TileTerrain.Custom
                    ? tile.CustomColor
                    : LevelLoader.TerrainColor(tile.Terrain);

                color.a = 0.85f;
                DrawCellRect(tile.GridPosition, color, new Color(0, 0, 0, 0.15f));

                // Non-walkable overlay
                if (!tile.IsWalkable)
                {
                    DrawCellRect(tile.GridPosition,
                        new Color(0.8f, 0.1f, 0.1f, 0.25f),
                        Color.clear);
                }

                // Surface badge
                if (tile.Surface != SurfaceType.Normal)
                {
                    var center = TileCenter(tile.GridPosition, 0.08f);
                    var style  = new GUIStyle(EditorStyles.miniLabel)
                        { fontSize = 7, normal = { textColor = Color.white } };
                    Handles.Label(center + Vector3.up * 0.05f, tile.Surface.ToString()[..3], style);
                }
            }
        }

        // ── Entity Drawing ────────────────────────────────────────────────────

        private void DrawPlacedEntities()
        {
            if (_levelData == null) return;

            foreach (var entity in _levelData.Entities)
            {
                var color  = EntityColor(entity.EntityType);
                var center = TileCenter(entity.GridPosition, 0.08f);
                float cs   = _levelData.CellSize;
                float r    = cs * 0.28f;

                // Filled circle (approximated with quad)
                Handles.color = new Color(color.r, color.g, color.b, 0.9f);
                Handles.DrawSolidDisc(center, Vector3.up, r);

                // Label
                string label = EntityLabel(entity.EntityType);
                var style = new GUIStyle
                {
                    fontSize  = Mathf.RoundToInt(cs * 14f),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = Color.white }
                };
                Handles.Label(center - Vector3.right * (cs * 0.12f)
                                     - Vector3.forward * (cs * 0.12f), label, style);
            }
        }

        // ── Hover Highlight ───────────────────────────────────────────────────

        private void DrawHoverHighlight(Vector2Int cell)
        {
            var color = _mode == PaintMode.Erase
                ? new Color(1f, 0.3f, 0.3f, 0.6f)
                : new Color(1f, 1f, 1f, 0.5f);

            DrawCellRect(cell, new Color(color.r, color.g, color.b, 0.15f), color);
        }

        // ── Mouse Handling ────────────────────────────────────────────────────

        private void HandleMouseEvents()
        {
            var evt = Event.current;
            if (evt == null) return;

            // Alt held = Scene View orbit — never consume, let Unity handle it
            if (evt.alt) return;

            bool leftDown  = evt.type == EventType.MouseDown && evt.button == 0;
            bool leftDrag  = evt.type == EventType.MouseDrag && evt.button == 0;
            bool rightDown = evt.type == EventType.MouseDown && evt.button == 1;
            bool mouseUp   = evt.type == EventType.MouseUp;

            // Middle-click and right-click drag go to Scene View camera — don't touch them
            if (evt.button == 2) return;

            if (leftDown || leftDrag)
            {
                _isPainting = true;
                if (_hoveredCell.HasValue)
                {
                    if (_mode == PaintMode.Fill)
                    {
                        if (leftDown) FloodFill(_hoveredCell.Value);
                    }
                    else
                    {
                        _selectedCell = _hoveredCell;
                        ApplyPaint(_hoveredCell.Value, _mode == PaintMode.Erase);
                    }
                }
                evt.Use();
            }
            else if (rightDown)
            {
                // Single right-click erases one tile; right-drag is free for camera
                if (_hoveredCell.HasValue)
                    ApplyPaint(_hoveredCell.Value, erase: true);
                evt.Use();
            }

            if (mouseUp) _isPainting = false;
        }

        private void HandleKeyboardShortcuts()
        {
            var evt = Event.current;
            if (evt?.type != EventType.KeyDown) return;

            // Right-click held = fly mode — don't steal WASD/E from Scene View navigation
            if (evt.button == 1 || Event.current.isMouse) return;
            if (UnityEngine.Input.GetMouseButton(1)) return;

            switch (evt.keyCode)
            {
                case KeyCode.T: _layer = ActiveLayer.Tiles;    evt.Use(); Repaint(); break;
                case KeyCode.E: _layer = ActiveLayer.Entities; evt.Use(); Repaint(); break;
                case KeyCode.P: _mode  = PaintMode.Paint;      evt.Use(); Repaint(); break;
                case KeyCode.X: _mode  = PaintMode.Erase;      evt.Use(); Repaint(); break;
                case KeyCode.F: _mode  = PaintMode.Fill;       evt.Use(); Repaint(); break;
            }
        }

        // ── Paint Actions ─────────────────────────────────────────────────────

        private void ApplyPaint(Vector2Int pos, bool erase)
        {
            if (_levelData == null) return;

            if (_layer == ActiveLayer.Tiles)
            {
                Undo.RecordObject(_levelData, erase ? "Erase Tile" : "Paint Tile");

                if (erase)
                    _levelData.RemoveTile(pos);
                else
                    _levelData.SetTile(pos, _selectedTerrain, _selectedSurface,
                                       _selectedWalkable, _selectedCustomColor);

                EditorUtility.SetDirty(_levelData);
            }
            else
            {
                Undo.RecordObject(_levelData, erase ? "Erase Entity" : "Place Entity");

                if (erase)
                    _levelData.RemoveEntity(pos);
                else
                    _levelData.SetEntity(pos, _selectedEntityType,
                                         _selectedArchetype, _selectedPlayerDef);

                EditorUtility.SetDirty(_levelData);
            }
        }

        private void FloodFill(Vector2Int start)
        {
            if (_levelData == null || _layer != ActiveLayer.Tiles) return;

            TileTerrain? matchTerrain = _levelData.GetTile(start)?.Terrain;

            // No-op if the start cell already has the target terrain
            if (matchTerrain.HasValue && matchTerrain.Value == _selectedTerrain) return;

            var visited = new HashSet<Vector2Int>();
            var queue   = new Queue<Vector2Int>();
            queue.Enqueue(start);

            Undo.RecordObject(_levelData, "Flood Fill");

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!visited.Add(cur))         continue;
                if (!_levelData.IsInBounds(cur)) continue;

                // Match: both empty, or both have the same terrain
                TileTerrain? curTerrain = _levelData.GetTile(cur)?.Terrain;
                if (curTerrain != matchTerrain) continue;

                _levelData.SetTile(cur, _selectedTerrain, _selectedSurface,
                                   _selectedWalkable, _selectedCustomColor);

                queue.Enqueue(cur + Vector2Int.right);
                queue.Enqueue(cur + Vector2Int.left);
                queue.Enqueue(cur + Vector2Int.up);
                queue.Enqueue(cur + Vector2Int.down);
            }

            EditorUtility.SetDirty(_levelData);
        }

        // ── Bulk Operations ───────────────────────────────────────────────────

        private void FillAll(TileTerrain terrain, SurfaceType surface, bool walkable)
        {
            if (_levelData == null) return;
            if (!EditorUtility.DisplayDialog("Fill All",
                $"Fill the entire {_levelData.GridSize.x}×{_levelData.GridSize.y} grid with {terrain}?",
                "Fill", "Cancel")) return;

            Undo.RecordObject(_levelData, "Fill All Tiles");
            _levelData.Tiles.Clear();

            for (int x = 0; x < _levelData.GridSize.x; x++)
            for (int y = 0; y < _levelData.GridSize.y; y++)
                _levelData.SetTile(new Vector2Int(x, y), terrain, surface, walkable);

            EditorUtility.SetDirty(_levelData);
            SceneView.RepaintAll();
        }

        private void ClearAll()
        {
            if (_levelData == null) return;
            if (!EditorUtility.DisplayDialog("Clear Level",
                "Remove ALL tiles and entities?", "Clear", "Cancel")) return;

            Undo.RecordObject(_levelData, "Clear Level");
            _levelData.Tiles.Clear();
            _levelData.Entities.Clear();
            EditorUtility.SetDirty(_levelData);
            SceneView.RepaintAll();
        }

        // ── Asset Management ──────────────────────────────────────────────────

        private void CreateNewLevel()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Level", "NewLevel", "asset", "Choose where to save the new level.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SetLevelData(asset);
        }

        private void OpenLevel()
        {
            string path = EditorUtility.OpenFilePanel("Open Level", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            path = "Assets" + path.Substring(Application.dataPath.Length);
            var asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (asset != null)
                SetLevelData(asset);
            else
                EditorUtility.DisplayDialog("Error", "That file is not a LevelData asset.", "OK");
        }

        private void SaveLevel()
        {
            if (_levelData == null) return;
            EditorUtility.SetDirty(_levelData);
            AssetDatabase.SaveAssets();
        }

        private void SetLevelData(LevelData data)
        {
            _levelData = data;
            if (data != null)
            {
                EditorPrefs.SetString(PrefKeyAsset, AssetDatabase.GetAssetPath(data));
                Selection.activeObject = data;
            }
            SceneView.RepaintAll();
            Repaint();
        }

        private void TryLoadLastAsset()
        {
            var path = EditorPrefs.GetString(PrefKeyAsset, string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (asset != null) _levelData = asset;
            }
        }

        // ── Coordinate Helpers ────────────────────────────────────────────────

        private Vector2Int? GetHoveredCell()
        {
            if (_levelData == null) return null;

            var ray   = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, _levelData.GridOrigin.y, 0f));

            if (!plane.Raycast(ray, out float dist)) return null;

            var worldPt  = ray.GetPoint(dist);
            var gridPos  = _levelData.WorldToGrid(worldPt);

            return _levelData.IsInBounds(gridPos) ? gridPos : null;
        }

        private Vector3 TileCenter(Vector2Int pos, float yOffset = 0f)
        {
            var w = _levelData.GridToWorld(pos);
            w.y   = _levelData.GridOrigin.y + yOffset;
            return w;
        }

        // ── Draw Helpers ──────────────────────────────────────────────────────

        private void DrawCellRect(Vector2Int pos, Color fill, Color outline)
        {
            if (_levelData == null) return;

            float cs  = _levelData.CellSize;
            float pad = cs * 0.02f;
            var   c   = TileCenter(pos, 0.02f);

            var corners = new[]
            {
                c + new Vector3(-cs * 0.5f + pad, 0, -cs * 0.5f + pad),
                c + new Vector3( cs * 0.5f - pad, 0, -cs * 0.5f + pad),
                c + new Vector3( cs * 0.5f - pad, 0,  cs * 0.5f - pad),
                c + new Vector3(-cs * 0.5f + pad, 0,  cs * 0.5f - pad),
            };

            Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
        }

        // ── Static Data Helpers ───────────────────────────────────────────────

        private static Color EntityColor(PlacedEntityType type) => type switch
        {
            PlacedEntityType.Player1 => new Color(0.20f, 0.45f, 0.90f),
            PlacedEntityType.Player2 => new Color(0.10f, 0.65f, 0.95f),
            PlacedEntityType.Player3 => new Color(0.10f, 0.80f, 0.80f),
            PlacedEntityType.Player4 => new Color(0.55f, 0.20f, 0.90f),
            PlacedEntityType.Enemy   => new Color(0.90f, 0.20f, 0.20f),
            PlacedEntityType.NPC     => new Color(0.90f, 0.80f, 0.10f),
            _                        => Color.white,
        };

        private static string EntityLabel(PlacedEntityType type) => type switch
        {
            PlacedEntityType.Player1 => "1",
            PlacedEntityType.Player2 => "2",
            PlacedEntityType.Player3 => "3",
            PlacedEntityType.Player4 => "4",
            PlacedEntityType.Enemy   => "E",
            PlacedEntityType.NPC     => "N",
            _                        => "?",
        };
    }
}
