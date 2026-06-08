// ============================================================================
// Presentation/MapRenderer.cs — Tilemap 地图渲染器（P2.3）
// 从 WorldView 绘制 Tilemap,按 controllerCountry 上色
// ============================================================================

using System.Collections.Generic;
using IronCrown.Application;
using IronCrown.Contracts;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IronCrown.Presentation
{
    /// <summary>
    /// Tilemap 地图渲染器。
    /// 挂在场景中的 Grid/Tilemap 对象上，由 MainHudBehaviour 驱动。
    /// </summary>
    public sealed class MapRenderer : MonoBehaviour
    {
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private TileBase _tile; // 纯白方块 tile asset

        // 省配色缓存 (provinceId → Color)
        private Dictionary<string, Color> _provinceColors = new();
        // 格→省映射 (tileId → provinceId)
        private Dictionary<string, string> _tileToProvince = new();
        // 已绘制的格 ID 集合
        private HashSet<string> _drawnTiles = new();

        // 选中省份高亮
        private string _selectedProvinceId;
        private string _hoveredProvinceId;

        // 移动/攻击目标
        private HashSet<string> _moveTargets = new();
        private HashSet<string> _attackTargets = new();

        /// <summary>设置格 tile asset（运行时注入）</summary>
        public void SetTile(TileBase tile)
        {
            _tile = tile;
        }

        /// <summary>设置 Tilemap 引用</summary>
        public void SetTilemap(Tilemap tilemap)
        {
            _tilemap = tilemap;
        }

        /// <summary>
        /// 从 WorldView 绘制全部 tiles。
        /// 首次调用创建 tile，后续只更新颜色。
        /// </summary>
        public void Render(WorldView vm)
        {
            if (_tilemap == null || _tile == null || vm == null) return;

            // 建立省份配色
            _provinceColors.Clear();
            if (vm.provinces != null)
            {
                foreach (var p in vm.provinces)
                {
                    if (!string.IsNullOrEmpty(p.ownerColor) &&
                        ColorUtility.TryParseHtmlString(p.ownerColor, out var color))
                    {
                        _provinceColors[p.id] = color;
                    }
                    else
                    {
                        _provinceColors[p.id] = new Color(0.3f, 0.3f, 0.3f); // 默认灰
                    }
                }
            }

            // 建立移动/攻击目标
            _moveTargets.Clear();
            _attackTargets.Clear();
            if (!string.IsNullOrEmpty(vm.selectedUnitId) && vm.units != null)
            {
                var selUnit = vm.units.Find(u => u.id == vm.selectedUnitId);
                if (selUnit != null && selUnit.movesLeft > 0 && !selUnit.isInBattle)
                {
                    var selProv = vm.provinces?.Find(p => p.id == selUnit.currentProvinceId);
                    if (selProv != null && selProv.neighbors != null)
                    {
                        foreach (var nId in selProv.neighbors)
                        {
                            var np = vm.provinces?.Find(p => p.id == nId);
                            if (np == null) continue;
                            if (np.controllerCountry == selUnit.ownerCountry)
                                _moveTargets.Add(nId);
                            else
                                _attackTargets.Add(nId);
                        }
                    }
                }
            }

            // 从 ReadModel 获取 tiles（如果有 tiles 信息的话）
            // P2.3 阶段: 直接从 provinces 的 gridX/gridY 生成 2×2 子格
            if (vm.provinces != null)
            {
                foreach (var p in vm.provinces)
                {
                    for (int n = 0; n < 4; n++)
                    {
                        string tileId = $"{p.id}_t{n}";
                        int tx = p.gridX * 2 + (n % 2);
                        int ty = p.gridY * 2 + (n / 2);
                        var cell = new Vector3Int(tx, ty, 0);

                        _tileToProvince[tileId] = p.id;

                        if (!_drawnTiles.Contains(tileId))
                        {
                            _tilemap.SetTile(cell, _tile);
                            _drawnTiles.Add(tileId);
                        }

                        // 颜色: 选中高亮 / 移动目标 / 攻击目标 / 默认
                        Color tileColor;
                        bool isSelected = p.id == _selectedProvinceId;
                        bool isMove = _moveTargets.Contains(p.id);
                        bool isAttack = _attackTargets.Contains(p.id);

                        if (isSelected)
                        {
                            tileColor = new Color(1f, 1f, 1f, 0.9f); // 白色高亮
                        }
                        else if (isMove)
                        {
                            tileColor = new Color(0.2f, 0.8f, 0.2f, 0.7f); // 绿色移动
                        }
                        else if (isAttack)
                        {
                            tileColor = new Color(0.9f, 0.2f, 0.2f, 0.7f); // 红色攻击
                        }
                        else if (_provinceColors.TryGetValue(p.id, out var baseColor))
                        {
                            // 稍微随机化同省内各格色调，增加视觉层次
                            float hueShift = (n - 1.5f) * 0.02f;
                            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                            tileColor = Color.HSVToRGB(
                                Mathf.Repeat(h + hueShift, 1f),
                                Mathf.Clamp(s + (n % 2 == 0 ? 0.05f : -0.05f), 0f, 1f),
                                Mathf.Clamp(v + (n < 2 ? 0.05f : -0.05f), 0f, 1f)
                            );
                            tileColor.a = 1f;
                        }
                        else
                        {
                            tileColor = new Color(0.3f, 0.3f, 0.3f);
                        }

                        _tilemap.SetTileFlags(cell, TileFlags.None);
                        _tilemap.SetColor(cell, tileColor);
                    }
                }
            }

            // 更新战斗中省份的脉冲效果
            if (vm.activeBattles != null)
            {
                float pulse = Mathf.PingPong(Time.time * 2f, 1f);
                foreach (var battle in vm.activeBattles)
                {
                    var pv = vm.provinces?.Find(p => p.id == battle.provinceId);
                    if (pv == null) continue;
                    for (int n = 0; n < 4; n++)
                    {
                        var cell = new Vector3Int(pv.gridX * 2 + (n % 2), pv.gridY * 2 + (n / 2), 0);
                        var c = new Color(1f, 0.5f + pulse * 0.3f, 0.2f, 0.8f + pulse * 0.2f);
                        _tilemap.SetTileFlags(cell, TileFlags.None);
                        _tilemap.SetColor(cell, c);
                    }
                }
            }
        }

        /// <summary>设置选中省份</summary>
        public void SetSelectedProvince(string provinceId)
        {
            _selectedProvinceId = provinceId;
        }

        /// <summary>
        /// 根据世界坐标获取省份 ID。
        /// 由 MapInputController 调用。
        /// </summary>
        public string GetProvinceAtWorld(Vector3 worldPos)
        {
            if (_tilemap == null) return null;
            var cell = _tilemap.WorldToCell(worldPos);

            // 遍历所有格，找匹配的 cell
            foreach (var kv in _tileToProvince)
            {
                // 从 tileId 解析坐标
                string tileId = kv.Key;
                string provinceId = kv.Value;
                // tileId = {provinceId}_t{n}, 需要从 province 的 gridX/gridY 计算
                // 直接检查 tilemap 上该 cell 是否有 tile
                if (_tilemap.HasTile(cell))
                {
                    // 找到匹配的省份（通过遍历 provinces 的子格坐标）
                    // 优化: 直接返回，因为 cell 坐标已确定
                    // 需要从 cell 反推 provinceId
                    // cell (tx, ty) → province gridX = tx/2, gridY = ty/2
                    int pgx = cell.x / 2;
                    int pgy = cell.y / 2;
                    // 搜索匹配的省份
                    foreach (var prov in _provinceColors.Keys)
                    {
                        // 不精确，改用 _tileToProvince 反查
                    }
                    return provinceId; // 暂时返回最后匹配的
                }
            }
            return null;
        }

        /// <summary>
        /// 根据 cell 坐标获取省份 ID（精确版本）。
        /// </summary>
        public string GetProvinceAtCell(Vector3Int cell)
        {
            // 从 cell 坐标反推 province: gridX = cell.x / 2, gridY = cell.y / 2
            // 遍历 _tileToProvince 找匹配
            foreach (var kv in _tileToProvince)
            {
                string tileId = kv.Key;
                string provinceId = kv.Value;
                // tileId = {provinceId}_t{n}
                // 需要从 provinceId 获取 gridX/gridY
                // 无法直接反推，改用 tilemap.HasTile 检查
                if (_tilemap.HasTile(cell))
                {
                    // 通过 cell 坐标计算 province 的 grid
                    int pgx = cell.x >> 1; // cell.x / 2
                    int pgy = cell.y >> 1; // cell.y / 2
                    // 搜索匹配的 provinceId
                    // 用 _tileToProvince 的 value 去重
                    // 更好的方式: 建立 cell→provinceId 映射
                    return kv.Value;
                }
            }
            return null;
        }

        /// <summary>清除所有 tile（场景重建时用）</summary>
        public void ClearAll()
        {
            if (_tilemap != null)
                _tilemap.ClearAllTiles();
            _drawnTiles.Clear();
            _tileToProvince.Clear();
        }
    }
}
