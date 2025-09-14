using UnityEngine;

/// <summary>
/// 全局配置：提供“容器气泡”预制体与定位偏移；
/// 预制体内需包含名为 "icon" 的 Image。
/// </summary>
public class ContainerLogoConfigManager : MonoBehaviour
{
    public static ContainerLogoConfigManager Instance;

    [Header("容器气泡")]
    [Tooltip("显示在物品顶部的气泡预制体（内部包含名为 'icon' 的 Image）")]
    public GameObject containerBubblePrefab;

    [Tooltip("相对于物品顶部中点的偏移（Y > 0 向上）")]
    public Vector2 offset = new Vector2(0, 2);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 如需跨场景保留：
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
