// 文件: SimulationMapManager.cs
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class SimulationMapManager : MonoBehaviour
{
    public static SimulationMapManager Instance;

    /*──────────── Inspector 字段 ────────────*/
    [Header("地图显示区域 (外层容器)")]
    public RectTransform mapDisplayArea;     // 比如 Canvas 里一个 Panel

    [Header("地图内容容器 (背景 + 物品 + Agent)")]
    public RectTransform mapContent;

    [Header("地图贴图文件名")]
    public string mapTextureFileName = "texture.png";

    [Header("地图背景 Image (可留空)")]
    public Image mapImage;

    [Header("资源根目录 (留空 = GameManager.resourcePath)")]
    public string mapFolder;

    [HideInInspector]
    public float backgroundScaleFactor = 1f;

    /*──────────── 生命周期 ────────────*/
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        Debug.Log("[SIMBOOT:S2] SimMapManager Start()");
        /* 根目录 */
        if (string.IsNullOrEmpty(mapFolder))
        {
            if (GameManager.Instance != null)
                mapFolder = GameManager.Instance.resourcePath;
            else
                Debug.LogError("[SIMBOOT:S2][ERR] 未设置 mapFolder 且未找到 GameManager！");
        }
        Debug.Log("[SIMBOOT:S2] mapFolder=" + mapFolder);

        /* ① 统一父容器坐标系为左上 */
        SetTopLeft(mapDisplayArea);   // 锚 & Pivot
        SetTopLeft(mapContent);       // 锚 & Pivot

        /* ② 加载背景贴图 */
        LoadMapBackground();
    }


    public void LoadMapBackground()
    {
        string actualMapFolder = Path.Combine(mapFolder, "map");
        string texturePath = Path.Combine(actualMapFolder, mapTextureFileName);
        Debug.Log("[SIMBOOT:S2] Try load background: " + texturePath);

        if (!File.Exists(texturePath))
        {
            Debug.LogWarning("[SIMBOOT:S2][ERR] 未找到地图贴图 " + texturePath);
            return;
        }

        /* 读图片 */
        Texture2D tex = new Texture2D(2, 2);
        if (!tex.LoadImage(File.ReadAllBytes(texturePath)))
        {
            Debug.LogError("[SIMBOOT:S2][ERR] 贴图加载失败");
            return;
        }
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                        new Vector2(0.5f, 0.5f));

        /* 动态创建 Image（如有需要） */
        if (mapImage == null)
        {
            GameObject go = new GameObject("MapImage", typeof(Image));
            go.transform.SetParent(mapContent, false);
            mapImage = go.GetComponent<Image>();
            Debug.Log("[SIMBOOT:S2] Created MapImage object");
        }
        mapImage.sprite = sp;

        /* ③ 背景自身也设为左上坐标系 */
        RectTransform imgRT = mapImage.rectTransform;
        SetTopLeft(imgRT, true);
        imgRT.anchoredPosition = Vector2.zero;

        /* ④ 计算缩放因子 */
        float panelW = mapDisplayArea.rect.width;
        float panelH = mapDisplayArea.rect.height;
        backgroundScaleFactor = Mathf.Min(panelW / tex.width,
                                          panelH / tex.height,
                                          1f);

        imgRT.sizeDelta = new Vector2(tex.width * backgroundScaleFactor,
                                      tex.height * backgroundScaleFactor);

        /* 背景放最底层 */
        mapImage.transform.SetSiblingIndex(0);

        Debug.Log($"[SIMBOOT:S2] Background created. src={tex.width}x{tex.height} scaled={imgRT.sizeDelta} factor={backgroundScaleFactor}");
    }


    /*──────────── 工具函数 ────────────*/
    /// <summary>把 RectTransform 的 Anchor 与 Pivot 都设为左上 (0,1)</summary>
    private void SetTopLeft(RectTransform rt, bool setAnchoredPosZero = false)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        if (setAnchoredPosZero) rt.anchoredPosition = Vector2.zero;
    }
}
