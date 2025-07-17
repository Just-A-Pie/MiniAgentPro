using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ToolItemButton : MonoBehaviour
{
    public EditorItem item;

    [Header("UI 引用")]
    public Image iconImage;        // 图标显示区域
    public TMP_Text nameText;      // 物品名称文本
    public TMP_Text categoryText;  // 物品类别文本

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnButtonClicked);
    }

    public void Setup(EditorItem newItem)
    {
        item = newItem;
        if (iconImage != null && item.thumbnail != null)
        {
            iconImage.sprite = item.thumbnail;
            float originalWidth = item.thumbnail.rect.width;
            float originalHeight = item.thumbnail.rect.height;
            float scaleX = 70f / originalWidth;
            float scaleY = 60f / originalHeight;
            float scaleFactor = Mathf.Min(scaleX, scaleY, 1f);
            iconImage.rectTransform.sizeDelta = new Vector2(originalWidth * scaleFactor, originalHeight * scaleFactor);
        }
        if (nameText != null)
            nameText.text = item.itemName;
        if (categoryText != null)
        {
            categoryText.text = item.category.ToString();
            if (item.category == EditorItemCategory.Object)
            {
                Color colorObj;
                if (ColorUtility.TryParseHtmlString("#000000", out colorObj))
                    categoryText.color = colorObj;
            }
            else if (item.category == EditorItemCategory.Building)
            {
                Color colorBld;
                if (ColorUtility.TryParseHtmlString("#000000", out colorBld))
                    categoryText.color = colorBld;
            }
        }
    }

    public void OnButtonClicked()
    {
        if (EditorManager.Instance != null)
        {
            // 如果选择了同一个按钮，取消选中状态
            if (EditorManager.Instance.currentSelectedItem == item)
            {
                EditorManager.Instance.SetSelectedItem(null);  // 取消选择
            }
            else
            {
                EditorManager.Instance.SetSelectedItem(item);  // 选择该物品
            }
        }
    }
}
