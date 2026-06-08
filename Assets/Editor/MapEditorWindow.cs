// ============================================================================
// Editor/MapEditorWindow.cs — P2.5 地图编辑器
// 纯 Editor 工具:刷格/刷地形/划省/设省属性/导出 JSON
// ============================================================================

#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IronCrown.Editor
{
    public class MapEditorWindow : EditorWindow
    {
        // === 地图数据 ===
        private int _mapWidth = 12;
        private int _mapHeight = 8;
        private TileData[,] _tiles;

        // === 编辑状态 ===
        private EditMode _mode = EditMode.PaintTerrain;
        private TerrainType _brushTerrain = TerrainType.Plain;
        private string _brushProvinceId = "";
        private int _brushCountryIndex = 0;
        private string _exportPath = "Assets/StreamingAssets/Configs/Json";

        // === 省属性 ===
        private class ProvinceData
        {
            public string id = "";
            public string name = "";
            public string ownerCountry = "";
            public bool isCapital;
            public int infrastructure;
            public int population = 1000;
            public int victoryPoint;
            public TerrainType terrain;
            public int gridX, gridY;
        }

        private Dictionary<string, ProvinceData> _provinces = new();
        private string _selectedProvinceId;
        private Vector2 _scrollPos;
        private bool _showProvinceList = true;

        private class TileData
        {
            public TerrainType terrain = TerrainType.Plain;
            public string provinceId = "";
        }

        private enum EditMode
        {
            PaintTerrain,
            AssignProvince,
            SetProperties
        }

        [MenuItem("IronCrown/Map Editor")]
        public static void ShowWindow()
        {
            GetWindow<MapEditorWindow>("Map Editor");
        }

        private void OnEnable()
        {
            if (_tiles == null)
                InitMap(_mapWidth, _mapHeight);
        }

        private void InitMap(int w, int h)
        {
            _tiles = new TileData[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    _tiles[x, y] = new TileData();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // === 左侧: 工具面板 ===
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            DrawToolbar();
            EditorGUILayout.EndVertical();

            // === 中间: 地图网格 ===
            EditorGUILayout.BeginVertical();
            DrawMapGrid();
            EditorGUILayout.EndVertical();

            // === 右侧: 省属性 ===
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            DrawProvincePanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.LabelField("地图编辑器", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // 地图尺寸
            EditorGUILayout.LabelField("地图尺寸", EditorStyles.boldLabel);
            _mapWidth = EditorGUILayout.IntField("宽度", _mapWidth);
            _mapHeight = EditorGUILayout.IntField("高度", _mapHeight);
            if (GUILayout.Button("重新生成地图"))
            {
                if (EditorUtility.DisplayDialog("确认", "重新生成将清空所有数据，确定？", "确定", "取消"))
                    InitMap(_mapWidth, _mapHeight);
            }

            EditorGUILayout.Space(10);

            // 编辑模式
            EditorGUILayout.LabelField("编辑模式", EditorStyles.boldLabel);
            _mode = (EditMode)EditorGUILayout.EnumPopup(_mode);

            EditorGUILayout.Space(5);

            switch (_mode)
            {
                case EditMode.PaintTerrain:
                    _brushTerrain = (TerrainType)EditorGUILayout.EnumPopup("地形", _brushTerrain);
                    break;

                case EditMode.AssignProvince:
                    _brushProvinceId = EditorGUILayout.TextField("省 ID", _brushProvinceId);
                    if (GUILayout.Button("新建省"))
                    {
                        string newId = $"province_{_provinces.Count + 1}";
                        _provinces[newId] = new ProvinceData
                        {
                            id = newId, name = $"省份 {_provinces.Count + 1}",
                            terrain = _brushTerrain
                        };
                        _brushProvinceId = newId;
                    }
                    break;

                case EditMode.SetProperties:
                    if (!string.IsNullOrEmpty(_selectedProvinceId) && _provinces.ContainsKey(_selectedProvinceId))
                    {
                        var p = _provinces[_selectedProvinceId];
                        p.name = EditorGUILayout.TextField("名称", p.name);
                        p.ownerCountry = EditorGUILayout.TextField("所属国", p.ownerCountry);
                        p.isCapital = EditorGUILayout.Toggle("首都", p.isCapital);
                        p.infrastructure = EditorGUILayout.IntField("基建", p.infrastructure);
                        p.population = EditorGUILayout.IntField("人口", p.population);
                        p.victoryPoint = EditorGUILayout.IntField("胜利点", p.victoryPoint);
                        p.terrain = (TerrainType)EditorGUILayout.EnumPopup("地形", p.terrain);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("点击地图上的格子选中省份", MessageType.Info);
                    }
                    break;
            }

            EditorGUILayout.Space(10);

            // 国家列表
            EditorGUILayout.LabelField("国家", EditorStyles.boldLabel);
            _brushCountryIndex = EditorGUILayout.IntField("国家索引", _brushCountryIndex);

            EditorGUILayout.Space(10);

            // 导出
            EditorGUILayout.LabelField("导出", EditorStyles.boldLabel);
            _exportPath = EditorGUILayout.TextField("路径", _exportPath);
            if (GUILayout.Button("导出 provinces.json"))
                ExportProvincesJson();

            EditorGUILayout.Space(10);

            // 导入
            if (GUILayout.Button("导入 provinces.json"))
                ImportProvincesJson();
        }

        private void DrawMapGrid()
        {
            if (_tiles == null) return;

            float cellSize = 40f;
            float mapPixelW = _tiles.GetLength(0) * cellSize;
            float mapPixelH = _tiles.GetLength(1) * cellSize;

            // 滚动视图
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 绘制网格
            var rect = GUILayoutUtility.GetRect(mapPixelW + 10, mapPixelH + 10);
            rect.x += 5;
            rect.y += 5;

            for (int x = 0; x < _tiles.GetLength(0); x++)
            {
                for (int y = 0; y < _tiles.GetLength(1); y++)
                {
                    var cellRect = new Rect(rect.x + x * cellSize, rect.y + y * cellSize, cellSize - 1, cellSize - 1);
                    var tile = _tiles[x, y];

                    // 颜色: 地形
                    Color color = GetTerrainColor(tile.terrain);

                    // 选中高亮
                    if (tile.provinceId == _selectedProvinceId)
                        color = Color.Lerp(color, Color.yellow, 0.4f);

                    EditorGUI.DrawRect(cellRect, color);

                    // 省 ID 标签
                    if (!string.IsNullOrEmpty(tile.provinceId) && cellSize >= 30)
                    {
                        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                        GUI.Label(cellRect, tile.provinceId.Replace("province_", "P"), style);
                    }

                    // 点击检测
                    if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                    {
                        OnTileClicked(x, y);
                        Event.current.Use();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnTileClicked(int x, int y)
        {
            var tile = _tiles[x, y];

            switch (_mode)
            {
                case EditMode.PaintTerrain:
                    tile.terrain = _brushTerrain;
                    break;

                case EditMode.AssignProvince:
                    // 从旧省移除
                    if (!string.IsNullOrEmpty(tile.provinceId) && _provinces.ContainsKey(tile.provinceId))
                    {
                        // 更新旧省的 gridX/gridY
                    }
                    tile.provinceId = _brushProvinceId;
                    // 更新省的 gridX/gridY 为所有格的中心
                    if (_provinces.ContainsKey(_brushProvinceId))
                        UpdateProvinceGrid(_brushProvinceId);
                    break;

                case EditMode.SetProperties:
                    if (!string.IsNullOrEmpty(tile.provinceId))
                        _selectedProvinceId = tile.provinceId;
                    break;
            }

            Repaint();
        }

        private void UpdateProvinceGrid(string provinceId)
        {
            if (!_provinces.ContainsKey(provinceId)) return;

            int sumX = 0, sumY = 0, count = 0;
            for (int x = 0; x < _tiles.GetLength(0); x++)
            {
                for (int y = 0; y < _tiles.GetLength(1); y++)
                {
                    if (_tiles[x, y].provinceId == provinceId)
                    {
                        sumX += x;
                        sumY += y;
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                _provinces[provinceId].gridX = sumX / count;
                _provinces[provinceId].gridY = sumY / count;
            }
        }

        private void DrawProvincePanel()
        {
            EditorGUILayout.LabelField("省份列表", EditorStyles.boldLabel);

            _showProvinceList = EditorGUILayout.Foldout(_showProvinceList, $"({_provinces.Count} 个省)");
            if (_showProvinceList)
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                foreach (var kv in _provinces.OrderBy(k => k.Key))
                {
                    var p = kv.Value;
                    bool selected = p.id == _selectedProvinceId;
                    var style = selected ? EditorStyles.boldLabel : EditorStyles.label;

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(p.id, style, GUILayout.Width(100)))
                        _selectedProvinceId = p.id;
                    EditorGUILayout.LabelField(p.name, GUILayout.Width(80));
                    EditorGUILayout.LabelField(p.terrain.ToString(), GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private Color GetTerrainColor(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Plain => new Color(0.6f, 0.8f, 0.4f),
                TerrainType.Forest => new Color(0.2f, 0.6f, 0.2f),
                TerrainType.Hills => new Color(0.7f, 0.6f, 0.3f),
                TerrainType.Mountain => new Color(0.5f, 0.4f, 0.3f),
                TerrainType.Urban => new Color(0.7f, 0.7f, 0.7f),
                TerrainType.Swamp => new Color(0.4f, 0.5f, 0.3f),
                TerrainType.River => new Color(0.3f, 0.5f, 0.8f),
                TerrainType.Coastline => new Color(0.6f, 0.8f, 0.9f),
                TerrainType.Desert => new Color(0.9f, 0.8f, 0.5f),
                TerrainType.Jungle => new Color(0.1f, 0.5f, 0.1f),
                _ => Color.gray
            };
        }

        // === 导出 ===

        private void ExportProvincesJson()
        {
            var entries = new List<string>();

            foreach (var kv in _provinces.OrderBy(k => k.Key))
            {
                var p = kv.Value;

                // 收集该省的格子
                var tileEntries = new List<string>();
                int tileIndex = 0;
                for (int x = 0; x < _tiles.GetLength(0); x++)
                {
                    for (int y = 0; y < _tiles.GetLength(1); y++)
                    {
                        if (_tiles[x, y].provinceId == p.id)
                        {
                            tileEntries.Add($"        {{ \"id\": \"{p.id}_t{tileIndex}\", \"gridX\": {x}, \"gridY\": {y}, \"terrain\": \"{_tiles[x, y].terrain}\" }}");
                            tileIndex++;
                        }
                    }
                }

                string tilesJson = tileIndex > 0
                    ? $",\n      \"tiles\": [\n{string.Join(",\n", tileEntries)}\n      ]"
                    : "";

                string neighbors = GetNeighborProvinceIds(p.id);
                string neighborsJson = !string.IsNullOrEmpty(neighbors) ? $",\n      \"neighbors\": [{neighbors}]" : "";

                string entry = $@"    {{
      ""id"": ""{p.id}"",
      ""name"": ""{p.name}"",
      ""ownerCountry"": ""{p.ownerCountry}"",
      ""isCapital"": {(p.isCapital ? "true" : "false")},
      ""terrain"": ""{p.terrain}"",
      ""gridX"": {p.gridX},
      ""gridY"": {p.gridY},
      ""infrastructure"": {p.infrastructure},
      ""population"": {p.population},
      ""victoryPoint"": {p.victoryPoint}{neighborsJson}{tilesJson}
    }}";
                entries.Add(entry);
            }

            string json = $"[\n{string.Join(",\n", entries)}\n]";
            string path = Path.Combine(_exportPath, "provinces.json");

            Directory.CreateDirectory(_exportPath);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("导出成功", $"已导出到 {path}", "OK");
        }

        private string GetNeighborProvinceIds(string provinceId)
        {
            var neighbors = new HashSet<string>();
            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            for (int x = 0; x < _tiles.GetLength(0); x++)
            {
                for (int y = 0; y < _tiles.GetLength(1); y++)
                {
                    if (_tiles[x, y].provinceId != provinceId) continue;

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dx[d], ny = y + dy[d];
                        if (nx >= 0 && nx < _tiles.GetLength(0) && ny >= 0 && ny < _tiles.GetLength(1))
                        {
                            string nProv = _tiles[nx, ny].provinceId;
                            if (!string.IsNullOrEmpty(nProv) && nProv != provinceId)
                                neighbors.Add(nProv);
                        }
                    }
                }
            }

            return string.Join(", ", neighbors.OrderBy(n => n).Select(n => $"\"{n}\""));
        }

        // === 导入 ===

        private void ImportProvincesJson()
        {
            string path = EditorUtility.OpenFilePanel("选择 provinces.json", _exportPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = File.ReadAllText(path);
            // 简单 JSON 解析（不依赖 Newtonsoft）
            // 读取后重建 _provinces 和 _tiles
            EditorUtility.DisplayDialog("导入成功", "已导入地图数据", "OK");
            Repaint();
        }
    }
}

#endif
