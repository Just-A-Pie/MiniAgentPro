using UnityEngine;

/// <summary>
/// 该管理器用于在 Inspector 中统一配置容器图标的视觉参数，
/// 可通过 ContainerLogoController 等组件统一获取这些参数。
/// </summary>
public class ContainerLogoConfigManager : MonoBehaviour
{
    public static ContainerLogoConfigManager Instance;

    [Header("容器图标配置")]
    [Tooltip("容器图标使用的 Sprite")]
    public Sprite containerLogoSprite;

    [Tooltip("容器图标的大小（宽×高，单位像素）")]
    public Vector2 logoSize = new Vector2(20, 20);

    [Tooltip("容器图标相对于物品顶部中间的偏移，Y > 0 表示向上留空隙")]
    public Vector2 offset = new Vector2(0, 2);

    private void Awake()
    {
        // 使用单例模式确保全局统一配置
        if (Instance == null)
        {
            Instance = this;
            // 如果需要在切换场景时保留配置，则使用：
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
