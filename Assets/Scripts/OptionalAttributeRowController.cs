using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class OptionalAttributeRowController : MonoBehaviour
{
    public Toggle attributeToggle;
    public TMP_Text attributeLabel;

    // 对于非 container 属性使用的输入区域
    public GameObject inputFieldContainer;
    public TMP_InputField inputField;

    // 针对 container 属性的区域
    public GameObject containerPanel;      // 整个容器区域（例如固定宽度的 Panel 或 ScrollRect Content）
    public Transform childContainer;       // 用于存放子物品按钮的容器（放在 containerPanel 内）
    public Button addChildButton;          // 加号按钮
    public GameObject containerChildButtonPrefab; // 子物品按钮预制体

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

            // 如果 defaultValue 非空，解析逗号分隔的子物品 uniqueId，并生成子按钮
            if (!string.IsNullOrEmpty(defaultValue))
            {
                string[] childIds = defaultValue.Split(',');
                foreach (string cid in childIds)
                {
                    // 查找全局 placedItems 中对应子物品，获取贴图和名称
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
    /// 获取当前属性输入的值：
    /// 对于 container 属性，返回 childContainer 内所有子物品 uniqueId 拼接成的逗号分隔字符串；
    /// 对于其他属性，返回 inputField 文本
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
    /// 在 container 模式下，添加一个子物品按钮到 childContainer，并确保加号按钮始终位于最后
    /// 修改后统一传入子物品名称
    /// </summary>
    public void AddChild(string childId, Sprite childThumbnail, string childName)
    {
        if (childContainer == null)
            return;
        if (containerChildButtonPrefab == null)
        {
            Debug.LogError("containerChildButtonPrefab 未设置！");
            return;
        }
        GameObject childButtonObj = Instantiate(containerChildButtonPrefab, childContainer);
        childButtonObj.name = childId;  // 用子物品 uniqueId 命名
        ContainerChildButtonController ccbc = childButtonObj.GetComponent<ContainerChildButtonController>();
        if (ccbc != null)
        {
            // 调用新版 Initialize 重载，传入子物品名称
            ccbc.Initialize(childId, childThumbnail, childName);
        }
        else
        {
            Debug.LogWarning("ContainerChildButtonController 脚本未挂载在 " + childButtonObj.name);
        }
        // 保证加号按钮始终在最后
        if (addChildButton != null)
        {
            addChildButton.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 刷新 container 区域的子物品按钮，根据当前 container 属性值刷新UI
    /// </summary>
    public void RefreshChildren(string containerValue)
    {
        if (childContainer == null)
            return;
        // 删除当前所有子按钮
        foreach (Transform child in childContainer)
        {
            Destroy(child.gameObject);
        }
        // 重新添加
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
        // 确保加号按钮在最后
        if (addChildButton != null)
        {
            addChildButton.transform.SetAsLastSibling();
        }
    }
}
