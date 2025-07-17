using UnityEngine;

/// <summary>
/// �ù����������� Inspector ��ͳһ��������ͼ����Ӿ�������
/// ��ͨ�� ContainerLogoController �����ͳһ��ȡ��Щ������
/// </summary>
public class ContainerLogoConfigManager : MonoBehaviour
{
    public static ContainerLogoConfigManager Instance;

    [Header("����ͼ������")]
    [Tooltip("����ͼ��ʹ�õ� Sprite")]
    public Sprite containerLogoSprite;

    [Tooltip("����ͼ��Ĵ�С������ߣ���λ���أ�")]
    public Vector2 logoSize = new Vector2(20, 20);

    [Tooltip("����ͼ���������Ʒ�����м��ƫ�ƣ�Y > 0 ��ʾ��������϶")]
    public Vector2 offset = new Vector2(0, 2);

    private void Awake()
    {
        // ʹ�õ���ģʽȷ��ȫ��ͳһ����
        if (Instance == null)
        {
            Instance = this;
            // �����Ҫ���л�����ʱ�������ã���ʹ�ã�
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
