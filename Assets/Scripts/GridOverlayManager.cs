// 文件: GridOverlayManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridOverlayManager : MonoBehaviour
{
    public enum OverlayMode { None, ShowSector, ShowArena }
    public OverlayMode currentMode = OverlayMode.None;
    public static GridOverlayManager Instance;

    [Header("Overlay 切换按钮")]
    public Button toggleOverlayButton;

    [Header("Label 预制体 (必须包含 TextMeshProUGUI)")]
    public GameObject labelPrefab;

    [Header("Overlay 方块参数")]
    [Range(0f, 1f)] public float overlaySaturation = 0.5f;
    [Range(0f, 1f)] public float overlayValue = 0.70f;
    [Range(0f, 1f)] public float overlayAlpha = 0.9f;

    [Header("Label 字体大小限制")]
    public float labelFontSizeMax = 24f;
    public float labelFontSizeMin = 8f;

    private MapManager mapMgr;
    private float cellSize;
    private int mapWidth, mapHeight;

    private List<GameObject> overlaySquares = new List<GameObject>();
    private List<GameObject> overlayLabels = new List<GameObject>();

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

        cellSize = mapMgr.gridSize * mapMgr.backgroundScaleFactor;
        mapWidth = mapMgr.mapMeta.maze_width;
        mapHeight = mapMgr.mapMeta.maze_height;

        if (toggleOverlayButton != null)
            toggleOverlayButton.onClick.AddListener(ToggleOverlayMode);

        UpdateButtonLabel();
    }

    void Update()
    {
        if (mapMgr.isDirty && currentMode != OverlayMode.None)
        {
            mapMgr.RebuildMapMazeData();
            RefreshOverlay();
            mapMgr.isDirty = false;
        }
    }

    public void ToggleOverlayMode()
    {
        currentMode = (OverlayMode)(((int)currentMode + 1) % 3);
        RefreshOverlay();
        UpdateButtonLabel();
    }

    private void UpdateButtonLabel()
    {
        if (toggleOverlayButton == null) return;
        var txt = toggleOverlayButton.GetComponentInChildren<Text>();
        if (txt == null) return;

        switch (currentMode)
        {
            case OverlayMode.None:
                txt.text = "显示 Sector 覆盖";
                break;
            case OverlayMode.ShowSector:
                txt.text = "显示 Arena 覆盖";
                break;
            case OverlayMode.ShowArena:
                txt.text = "关闭覆盖";
                break;
        }
    }

    private Color GenerateColorForID(int id)
    {
        float hue = ((id * 137) % 360) / 360f;
        Color rgb = Color.HSVToRGB(hue, overlaySaturation, overlayValue);
        rgb.a = overlayAlpha;
        return rgb;
    }

    public void RefreshOverlay()
    {
        // 清理旧的
        foreach (var go in overlaySquares) Destroy(go);
        foreach (var go in overlayLabels) Destroy(go);
        overlaySquares.Clear();
        overlayLabels.Clear();

        if (currentMode == OverlayMode.None) return;

        // 确保数据最新
        mapMgr.RebuildMapMazeData();

        var sector = mapMgr.mapSectorMaze;
        var arena = mapMgr.mapArenaMaze;
        var gridData = (currentMode == OverlayMode.ShowSector) ? sector : arena;

        // 1. 绘制半透明方块
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int id = gridData[y, x];
                if (id <= 0) continue;

                var sq = new GameObject($"Overlay_{x}_{y}", typeof(Image));
                sq.transform.SetParent(mapMgr.mapContent, false);
                var img = sq.GetComponent<Image>();
                img.color = GenerateColorForID(id);

                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(x * cellSize, -y * cellSize);

                sq.transform.SetAsLastSibling();
                overlaySquares.Add(sq);
            }
        }

        // 2. 连通域检测并放置动态字号标签
        bool[,] visited = new bool[mapHeight, mapWidth];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int id = gridData[y, x];
                if (id <= 0 || visited[y, x]) continue;

                // BFS 收集连通区域
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

                    // 四向
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

                // 计算质心
                float sumX = 0, sumY = 0;
                foreach (var c in region)
                {
                    sumX += c.x;
                    sumY += c.y;
                }
                float cx = sumX / region.Count;
                float cy = sumY / region.Count;

                // 计算可用宽度（像素）
                int cols = maxX - minX + 1;
                float availW = cols * cellSize;

                // 放置 Label
                if (labelPrefab != null)
                {
                    var labGO = Instantiate(labelPrefab, mapMgr.mapContent, false);
                    var label = labGO.GetComponent<TextMeshProUGUI>();
                    label.text = mapMgr.GetBlockName(id, currentMode == OverlayMode.ShowSector);
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 0f;
                    label.fontSizeMax = labelFontSizeMax;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.alignment = TextAlignmentOptions.Center;

                    var lrt = label.rectTransform;
                    lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1);
                    lrt.pivot = new Vector2(0.5f, 0.5f);
                    // 容器宽度 = availW，高度取 cellSize
                    lrt.sizeDelta = new Vector2(availW, cellSize);
                    // 定位到区域中心
                    lrt.anchoredPosition = new Vector2(
                        cx * cellSize + cellSize / 2f,
                       -cy * cellSize - cellSize / 2f
                    );

                    overlayLabels.Add(labGO);
                }
            }
        }
    }
}
