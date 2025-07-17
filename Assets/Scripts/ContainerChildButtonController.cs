using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ContainerChildButtonController : MonoBehaviour
{
    public Image childImage;         // ������ʾ����Ʒ��ͼ
    public TMP_Text childNameText;   // ������������ʾ����Ʒ����
    public string childId;           // �洢����Ʒ�� uniqueId

    /// <summary>
    /// ��ʼ������Ʒ��ť��ͬʱ������ͼ����ʾ����
    /// </summary>
    /// <param name="id">����Ʒ uniqueId</param>
    /// <param name="thumbnail">����Ʒ��ͼ</param>
    /// <param name="childName">����Ʒ����</param>
    public void Initialize(string id, Sprite thumbnail, string childName)
    {
        childId = id;
        if (childImage != null && thumbnail != null)
        {
            childImage.sprite = thumbnail;
            // �ȱ����ţ�ȷ����ʾ��С������50����
            float origW = thumbnail.rect.width;
            float origH = thumbnail.rect.height;
            float scaleFactor = Mathf.Min(50f / origW, 50f / origH, 1f);
            childImage.rectTransform.sizeDelta = new Vector2(origW * scaleFactor, origH * scaleFactor);
        }
        if (childNameText != null)
        {
            childNameText.text = childName;
        }
        // ��Ӱ�ť����¼�
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
                Debug.LogError("PopupManager δ�ҵ���");
            }
        });
    }
}
