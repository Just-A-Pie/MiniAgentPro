using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ToolItemButton : MonoBehaviour
{
    public EditorItem item;

    [Header("UI ����")]
    public Image iconImage;        // ͼ����ʾ����
    public TMP_Text nameText;      // ��Ʒ�����ı�
    public TMP_Text categoryText;  // ��Ʒ����ı�

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
            // ���ѡ����ͬһ����ť��ȡ��ѡ��״̬
            if (EditorManager.Instance.currentSelectedItem == item)
            {
                EditorManager.Instance.SetSelectedItem(null);  // ȡ��ѡ��
            }
            else
            {
                EditorManager.Instance.SetSelectedItem(item);  // ѡ�����Ʒ
            }
        }
    }
}
