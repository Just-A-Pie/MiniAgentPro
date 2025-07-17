using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class OptionalAttributeRowController : MonoBehaviour
{
    public Toggle attributeToggle;
    public TMP_Text attributeLabel;

    // ���ڷ� container ����ʹ�õ���������
    public GameObject inputFieldContainer;
    public TMP_InputField inputField;

    // ��� container ���Ե�����
    public GameObject containerPanel;      // ����������������̶���ȵ� Panel �� ScrollRect Content��
    public Transform childContainer;       // ���ڴ������Ʒ��ť������������ containerPanel �ڣ�
    public Button addChildButton;          // �ӺŰ�ť
    public GameObject containerChildButtonPrefab; // ����Ʒ��ťԤ����

    private string attributeName;

    public void Initialize(string attrName, bool defaultEnabled, string defaultValue)
    {
        attributeName = attrName;
        if (attributeLabel != null)
            attributeLabel.text = attrName;

        if (attributeToggle != null)
        {
            attributeToggle.isOn = defaultEnabled;
            attributeToggle.onValueChanged.AddListener(OnToggleChanged);
        }

        if (attributeName == "container")
        {
            if (inputFieldContainer != null)
                inputFieldContainer.SetActive(false);
            if (containerPanel != null)
                containerPanel.SetActive(defaultEnabled);

            // ��� defaultValue �ǿգ��������ŷָ�������Ʒ uniqueId���������Ӱ�ť
            if (!string.IsNullOrEmpty(defaultValue))
            {
                string[] childIds = defaultValue.Split(',');
                foreach (string cid in childIds)
                {
                    // ����ȫ�� placedItems �ж�Ӧ����Ʒ����ȡ��ͼ������
                    var child = MapManager.Instance.placedItems.Find(x => x.uniqueId == cid);
                    Sprite thumb = (child.item != null) ? child.item.thumbnail : null;
                    string childName = (child.item != null) ? child.item.itemName : "";
                    if (childContainer != null)
                        AddChild(cid, thumb, childName);
                }
            }
        }
        else
        {
            if (inputFieldContainer != null)
                inputFieldContainer.SetActive(defaultEnabled);
            if (containerPanel != null)
                containerPanel.SetActive(false);
            if (inputField != null)
                inputField.text = defaultValue;
        }
    }

    private void OnToggleChanged(bool isOn)
    {
        if (attributeName == "container")
        {
            if (containerPanel != null)
            {
                containerPanel.SetActive(isOn);
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
            }
        }
        else
        {
            if (inputFieldContainer != null)
            {
                inputFieldContainer.SetActive(isOn);
                LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
            }
        }
    }

    /// <summary>
    /// ��ȡ��ǰ���������ֵ��
    /// ���� container ���ԣ����� childContainer ����������Ʒ uniqueId ƴ�ӳɵĶ��ŷָ��ַ�����
    /// �����������ԣ����� inputField �ı�
    /// </summary>
    public string GetInputValue()
    {
        if (attributeName == "container")
        {
            string result = "";
            if (childContainer != null)
            {
                foreach (Transform child in childContainer)
                {
                    ContainerChildButtonController ccbc = child.GetComponent<ContainerChildButtonController>();
                    if (ccbc != null)
                    {
                        if (!string.IsNullOrEmpty(result))
                            result += ",";
                        result += ccbc.childId;
                    }
                }
            }
            return result;
        }
        else
        {
            if (attributeToggle != null && attributeToggle.isOn && inputField != null)
                return inputField.text.Trim();
            return "";
        }
    }

    public bool IsEnabled()
    {
        return attributeToggle != null && attributeToggle.isOn;
    }

    /// <summary>
    /// �� container ģʽ�£����һ������Ʒ��ť�� childContainer����ȷ���ӺŰ�ťʼ��λ�����
    /// �޸ĺ�ͳһ��������Ʒ����
    /// </summary>
    public void AddChild(string childId, Sprite childThumbnail, string childName)
    {
        if (childContainer == null)
            return;
        if (containerChildButtonPrefab == null)
        {
            Debug.LogError("containerChildButtonPrefab δ���ã�");
            return;
        }
        GameObject childButtonObj = Instantiate(containerChildButtonPrefab, childContainer);
        childButtonObj.name = childId;  // ������Ʒ uniqueId ����
        ContainerChildButtonController ccbc = childButtonObj.GetComponent<ContainerChildButtonController>();
        if (ccbc != null)
        {
            // �����°� Initialize ���أ���������Ʒ����
            ccbc.Initialize(childId, childThumbnail, childName);
        }
        else
        {
            Debug.LogWarning("ContainerChildButtonController �ű�δ������ " + childButtonObj.name);
        }
        // ��֤�ӺŰ�ťʼ�������
        if (addChildButton != null)
        {
            addChildButton.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// ˢ�� container ���������Ʒ��ť�����ݵ�ǰ container ����ֵˢ��UI
    /// </summary>
    public void RefreshChildren(string containerValue)
    {
        if (childContainer == null)
            return;
        // ɾ����ǰ�����Ӱ�ť
        foreach (Transform child in childContainer)
        {
            Destroy(child.gameObject);
        }
        // �������
        if (!string.IsNullOrEmpty(containerValue))
        {
            string[] childIds = containerValue.Split(',');
            foreach (string cid in childIds)
            {
                var child = MapManager.Instance.placedItems.Find(x => x.uniqueId == cid);
                if (child.item != null)
                {
                    Sprite thumb = child.item.thumbnail;
                    string childName = child.item.itemName;
                    AddChild(cid, thumb, childName);
                }
            }
        }
        // ȷ���ӺŰ�ť�����
        if (addChildButton != null)
        {
            addChildButton.transform.SetAsLastSibling();
        }
    }
}
