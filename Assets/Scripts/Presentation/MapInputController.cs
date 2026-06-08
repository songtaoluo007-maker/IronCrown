// ============================================================================
// Presentation/MapInputController.cs — 地图输入控制器（P2.3）
// 鼠标点击 → cell → province → SelectProvince
// 相机缩放/平移
// ============================================================================

using IronCrown.Application;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IronCrown.Presentation
{
    /// <summary>
    /// 地图输入控制器。
    /// 处理鼠标点击选省、相机缩放平移。
    /// </summary>
    public sealed class MapInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private MapRenderer _mapRenderer;

        private GameSessionService _session;
        private MainHudController _hud;

        // 相机控制
        private float _zoomSpeed = 2f;
        private float _panSpeed = 0.5f;
        private float _minZoom = 2f;
        private float _maxZoom = 15f;
        private bool _isPanning;
        private Vector3 _panStart;

        public void Initialize(GameSessionService session, MainHudController hud)
        {
            _session = session;
            _hud = hud;
        }

        private void Update()
        {
            if (_session == null) return;

            HandleZoom();
            HandlePan();
            HandleClick();
        }

        private void HandleClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // 检查是否点击在 UI 上
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Vector3 worldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0;

            if (_tilemap == null) return;

            Vector3Int cell = _tilemap.WorldToCell(worldPos);
            if (!_tilemap.HasTile(cell)) return;

            // 从 cell 坐标推算省份: gridX = cell.x / 2, gridY = cell.y / 2
            // 通过 MapRenderer 获取省份 ID
            string provinceId = FindProvinceAtCell(cell);
            if (string.IsNullOrEmpty(provinceId)) return;

            // 更新选中高亮
            if (_mapRenderer != null)
                _mapRenderer.SetSelectedProvince(provinceId);

            // 调用 HUD 的选省逻辑
            if (_hud != null)
                _hud.SelectProvince(provinceId);
        }

        /// <summary>从 cell 坐标精确查找省份 ID</summary>
        private string FindProvinceAtCell(Vector3Int cell)
        {
            // cell (tx, ty) → province gridX = tx / 2, gridY = ty / 2
            // 遍历 WorldView 的 provinces 查找匹配
            if (_session == null) return null;
            var vm = _session.GetWorldView();
            if (vm?.provinces == null) return null;

            int pgx = cell.x >> 1;
            int pgy = cell.y >> 1;

            foreach (var p in vm.provinces)
            {
                if (p.gridX == pgx && p.gridY == pgy)
                    return p.id;
            }
            return null;
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;

            if (_camera == null) return;
            float newSize = _camera.orthographicSize - scroll * _zoomSpeed;
            _camera.orthographicSize = Mathf.Clamp(newSize, _minZoom, _maxZoom);
        }

        private void HandlePan()
        {
            // 中键拖拽 或 右键拖拽
            if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
            {
                _isPanning = true;
                _panStart = _camera.ScreenToWorldPoint(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
            {
                _isPanning = false;
            }

            if (_isPanning && _camera != null)
            {
                Vector3 current = _camera.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = _panStart - current;
                _camera.transform.position += delta;
            }
        }
    }
}
