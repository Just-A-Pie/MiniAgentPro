using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ContainerChildButtonController : MonoBehaviour
{
    public Image childImage;         // 用于显示子物品贴图
    public TMP_Text childNameText;   // 新增：用于显示子物品名称
    public string childId;           // 存储子物品的 uniqueId

    /// <summary>
    /// 初始化子物品按钮，同时设置贴图和显示名称
    /// </summary>
    /// <param name="id">子物品 uniqueId</param>
    /// <param name="thumbnail">子物品贴图</param>
    /// <param name="childName">子物品名称</param>
    public void Initialize(string id, Sprite thumbnail, string childName)
    {
        childId = id;
        if (childImage != null && thumbnail != null)
        {
            childImage.sprite = thumbnail;
            // 等比缩放，确保显示大小不超过50像素
            float origW = thumbnail.rect.width;
            float origH = thumbnail.rect.height;
            float scaleFactor = Mathf.Min(50f / origW, 50f / origH, 1f);
            childImage.rectTransform.sizeDelta = new Vector2(origW * scaleFactor, origH * scaleFactor);
        }
        if (childNameText != null)
        {
            childNameText.text = childName;
        }
        // 添加按钮点击事件
        Button btn = GetComponent<Button>();
        if (btn == null)
            btn = gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => {
            PopupManager popupMgr = FindObjectOfType<PopupManager>();
            if (popupMgr != null)
            {
                popupMgr.OpenChildPopup(childId);
            }
            else
            {
                Debug.LogError("PopupManager 未找到！");
            }
        });
    }
}
