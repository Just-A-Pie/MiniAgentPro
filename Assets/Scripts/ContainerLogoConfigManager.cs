using UnityEngine;

/// <summary>
/// ȫ�����ã��ṩ���������ݡ�Ԥ�����붨λƫ�ƣ�
/// Ԥ�������������Ϊ "icon" �� Image��
/// </summary>
public class ContainerLogoConfigManager : MonoBehaviour
{
    public static ContainerLogoConfigManager Instance;

    [Header("��������")]
    [Tooltip("��ʾ����Ʒ����������Ԥ���壨�ڲ�������Ϊ 'icon' �� Image��")]
    public GameObject containerBubblePrefab;

    [Tooltip("�������Ʒ�����е��ƫ�ƣ�Y > 0 ���ϣ�")]
    public Vector2 offset = new Vector2(0, 2);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // ����糡��������
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
