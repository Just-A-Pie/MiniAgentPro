// �ļ�: StartupManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartupManager : MonoBehaviour
{
    // ���������������������������������� �����������ã����������� Inspector ������ ����������������������������������
    [Header("�����������ã�������")]
    [Tooltip("����ʱǿ���ô���ģʽ�������ޱ߿�ȫ����")]
    public bool forceWindowedMode = true;

    [Tooltip("������ʾ���ֱ��ʵı������ô��ڴ�С������ 0.8 ��ʾ 80%")]
    [Range(0.3f, 1.0f)]
    public float screenFraction = 0.8f;

    [Tooltip("������С��ȣ����أ�")]
    public int minWidth = 960;

    [Tooltip("������С�߶ȣ����أ�")]
    public int minHeight = 540;

    [Tooltip("����ʱ�Ƿ�ǿ�����ô��ڴ�С������ Player Settings Ĭ�Ϸֱ��ʣ�")]
    public bool forceWindowSizeOnStart = true;

    // ���������������������������������� ���ԭ���ֶ� ����������������������������������
    [Header("�����")]
    public TMP_InputField mapFolderInput;   // ��ͼ�ļ��е�ַ�����
    public TMP_InputField simFolderInput;   // Simulation �ļ��е�ַ�����

    [Header("������ť")]
    public Button mapEditorButton;          // ������ͼ�༭����ť
    public Button simulationButton;         // ���� Simulation ģ�鰴ť

    // ���������������������������������� ������Awake �����ô���ģʽ/�ߴ� ����������������������������������
    private void Awake()
    {
        // 1) ǿ�ƴ���ģʽ�������ޱ߿�ȫ�����ڡ���
        if (forceWindowedMode)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }

        // 2) ����ʾ���ֱ��ʱ�������һ����С���ڣ�����һ������������Ļ
        if (forceWindowSizeOnStart)
        {
            // ����ʾ���ֱ��ʣ�������������˵���ǰ�ֱ���
            int sw = (Display.main != null) ? Display.main.systemWidth : Screen.currentResolution.width;
            int sh = (Display.main != null) ? Display.main.systemHeight : Screen.currentResolution.height;

            int targetW = Mathf.Max(minWidth, Mathf.RoundToInt(sw * screenFraction));
            int targetH = Mathf.Max(minHeight, Mathf.RoundToInt(sh * screenFraction));

#if UNITY_2019_1_OR_NEWER
            // �°� API��ֱ��ָ������ģʽ
            Screen.SetResolution(targetW, targetH, FullScreenMode.Windowed);
#else
            // ������ API��false = ��ȫ�������ڣ�
            Screen.SetResolution(targetW, targetH, false);
#endif
        }
    }

    // ���������������������������������� ��������ԭ�е� Start/�߼���δ�Ķ��� ����������������������������������
    private void Start()
    {
        // �Զ���䣨������ǰΪ��ʱ��
        if (mapFolderInput != null && string.IsNullOrWhiteSpace(mapFolderInput.text))
            mapFolderInput.text = "root:/sampleMap";
        if (simFolderInput != null && string.IsNullOrWhiteSpace(simFolderInput.text))
            simFolderInput.text = "root:/sampleSim";

        // Ϊ��ť�󶨵���¼�
        if (mapEditorButton != null)
            mapEditorButton.onClick.AddListener(OnMapEditorButtonClicked);
        if (simulationButton != null)
            simulationButton.onClick.AddListener(OnSimulationButtonClicked);
    }

    // ��ͼ�༭��������ť����¼�����
    private void OnMapEditorButtonClicked()
    {
        if (mapFolderInput == null)
        {
            Debug.LogWarning("��ͼ�ļ��������δ�󶨣�");
            return;
        }

        string mapFolderRaw = mapFolderInput.text.Trim();
        if (string.IsNullOrEmpty(mapFolderRaw))
        {
            Debug.LogWarning("��ͼ�ļ��е�ַΪ�գ�");
            return;
        }

        // ���� root:/ ǰ׺�����·��
        string mapFolderResolved = RootPath.Resolve(mapFolderRaw);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.resourcePath = mapFolderResolved;
            Debug.Log($"��ͼ�ļ��е�ַ����Ϊ��ԭʼ='{mapFolderRaw}' �� ����='{mapFolderResolved}'��");
        }
        else
        {
            Debug.LogError("δ�ҵ� GameManager ʵ����");
            return;
        }

        SceneManager.LoadScene("EditingPage");
    }

    // Simulation ������ť����¼�����ʹ���첽���ط�ʽ��
    private void OnSimulationButtonClicked()
    {
        if (mapFolderInput == null || simFolderInput == null)
        {
            Debug.LogWarning("��ͼ/���� �����δ�󶨣�");
            return;
        }

        string mapFolderRaw = mapFolderInput.text.Trim();
        string simFolderRaw = simFolderInput.text.Trim();

        if (string.IsNullOrEmpty(mapFolderRaw))
        {
            Debug.LogWarning("��ͼ�ļ��е�ַΪ�գ�");
            return;
        }
        if (string.IsNullOrEmpty(simFolderRaw))
        {
            Debug.LogWarning("Simulation �ļ��е�ַΪ�գ�");
            return;
        }

        // ���� root:/ ǰ׺�����·��
        string mapFolderResolved = RootPath.Resolve(mapFolderRaw);
        string simFolderResolved = RootPath.Resolve(simFolderRaw);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.resourcePath = mapFolderResolved;
            GameManager.Instance.simPath = simFolderResolved;
            Debug.Log($"��ͼ�ļ������ã�ԭ='{mapFolderRaw}' �� ����='{mapFolderResolved}'����" +
                      $"Simulation �ļ������ã�ԭ='{simFolderRaw}' �� ����='{simFolderResolved}'��");
        }
        else
        {
            Debug.LogError("δ�ҵ� GameManager ʵ����");
            return;
        }

        // �첽���� SimulationPage ����
        StartCoroutine(LoadSimulationSceneAsync("SimulationPage"));
    }

    private IEnumerator LoadSimulationSceneAsync(string sceneName)
    {
        Debug.Log("[SIMBOOT:S0] Begin LoadSimulationSceneAsync -> " + sceneName);
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
        {
            yield return null;
        }
        Debug.Log("[SIMBOOT:S0] Reached 0.9, waiting 1s before activation");
        yield return new WaitForSeconds(1f);

        asyncOp.allowSceneActivation = true;
        Debug.Log("[SIMBOOT:S0] allowSceneActivation = true");
    }

}
