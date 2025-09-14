using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // for IEnumerator

public class GridOverlayManager : MonoBehaviour
{
    // 兼容旧枚举名：ShowSector/ShowArena 作为别名
    public enum OverlayMode
    {
        None = 0,
        Sector = 1,
        Arena = 2,
        Collision = 3,
        ShowSector = Sector,
        ShowArena = Arena
    }

    public OverlayMode currentMode = OverlayMode.None;
    public static GridOverlayManager Instance;

    [Header("Overlay 选择（用 TMP_Dropdown）")]
    public TMP_Dropdown overlayDropdown;

    [Header("Label 预制体 (必须包含 TextMeshProUGUI)")]
    public GameObject labelPrefab;

    [Header("Overlay 方块参数")]
    [Range(0f, 1f)] public float overlaySaturation = 0.5f;
    [Range(0f, 1f)] public float overlayValue = 0.70f;
    [Range(0f, 1f)] public float overlayAlpha = 0.9f;

    [Header("Label 字体大小限制")]
    public float labelFontSizeMax = 24f;
    public float labelFontSizeMin = 8f;

    [Header("行为选项")]
    public bool showCollisionLabels = false;
    public string collisionLabelText = "Blocked";
    public bool includeCollisionInToggle = false;   // 旧按钮轮换是否包含 Collision

    private MapManager mapMgr;
    private float cellSize;
    private int mapWidth, mapHeight;

    // —— 保留标签列表 —— //
    private readonly List<GameObject> overlayLabels = new List<GameObject>();

    // —— 新增：方块池 —— //
    private Image[] _cells;          // 长度 = mapWidth * mapHeight
    private int _cellsW, _cellsH;    // 当前池尺寸

    // —— 新增：两个专用容器，始终置顶（标签在格子之上） —— //
    private RectTransform _cellsRoot;   // "OverlayCells"
    private RectTransform _labelsRoot;  // "OverlayLabels"

    // —— 差异更新缓存 —— //
    private int[] _lastIds;                 // 长度 = _cells.Length
    private bool _cacheValid = false;
    private OverlayMode _lastModeForCache = OverlayMode.None;

    private float nextRebuildAllowedTime = 0f;
    private const float rebuildDebounce = 0.05f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        mapMgr = MapManager.Instance;
    }

    void Start()
    {
        if (mapMgr == null)
        {
            Debug.LogError("GridOverlayManager: 找不到 MapManager 实例！");
            return;
        }

        // 初次缓存（渲染前还会再取一遍，保证稳）
        SafeUpdateDims();

        if (overlayDropdown != null)
        {
            overlayDropdown.onValueChanged.AddListener(OnOverlayModeChanged);
            overlayDropdown.SetValueWithoutNotify((int)currentMode);
        }

        // —— 启动即：池化格子 + 预重建全局 Maze（不等切视图）—— //
        StartCoroutine(PrimeOnStart());
    }

    private IEnumerator PrimeOnStart()
    {
        // 等待 mapMgr / mapMeta / mapImage 初始化完毕
        while (mapMgr == null || mapMgr.mapMeta == null || mapMgr.mapImage == null)
            yield return null;

        // 再等一帧，确保背景贴图与缩放布局稳定
        yield return null;

        SafeUpdateDims();
        EnsureCells(); // ① 一次性创建所有格子（置顶）

        // ② 启动就预重建 Maze（会打印 block mapping 的日志一次）
        yield return StartCoroutine(mapMgr.RebuildMapMazeDataCoroutine(
            yieldEveryItems: 1,
            yieldEveryRows: 8
        ));

        // 重建后失效缓存，避免旧缓存短路
        _cacheValid = false;
        _lastModeForCache = OverlayMode.None;

        // ③ 默认不显示覆盖层的话（currentMode=None），也先把池套一遍空网格以隐藏并确保层级正确
        ApplyGridDelta(EmptyGrid());
        MaintainOverlayOrder();

        // ④ 如果你想进场就显示某一层，可在 Inspector 把 currentMode 设为 Sector/Arena/Collision；
        //    这里不强制触发，保持和 Inspector 一致。
        if (currentMode != OverlayMode.None)
            RefreshWithOverlay();
    }

    void Update()
    {
        if (currentMode != OverlayMode.None && mapMgr.isDirty)
        {
            if (Time.unscaledTime >= nextRebuildAllowedTime)
            {
                nextRebuildAllowedTime = Time.unscaledTime + rebuildDebounce;

                // 刷新前失效缓存（很关键）
                _cacheValid = false;
                _lastModeForCache = OverlayMode.None;

                Debug.Log("[GridOverlayManager] isDirty=true → RefreshWithOverlay()");
                RefreshWithOverlay();
            }
        }
    }

    // —— 新接口 —— //
    public void OnOverlayModeChanged(int idx)
    {
        // 切视图先失效缓存，保证强制比较
        _cacheValid = false;
        _lastModeForCache = OverlayMode.None;

        currentMode = (OverlayMode)idx;
        Debug.Log($"[GridOverlayManager] OnOverlayModeChanged → {currentMode}");
        RefreshWithOverlay();
    }

    public void RefreshWithOverlay()
    {
        StartCoroutine(RefreshRoutine());
    }

    private System.Collections.IEnumerator RefreshRoutine()
    {
        if (LoadingOverlay.Instance != null)
        {
            LoadingOverlay.Instance.Show("正在生成覆盖层…");
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
        }

        // —— 分帧重建（如需） —— //
        bool didRebuild = false;
        if (!mapMgr.hasMazeBuilt || mapMgr.isDirty)
        {
            // 用分帧协程重建，期间会多次 yield，让面板可见且动起来
            yield return StartCoroutine(mapMgr.RebuildMapMazeDataCoroutine(
                yieldEveryItems: 1,   // 每处理一个物体就 yield 一次
                yieldEveryRows: 8     // 每 8 行 yield 一次
            ));
            didRebuild = true;
        }

        // 重建后失效缓存，确保本轮渲染不会被旧缓存短路
        if (didRebuild)
        {
            _cacheValid = false;
            _lastModeForCache = OverlayMode.None;
        }

        // —— 分帧渲染（仅标签需要分帧；方块改为一次性变色/显隐） —— //
        yield return StartCoroutine(RenderOverlayCoroutine(
            yieldEveryRows: 8, // 标签每 8 行 yield 一次
            withLabels: true
        ));

        if (LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.Hide();
    }

    // —— 兼容旧接口 —— //
    public void RefreshOverlay()
    {
        if (currentMode == OverlayMode.None)
        {
            ClearOverlayNodes();           // 现仅清标签
            ApplyGridDelta(EmptyGrid());   // 全隐藏（不销毁池）
            MaintainOverlayOrder();
            return;
        }

        if (!mapMgr.hasMazeBuilt || mapMgr.isDirty)
        {
            // 未构建或已脏 → 走协程（可显示遮罩）
            Debug.Log("[GridOverlayManager] RefreshOverlay() → fallback to RefreshWithOverlay()");
            RefreshWithOverlay();
        }
        else
        {
            // 已构建且不脏 → 直接重画
            Debug.Log("[GridOverlayManager] RefreshOverlay() → RenderOverlay()");
            RenderOverlay();
        }
    }

    public void ToggleOverlayMode()
    {
        OverlayMode[] order = includeCollisionInToggle
            ? new[] { OverlayMode.None, OverlayMode.Sector, OverlayMode.Arena, OverlayMode.Collision }
            : new[] { OverlayMode.None, OverlayMode.Sector, OverlayMode.Arena };

        int idx = System.Array.IndexOf(order, currentMode);
        if (idx < 0) idx = 0;
        currentMode = order[(idx + 1) % order.Length];

        if (overlayDropdown != null)
            overlayDropdown.SetValueWithoutNotify((int)currentMode);

        Debug.Log($"[GridOverlayManager] ToggleOverlayMode → {currentMode}");
        // 切模式前后都确保缓存失效
        _cacheValid = false; _lastModeForCache = OverlayMode.None;
        RefreshWithOverlay();
    }

    // —— 渲染（同步） —— //
    private void RenderOverlay()
    {
        SafeUpdateDims(); // 防止早期取值为 0
        EnsureCells();

        ClearOverlayNodes(); // 现在只清标签

        if (currentMode == OverlayMode.None)
        {
            ApplyGridDelta(EmptyGrid()); // 全隐藏
            MaintainOverlayOrder();
            return;
        }

        int[,] gridData = null;
        switch (currentMode)
        {
            case OverlayMode.Sector: gridData = mapMgr.mapSectorMaze; break;
            case OverlayMode.Arena: gridData = mapMgr.mapArenaMaze; break;
            case OverlayMode.Collision: gridData = mapMgr.mapCollisionMaze; break;
        }
        if (gridData == null)
        {
            Debug.LogWarning("[GridOverlayManager] gridData is null");
            ApplyGridDelta(EmptyGrid());
            MaintainOverlayOrder();
            return;
        }

        // 1) 方块：只做差异更新（变色/显隐）
        ApplyGridDelta(gridData);
        MaintainOverlayOrder();

        // 2) 标签
        bool doLabels = (currentMode == OverlayMode.Sector || currentMode == OverlayMode.Arena) ||
                        (currentMode == OverlayMode.Collision && showCollisionLabels);
        if (!doLabels || labelPrefab == null) return;

        BuildLabelsSync(gridData);
        MaintainOverlayOrder();
    }

    // —— 渲染（协程，分帧） —— //
    private System.Collections.IEnumerator RenderOverlayCoroutine(int yieldEveryRows = 8, bool withLabels = true)
    {
        SafeUpdateDims();
        EnsureCells();

        ClearOverlayNodes(); // 仅清标签

        if (currentMode == OverlayMode.None)
        {
            ApplyGridDelta(EmptyGrid());
            MaintainOverlayOrder();
            yield break;
        }

        int[,] gridData = null;
        bool wantLabels = false;
        switch (currentMode)
        {
            case OverlayMode.Sector: gridData = mapMgr.mapSectorMaze; wantLabels = true; break;
            case OverlayMode.Arena: gridData = mapMgr.mapArenaMaze; wantLabels = true; break;
            case OverlayMode.Collision: gridData = mapMgr.mapCollisionMaze; wantLabels = showCollisionLabels; break;
        }
        if (gridData == null) { ApplyGridDelta(EmptyGrid()); MaintainOverlayOrder(); yield break; }

        // 1) 方块：一次性差异更新
        ApplyGridDelta(gridData);
        MaintainOverlayOrder();

        // 2) 标签（分帧）
        if (!withLabels || !wantLabels || labelPrefab == null) yield break;

        bool[,] visited = new bool[mapHeight, mapWidth];
        int rowsDone = 0;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int id = gridData[y, x];
                if (id <= 0 || visited[y, x]) continue;

                var queue = new Queue<Vector2Int>();
                var region = new List<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                visited[y, x] = true;

                int minX = x, maxX = x;
                while (queue.Count > 0)
                {
                    var v = queue.Dequeue();
                    region.Add(v);
                    minX = Mathf.Min(minX, v.x);
                    maxX = Mathf.Max(maxX, v.x);

                    var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
                    foreach (var d in dirs)
                    {
                        int nx = v.x + d.x, ny = v.y + d.y;
                        if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight && !visited[ny, nx] && gridData[ny, nx] == id)
                        {
                            visited[ny, nx] = true;
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }

                float sumX = 0, sumY = 0;
                foreach (var c in region) { sumX += c.x; sumY += c.y; }
                float cx = sumX / region.Count, cy = sumY / region.Count;

                int cols = maxX - minX + 1;
                float availW = cols * cellSize;

                var labGO = Instantiate(labelPrefab, _labelsRoot != null ? _labelsRoot : mapMgr.mapContent, false);
                var label = labGO.GetComponent<TextMeshProUGUI>();

                // 标签不拦截点击
                var labelGraphic = labGO.GetComponent<Graphic>();
                if (labelGraphic != null) labelGraphic.raycastTarget = false;

                if (currentMode == OverlayMode.Collision && showCollisionLabels)
                    label.text = string.IsNullOrEmpty(collisionLabelText) ? id.ToString() : collisionLabelText;
                else
                    label.text = mapMgr.GetBlockName(id, currentMode == OverlayMode.Sector);

                label.enableAutoSizing = true;
                label.fontSizeMin = 0f;
                label.fontSizeMax = labelFontSizeMax;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.alignment = TextAlignmentOptions.Center;

                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(availW, cellSize);
                lrt.anchoredPosition = new Vector2(
                    cx * cellSize + cellSize / 2f,
                    -cy * cellSize - cellSize / 2f
                );

                overlayLabels.Add(labGO);
            }

            rowsDone++;
            if (rowsDone % yieldEveryRows == 0)
                yield return null; // 再让一帧过去
        }

        MaintainOverlayOrder();
    }

    // —— 仅清“标签”；不再销毁方块池 —— //
    private void ClearOverlayNodes()
    {
        foreach (var go in overlayLabels) if (go) Destroy(go);
        overlayLabels.Clear();

        // 保险：如果外部有人手动往 _labelsRoot 里塞东西，也一并清掉
        if (_labelsRoot != null)
        {
            for (int i = _labelsRoot.childCount - 1; i >= 0; i--)
                Destroy(_labelsRoot.GetChild(i).gameObject);
        }
    }

    private void SafeUpdateDims()
    {
        if (mapMgr == null || mapMgr.mapMeta == null) return;
        cellSize = mapMgr.gridSize * mapMgr.backgroundScaleFactor;
        mapWidth = mapMgr.mapMeta.maze_width;
        mapHeight = mapMgr.mapMeta.maze_height;
    }

    private Color GenerateColorForID(int id)
    {
        float hue = ((id * 137) % 360) / 360f;
        Color rgb = Color.HSVToRGB(hue, overlaySaturation, overlayValue);
        rgb.a = overlayAlpha;
        return rgb;
    }

    // —— 一次性创建全部方块；尺寸/缩放变化时会重建；并把容器置顶 —— //
    private void EnsureCells()
    {
        SafeUpdateDims();

        // 尺寸没变且已创建过，直接复用并维持层级
        if (_cells != null && _cellsW == mapWidth && _cellsH == mapHeight && _cellsRoot != null && _labelsRoot != null)
        {
            MaintainOverlayOrder();
            return;
        }

        // 清理旧容器
        if (_cellsRoot != null) Destroy(_cellsRoot.gameObject);
        if (_labelsRoot != null) Destroy(_labelsRoot.gameObject);

        // 1) 创建方块容器
        var cellsGO = new GameObject("OverlayCells", typeof(RectTransform));
        _cellsRoot = cellsGO.GetComponent<RectTransform>();
        _cellsRoot.SetParent(mapMgr.mapContent, false);
        _cellsRoot.anchorMin = _cellsRoot.anchorMax = new Vector2(0, 1);
        _cellsRoot.pivot = new Vector2(0, 1);
        _cellsRoot.anchoredPosition = Vector2.zero;

        // 2) 创建标签容器（始终在方块之上）
        var labelsGO = new GameObject("OverlayLabels", typeof(RectTransform));
        _labelsRoot = labelsGO.GetComponent<RectTransform>();
        _labelsRoot.SetParent(mapMgr.mapContent, false);
        _labelsRoot.anchorMin = _labelsRoot.anchorMax = new Vector2(0, 1);
        _labelsRoot.pivot = new Vector2(0, 1);
        _labelsRoot.anchoredPosition = Vector2.zero;

        // 放到最顶层：先方块，再标签（标签在最上）
        _cellsRoot.SetAsLastSibling();
        _labelsRoot.SetAsLastSibling();

        _cellsW = mapWidth; _cellsH = mapHeight;
        _cells = new Image[_cellsW * _cellsH];

        float sz = cellSize;
        for (int y = 0; y < _cellsH; y++)
        {
            for (int x = 0; x < _cellsW; x++)
            {
                var go = new GameObject($"Overlay_{x}_{y}", typeof(Image));
                go.transform.SetParent(_cellsRoot, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;   // 不拦截点击
                img.enabled = false;         // 初始隐藏

                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(sz, sz);
                rt.anchoredPosition = new Vector2(x * sz, -y * sz);

                _cells[y * _cellsW + x] = img;
            }
        }

        // 重置差异缓存
        _lastIds = new int[_cellsW * _cellsH];
        for (int i = 0; i < _lastIds.Length; i++) _lastIds[i] = int.MinValue;
        _cacheValid = false;
        _lastModeForCache = OverlayMode.None;
    }

    // —— 根据网格数据做“差异上色/显隐”（不创建/销毁节点） —— //
    private void ApplyGridDelta(int[,] gridData)
    {
        EnsureCells();

        if (_lastIds == null || _lastIds.Length != _cells.Length)
        {
            _lastIds = new int[_cells.Length];
            for (int i = 0; i < _lastIds.Length; i++) _lastIds[i] = int.MinValue;
            _cacheValid = false;
            _lastModeForCache = OverlayMode.None;
        }

        bool modeChanged = (_lastModeForCache != currentMode);
        int w = _cellsW, h = _cellsH;

        int changed = 0; // 可选：统计变化

        for (int y = 0; y < h; y++)
        {
            int rowBase = y * w;
            for (int x = 0; x < w; x++)
            {
                int idx = rowBase + x;
                int newId = (currentMode != OverlayMode.None) ? gridData[y, x] : 0;

                int oldId = (_cacheValid && !modeChanged) ? _lastIds[idx] : int.MinValue;
                if (newId == oldId) continue;

                Image img = _cells[idx];
                if (newId > 0)
                {
                    img.enabled = true;
                    img.color = GenerateColorForID(newId);
                }
                else
                {
                    img.enabled = false;
                }
                _lastIds[idx] = newId;
                changed++;
            }
        }

        _cacheValid = true;
        _lastModeForCache = currentMode;

        // 可选日志
        // Debug.Log($"[Overlay] ApplyGridDelta done. changed cells = {changed}, mode={currentMode}");
    }

    // —— None 模式时使用：返回全 0 网格（全部隐藏） —— //
    private int[,] EmptyGrid()
    {
        var g = new int[mapHeight, mapWidth]; // 默认值全 0
        return g;
    }

    // —— 同步标签生成（给 RenderOverlay 用） —— //
    private void BuildLabelsSync(int[,] gridData)
    {
        bool[,] visited = new bool[mapHeight, mapWidth];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int id = gridData[y, x];
                if (id <= 0 || visited[y, x]) continue;

                var queue = new Queue<Vector2Int>();
                var region = new List<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                visited[y, x] = true;

                int minX = x, maxX = x;
                while (queue.Count > 0)
                {
                    var v = queue.Dequeue();
                    region.Add(v);
                    minX = Mathf.Min(minX, v.x);
                    maxX = Mathf.Max(maxX, v.x);

                    var dirs = new[] {
                        new Vector2Int(1,0), new Vector2Int(-1,0),
                        new Vector2Int(0,1), new Vector2Int(0,-1)
                    };
                    foreach (var d in dirs)
                    {
                        int nx = v.x + d.x, ny = v.y + d.y;
                        if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight
                            && !visited[ny, nx] && gridData[ny, nx] == id)
                        {
                            visited[ny, nx] = true;
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }

                float sumX = 0, sumY = 0;
                foreach (var c in region) { sumX += c.x; sumY += c.y; }
                float cx = sumX / region.Count, cy = sumY / region.Count;

                int cols = maxX - minX + 1;
                float availW = cols * cellSize;

                var labGO = Instantiate(labelPrefab, _labelsRoot != null ? _labelsRoot : mapMgr.mapContent, false);
                var label = labGO.GetComponent<TextMeshProUGUI>();

                // 标签不拦截点击
                var labelGraphic = labGO.GetComponent<Graphic>();
                if (labelGraphic != null) labelGraphic.raycastTarget = false;

                if (currentMode == OverlayMode.Collision && showCollisionLabels)
                {
                    label.text = string.IsNullOrEmpty(collisionLabelText) ? id.ToString() : collisionLabelText;
                }
                else
                {
                    bool isSector = (currentMode == OverlayMode.Sector);
                    label.text = mapMgr.GetBlockName(id, isSector);
                }

                label.enableAutoSizing = true;
                label.fontSizeMin = 0f;
                label.fontSizeMax = labelFontSizeMax;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.alignment = TextAlignmentOptions.Center;

                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(availW, cellSize);
                lrt.anchoredPosition = new Vector2(
                    cx * cellSize + cellSize / 2f,
                    -cy * cellSize - cellSize / 2f
                );

                overlayLabels.Add(labGO);
            }
        }
    }

    // —— 保持容器位于最顶层（标签在最上） —— //
    private void MaintainOverlayOrder()
    {
        if (_cellsRoot == null || _labelsRoot == null) return;
        _cellsRoot.SetAsLastSibling();  // 先把格子提到顶
        _labelsRoot.SetAsLastSibling(); // 再把标签提到最顶
    }
}
