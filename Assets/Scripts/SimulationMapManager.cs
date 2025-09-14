// �ļ�: SimulationMapManager.cs
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class SimulationMapManager : MonoBehaviour
{
    public static SimulationMapManager Instance;

    /*������������������������ Inspector �ֶ� ������������������������*/
    [Header("��ͼ��ʾ���� (�������)")]
    public RectTransform mapDisplayArea;     // ���� Canvas ��һ�� Panel

    [Header("��ͼ�������� (���� + ��Ʒ + Agent)")]
    public RectTransform mapContent;

    [Header("��ͼ��ͼ�ļ���")]
    public string mapTextureFileName = "texture.png";

    [Header("��ͼ���� Image (������)")]
    public Image mapImage;

    [Header("��Դ��Ŀ¼ (���� = GameManager.resourcePath)")]
    public string mapFolder;

    [HideInInspector]
    public float backgroundScaleFactor = 1f;

    /*������������������������ �������� ������������������������*/
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        Debug.Log("[SIMBOOT:S2] SimMapManager Start()");
        /* ��Ŀ¼ */
        if (string.IsNullOrEmpty(mapFolder))
        {
            if (GameManager.Instance != null)
                mapFolder = GameManager.Instance.resourcePath;
            else
                Debug.LogError("[SIMBOOT:S2][ERR] δ���� mapFolder ��δ�ҵ� GameManager��");
        }
        Debug.Log("[SIMBOOT:S2] mapFolder=" + mapFolder);

        /* �� ͳһ����������ϵΪ���� */
        SetTopLeft(mapDisplayArea);   // ê & Pivot
        SetTopLeft(mapContent);       // ê & Pivot

        /* �� ���ر�����ͼ */
        LoadMapBackground();
    }


    public void LoadMapBackground()
    {
        string actualMapFolder = Path.Combine(mapFolder, "map");
        string texturePath = Path.Combine(actualMapFolder, mapTextureFileName);
        Debug.Log("[SIMBOOT:S2] Try load background: " + texturePath);

        if (!File.Exists(texturePath))
        {
            Debug.LogWarning("[SIMBOOT:S2][ERR] δ�ҵ���ͼ��ͼ " + texturePath);
            return;
        }

        /* ��ͼƬ */
        Texture2D tex = new Texture2D(2, 2);
        if (!tex.LoadImage(File.ReadAllBytes(texturePath)))
        {
            Debug.LogError("[SIMBOOT:S2][ERR] ��ͼ����ʧ��");
            return;
        }
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                        new Vector2(0.5f, 0.5f));

        /* ��̬���� Image��������Ҫ�� */
        if (mapImage == null)
        {
            GameObject go = new GameObject("MapImage", typeof(Image));
            go.transform.SetParent(mapContent, false);
            mapImage = go.GetComponent<Image>();
            Debug.Log("[SIMBOOT:S2] Created MapImage object");
        }
        mapImage.sprite = sp;

        /* �� ��������Ҳ��Ϊ��������ϵ */
        RectTransform imgRT = mapImage.rectTransform;
        SetTopLeft(imgRT, true);
        imgRT.anchoredPosition = Vector2.zero;

        /* �� ������������ */
        float panelW = mapDisplayArea.rect.width;
        float panelH = mapDisplayArea.rect.height;
        backgroundScaleFactor = Mathf.Min(panelW / tex.width,
                                          panelH / tex.height,
                                          1f);

        imgRT.sizeDelta = new Vector2(tex.width * backgroundScaleFactor,
                                      tex.height * backgroundScaleFactor);

        /* ��������ײ� */
        mapImage.transform.SetSiblingIndex(0);

        Debug.Log($"[SIMBOOT:S2] Background created. src={tex.width}x{tex.height} scaled={imgRT.sizeDelta} factor={backgroundScaleFactor}");
    }


    /*������������������������ ���ߺ��� ������������������������*/
    /// <summary>�� RectTransform �� Anchor �� Pivot ����Ϊ���� (0,1)</summary>
    private void SetTopLeft(RectTransform rt, bool setAnchoredPosZero = false)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        if (setAnchoredPosZero) rt.anchoredPosition = Vector2.zero;
    }
}
