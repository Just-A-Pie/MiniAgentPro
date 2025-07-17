using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartupManager : MonoBehaviour
{
    [Header("�����")]
    public TMP_InputField mapFolderInput;   // ��ͼ�ļ��е�ַ�����
    public TMP_InputField simFolderInput;     // Simulation �ļ��е�ַ�����

    [Header("������ť")]
    public Button mapEditorButton;            // ������ͼ�༭����ť
    public Button simulationButton;           // ���� Simulation ģ�鰴ť

    private void Start()
    {
        // Ϊ��ť�󶨵���¼�
        if (mapEditorButton != null)
            mapEditorButton.onClick.AddListener(OnMapEditorButtonClicked);
        if (simulationButton != null)
            simulationButton.onClick.AddListener(OnSimulationButtonClicked);
    }

    // ��ͼ�༭��������ť����¼�����
    private void OnMapEditorButtonClicked()
    {
        string mapFolder = mapFolderInput.text.Trim();
        if (string.IsNullOrEmpty(mapFolder))
        {
            Debug.LogWarning("��ͼ�ļ��е�ַΪ�գ�");
            return;
        }
        GameManager.Instance.resourcePath = mapFolder;
        Debug.Log("��ͼ�ļ��е�ַ����Ϊ��" + mapFolder);
        SceneManager.LoadScene("EditingPage");
    }

    // Simulation ������ť����¼�����ʹ���첽���ط�ʽ��
    private void OnSimulationButtonClicked()
    {
        string mapFolder = mapFolderInput.text.Trim();
        string simFolder = simFolderInput.text.Trim();

        if (string.IsNullOrEmpty(mapFolder))
        {
            Debug.LogWarning("��ͼ�ļ��е�ַΪ�գ�");
            return;
        }
        if (string.IsNullOrEmpty(simFolder))
        {
            Debug.LogWarning("Simulation �ļ��е�ַΪ�գ�");
            return;
        }
        GameManager.Instance.resourcePath = mapFolder;
        GameManager.Instance.simPath = simFolder;
        Debug.Log("��ͼ�ļ��е�ַ����Ϊ��" + mapFolder + "��Simulation �ļ��е�ַ����Ϊ��" + simFolder);

        // �첽���� SimulationPage ����
        StartCoroutine(LoadSimulationSceneAsync("SimulationPage"));
    }

    private IEnumerator LoadSimulationSceneAsync(string sceneName)
    {
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        // �ӳټ����³���
        asyncOp.allowSceneActivation = false;
        Debug.Log("��ʼ�첽���س��� " + sceneName);

        // �ȴ����ؽ��ȴﵽ 0.9��Unity �����³���ʱ���ֵΪ0.9��
        while (asyncOp.progress < 0.9f)
        {
            Debug.Log("�������ؽ���: " + asyncOp.progress);
            yield return null;
        }
        Debug.Log("�������ؽ��ȴ� 0.9���ȴ� SimulationPage �ڲ����ݼ������...");

        // �˴��ɸ��� SimulationPage �ڲ����ݼ�������������ӳ�ʱ��
        // ʾ���м򵥵ȴ� 1 ��
        yield return new WaitForSeconds(1f);

        // �������³���
        asyncOp.allowSceneActivation = true;
        yield return null;
    }
}
