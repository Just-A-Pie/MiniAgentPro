// 文件: PopupManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using static MapManager;

public class PopupManager : MonoBehaviour
{
    [Header("运行模式")]
    [Tooltip("SimulationPage 勾选，只查看信息，不允许编辑/删除/移动")]
    public bool readOnlyMode = false;

    [Header("弹窗预制体")]
    public GameObject objectBuildingPopupPrefab; // 父弹窗预制体

    [Header("顶层弹窗父容器")]
    [Tooltip("请指向场景中专门用于显示对话框的顶层 Canvas 下的容器，例如名为 DialogCanvas 的对象")]
    public Transform popupParent;

    [Header("可选属性行预制体")]
    public GameObject optionalAttributeRowPrefab; // 用于生成属性行（包括 container 行）

    // 预定义可选属性列表，此处 container 行负责子物品显示
    private List<string> optionalAttributes = new List<string>()
    {
        "quantity",
        "container"
    };

    // 弹窗层级（UI）栈
    private Stack<GameObject> popupStack = new Stack<GameObject>();
    // 与 UI 层级一一对应的“当前物品上下文”栈
    private Stack<PlacedItem> contextStack = new Stack<PlacedItem>();

    private GameObject currentPopup;
    private PlacedItem? currentPlacedItem;

    private string originalName;
    private Dictionary<string, string> originalAttributes;
    private TMP_InputField nameInputField;
    private Button applyButton;
    private bool isApplied = false;

    // 保存各个属性行对应的 OptionalAttributeRowController（container 行就在其中）
    private Dictionary<string, OptionalAttributeRowController> attributeRows = new Dictionary<string, OptionalAttributeRowController>();

    // 子物品显示容器：假设预制体结构为：objectBuildingPopupPrefab -> AttributesPanel -> container_Row -> ChildContainerPanel
    private Transform childListContainer
    {
        get
        {
            if (currentPopup == null)
            {
                Debug.LogWarning("childListContainer: currentPopup 为 null");
                return null;
            }
            Transform t = currentPopup.transform.Find("AttributesPanel/container_Row/ChildContainerPanel");
            if (t == null)
                Debug.LogWarning("childListContainer 未找到，请检查预制体中 'AttributesPanel/container_Row/ChildContainerPanel' 的设置");
            return t;
        }
    }

    /// <summary>
    /// 显示物品编辑弹窗（顶层）。清空所有栈。
    /// </summary>
    public void ShowPopup(PlacedItem placedItem)
    {
        // 确保 popupParent 已经赋值
        if (popupParent == null)
        {
            GameObject dialogGO = GameObject.Find("DialogCanvas");
            if (dialogGO != null)
            {
                popupParent = dialogGO.transform;
            }
            else
            {
                Debug.LogError("PopupManager: 未设置 popupParent 且未找到名为 'DialogCanvas' 的对象");
                return;
            }
        }

        if (EditorManager.Instance.currentSelectedItem != null)
            return;

        Debug.Log($"ShowPopup: 显示物品弹窗，ID: {placedItem.uniqueId}");

        // 清空所有层级
        popupStack.Clear();
        contextStack.Clear();

        if (currentPopup != null)
            Destroy(currentPopup);

        // 在顶层弹窗父容器下实例化对话框
        currentPopup = Instantiate(objectBuildingPopupPrefab, popupParent);
        // 确保当前弹窗位于父容器的最后（最上层）
        currentPopup.transform.SetAsLastSibling();

        // 如果弹窗上挂有 Canvas，则设置排序，使其始终在顶层显示
        if (currentPopup.TryGetComponent<Canvas>(out var cv))
        {
            cv.overrideSorting = true;
            cv.sortingOrder = 1000;
        }

        SetCloseButtonText(currentPopup, "×");

        currentPlacedItem = placedItem;
        originalName = placedItem.item.itemName;
        originalAttributes = placedItem.item.attributes != null
            ? new Dictionary<string, string>(placedItem.item.attributes)
            : new Dictionary<string, string>();
        isApplied = false;

        // 设置基本面板
        var closeButton = currentPopup.transform.Find("CloseButton")?.GetComponent<Button>();
        if (closeButton != null)
            closeButton.onClick.AddListener(() => { ClosePopup(); });

        var basicPanel = currentPopup.transform.Find("Basic");
        if (basicPanel != null)
        {
            var typeText = basicPanel.Find("TypeText")?.GetComponent<TMP_Text>();
            if (typeText != null)
                typeText.text = placedItem.category.ToString();
            nameInputField = basicPanel.Find("NameInputField")?.GetComponent<TMP_InputField>();
            if (nameInputField != null)
            {
                nameInputField.text = originalName;
                nameInputField.onValueChanged.AddListener(OnNameChanged);
            }
            var image = basicPanel.Find("Image")?.GetComponent<Image>();
            if (image != null && placedItem.item.thumbnail != null)
            {
                image.sprite = placedItem.item.thumbnail;
                float origW = placedItem.item.thumbnail.rect.width;
                float origH = placedItem.item.thumbnail.rect.height;
                float scaleFactor = Mathf.Min(100f / origW, 100f / origH, 1f);
                image.rectTransform.sizeDelta = new Vector2(origW * scaleFactor, origH * scaleFactor);
            }
        }
        else
        {
            Debug.LogWarning("ShowPopup: 未找到 Basic 面板");
        }

        // 设置 Apply、Delete 和 Move 按钮
        applyButton = currentPopup.transform.Find("ApplyButton")?.GetComponent<Button>();
        if (applyButton != null)
            applyButton.onClick.AddListener(ApplyChanges);
        var deleteButton = currentPopup.transform.Find("DeleteButton")?.GetComponent<Button>();
        if (deleteButton != null)
            deleteButton.onClick.AddListener(() => { DeleteItem(placedItem); });
        var moveButton = currentPopup.transform.Find("MoveButton")?.GetComponent<Button>();
        if (moveButton != null)
        {
            moveButton.onClick.RemoveAllListeners();
            moveButton.onClick.AddListener(OnMoveButtonClicked);
        }

        // 处理属性行（包括 container 行），重建全部属性行
        BuildAttributeRowsFor(currentPlacedItem.Value);

        DisableMapOperations();
        Debug.Log("ShowPopup: 弹窗显示完成");

        /*=================== 只读模式处理 ===================*/
        if (readOnlyMode)
            ApplyReadOnlyMask();
    }

    private void BuildAttributeRowsFor(PlacedItem item)
    {
        var attributesPanel = currentPopup.transform.Find("AttributesPanel");
        if (attributesPanel != null)
        {
            foreach (Transform child in attributesPanel)
                Destroy(child.gameObject);
            attributeRows.Clear();
            foreach (string attrName in optionalAttributes)
            {
                bool enabled = false;
                string defaultVal = "";
                if (item.item.attributes != null && item.item.attributes.ContainsKey(attrName))
                {
                    enabled = true;
                    defaultVal = item.item.attributes[attrName];
                }
                CreateOptionalAttributeRow(attrName, attributesPanel, enabled, defaultVal);
            }
        }
        else
        {
            Debug.LogWarning("BuildAttributeRowsFor: 未找到 AttributesPanel");
        }
    }

    /// <summary>
    /// 设置弹窗关闭按钮的文本
    /// </summary>
    private void SetCloseButtonText(GameObject popup, string text)
    {
        var closeButton = popup.transform.Find("CloseButton")?.GetComponent<Button>();
        if (closeButton != null)
        {
            TMP_Text txt = closeButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = text;
        }
    }

    /// <summary>
    /// 创建可选属性行，包含设置及事件绑定
    /// </summary>
    private void CreateOptionalAttributeRow(string attrName, Transform parent, bool defaultEnabled, string defaultValue)
    {
        if (!optionalAttributeRowPrefab)
        {
            Debug.LogError("PopupManager: optionalAttributeRowPrefab 未设置！");
            return;
        }
        GameObject rowObj = Instantiate(optionalAttributeRowPrefab, parent);
        rowObj.name = attrName + "_Row";
        OptionalAttributeRowController controller = rowObj.GetComponent<OptionalAttributeRowController>();
        if (controller != null)
        {
            controller.Initialize(attrName, defaultEnabled, defaultValue);
            attributeRows[attrName] = controller;
            if (attrName == "container" && controller.addChildButton != null)
            {
                controller.addChildButton.onClick.RemoveAllListeners();
                controller.addChildButton.onClick.AddListener(AddChildToContainer);
            }
            else if (controller.inputField != null)
            {
                controller.inputField.onValueChanged.AddListener((value) => { /* 可扩展处理 */ });
            }
        }
        else
        {
            Debug.LogError("CreateOptionalAttributeRow: OptionalAttributeRowController 脚本未挂载在 " + rowObj.name);
        }
    }

    private void OnNameChanged(string newText)
    {
        Debug.Log($"OnNameChanged: 新名称为 {newText}");
    }

    /// <summary>
    /// 应用修改，更新物品名称及属性，并同步到全局数据
    /// </summary>
    private void ApplyChanges()
    {
        if (!currentPlacedItem.HasValue || nameInputField == null)
            return;
        var updatedItem = currentPlacedItem.Value;
        string newName = nameInputField.text.Trim();
        updatedItem.item.itemName = newName;
        currentPlacedItem = updatedItem;
        originalName = newName;
        Dictionary<string, string> newAttributes = new Dictionary<string, string>();
        foreach (var pair in attributeRows)
        {
            string val = pair.Value.GetInputValue();
            if (!string.IsNullOrEmpty(val))
                newAttributes[pair.Key] = val;
        }
        updatedItem.item.attributes = newAttributes;
        // 更新全局列表中对应物品的数据
        List<PlacedItem> list = MapManager.Instance.placedItems;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].uniqueId == updatedItem.uniqueId)
            {
                list[i] = updatedItem;
                break;
            }
        }
        isApplied = true;
        MapManager.Instance.isDirty = true;
        Debug.Log($"ApplyChanges: 物品 [{updatedItem.uniqueId}] 名称改为: {newName}, 更新 {newAttributes.Count} 项属性");

        // 同步更新该物品上的容器图标显示状态
        Transform itemTransform = MapManager.Instance.mapContent.Find(updatedItem.uniqueId);
        if (itemTransform != null)
        {
            ContainerLogoController logoCtrl = itemTransform.GetComponent<ContainerLogoController>();
            if (logoCtrl != null)
            {
                bool hasContainer = updatedItem.item.attributes != null &&
                                    updatedItem.item.attributes.ContainsKey("container") &&
                                    !string.IsNullOrEmpty(updatedItem.item.attributes["container"]);
                logoCtrl.UpdateLogoVisibility(hasContainer);
            }
        }
    }

    /// <summary>
    /// 删除物品（包括递归删除 container 中的子物品，并更新母物品的 container 属性）
    /// </summary>
    private void DeleteItem(PlacedItem placedItem)
    {
        Debug.Log($"DeleteItem: 正在删除物品 {placedItem.uniqueId} ({placedItem.item.itemName})");
        // 递归删除该物品 container 中的子物品
        if (placedItem.item.attributes != null && placedItem.item.attributes.ContainsKey("container"))
        {
            string containerStr = placedItem.item.attributes["container"];
            if (!string.IsNullOrEmpty(containerStr))
            {
                string[] childIds = containerStr.Split(',');
                foreach (string childId in childIds)
                {
                    PlacedItem? child = MapManager.Instance.placedItems.Find(x => x.uniqueId == childId);
                    if (child.HasValue)
                    {
                        Debug.Log($"DeleteItem: 递归删除子物品 {child.Value.uniqueId} from {placedItem.uniqueId}");
                        DeleteItem(child.Value);
                    }
                }
            }
        }
        // 如果删除的是子物品，则更新其母物品的 container 属性
        if (placedItem.gridX == -1 && placedItem.gridY == -1)
        {
            Debug.Log($"DeleteItem: 物品 {placedItem.uniqueId} 被视为子物品，更新其母物品的 container 属性");
            foreach (var pi in MapManager.Instance.placedItems)
            {
                if (pi.item.attributes != null && pi.item.attributes.ContainsKey("container"))
                {
                    string containerStr = pi.item.attributes["container"];
                    List<string> ids = new List<string>(containerStr.Split(','));
                    if (ids.Remove(placedItem.uniqueId))
                    {
                        string newContainer = string.Join(",", ids);
                        pi.item.attributes["container"] = newContainer;
                        Debug.Log($"DeleteItem: 母物品 {pi.uniqueId} container 更新为: '{newContainer}'");
                        Transform parentTransform = MapManager.Instance.mapContent.Find(pi.uniqueId);
                        if (parentTransform != null)
                        {
                            var logoCtrl = parentTransform.GetComponent<ContainerLogoController>();
                            if (logoCtrl != null)
                            {
                                bool hasChildren = ids.Count > 0;
                                logoCtrl.UpdateLogoVisibility(hasChildren);
                                if (hasChildren)
                                    logoCtrl.RefreshLogoPosition();
                            }
                        }

                        // 如果当前弹窗正在显示这个母物品，刷新它的子物品区
                        if (currentPlacedItem.HasValue && currentPlacedItem.Value.uniqueId == pi.uniqueId)
                        {
                            UpdateParentItemUI(currentPlacedItem.Value);
                        }
                    }
                }
            }
        }
        // 删除该物品对应的 UI 对象（从 popupParent 中查找）
        foreach (Transform child in popupParent)
        {
            if (child.gameObject.name == placedItem.uniqueId)
            {
                Debug.Log($"DeleteItem: 销毁 UI 对象，ID: {placedItem.uniqueId}");
                Destroy(child.gameObject);
                break;
            }
        }
        MapManager.Instance.RemoveItem(placedItem.uniqueId);
        MapManager.Instance.isDirty = true;
        Debug.Log($"DeleteItem: 物品 {placedItem.uniqueId} 删除成功");
        ClosePopup();
    }

    /// <summary>
    /// 关闭当前弹窗：若有上层则回到上一层，并还原对应的上下文；否则恢复地图操作。
    /// </summary>
    private void ClosePopup()
    {
        if (currentPopup != null)
        {
            Debug.Log("ClosePopup: 关闭当前弹窗");
            Destroy(currentPopup);
            currentPopup = null;
        }
        if (popupStack.Count > 0)
        {
            currentPopup = popupStack.Pop();
            currentPopup.SetActive(true);

            // 恢复当前层物品上下文
            if (contextStack.Count > 0)
                currentPlacedItem = contextStack.Pop();
            else
                currentPlacedItem = null;

            // 重新绑定当前弹窗的属性行引用
            RebindAttributeRowsFromCurrentPopup();

            // 按是否还能再返回设置按钮
            SetCloseButtonText(currentPopup, popupStack.Count > 0 ? "←" : "×");
        }
        else
        {
            EnableMapOperations();
            currentPlacedItem = null;
            contextStack.Clear();
        }
    }

    /// <summary>
    /// 禁用地图拖拽、建筑放置和缩放操作
    /// </summary>
    private void DisableMapOperations()
    {
        MapDragController mapDrag = FindObjectOfType<MapDragController>();
        if (mapDrag != null)
            mapDrag.enabled = false;
        BuildingPlacementController buildingPlacement = FindObjectOfType<BuildingPlacementController>();
        if (buildingPlacement != null)
            buildingPlacement.enabled = false;
        MapZoomController mapZoom = FindObjectOfType<MapZoomController>();
        if (mapZoom != null)
            mapZoom.enabled = false;
        Debug.Log("DisableMapOperations: 禁用地图操作");
    }

    /// <summary>
    /// 恢复地图拖拽、建筑放置和缩放操作
    /// </summary>
    private void EnableMapOperations()
    {
        MapDragController mapDrag = FindObjectOfType<MapDragController>();
        if (mapDrag != null)
            mapDrag.enabled = true;
        BuildingPlacementController buildingPlacement = FindObjectOfType<BuildingPlacementController>();
        if (buildingPlacement != null)
            buildingPlacement.enabled = true;
        MapZoomController mapZoom = FindObjectOfType<MapZoomController>();
        if (mapZoom != null)
            mapZoom.enabled = true;
        Debug.Log("EnableMapOperations: 恢复地图操作");
    }

    /// <summary>
    /// 添加子物品：当点击加号按钮时调用，创建子物品数据、更新当前层物品的 container 属性，
    /// 并在 UI 上追加子物品按钮。
    /// </summary>
    public void AddChildToContainer()
    {
        if (EditorManager.Instance.currentSelectedItem == null)
        {
            Debug.LogWarning("AddChildToContainer: 请先在工具栏选中要添加的子物品！");
            return;
        }
        EditorItem toolbarItem = EditorManager.Instance.currentSelectedItem;
        string newChildUniqueId = System.Guid.NewGuid().ToString();
        EditorItem childInstance = new EditorItem
        {
            uniqueId = newChildUniqueId,
            typeId = toolbarItem.typeId,
            itemName = toolbarItem.itemName,
            gridWidth = toolbarItem.gridWidth,
            gridHeight = toolbarItem.gridHeight,
            category = toolbarItem.category,
            thumbnail = toolbarItem.thumbnail,
            attributes = new System.Collections.Generic.Dictionary<string, string>()
        };
        PlacedItem childPlacedItem = new PlacedItem
        {
            uniqueId = newChildUniqueId,
            item = childInstance,
            category = toolbarItem.category,
            typeId = toolbarItem.typeId,
            gridX = -1,
            gridY = -1,
            gridWidth = toolbarItem.gridWidth,
            gridHeight = toolbarItem.gridHeight
        };
        MapManager.Instance.placedItems.Add(childPlacedItem);
        Debug.Log($"AddChildToContainer: 全局新增子物品 {newChildUniqueId}");

        if (!currentPlacedItem.HasValue)
            return;
        var updatedParent = currentPlacedItem.Value;

        string existing = "";
        if (updatedParent.item.attributes != null && updatedParent.item.attributes.ContainsKey("container"))
            existing = updatedParent.item.attributes["container"];
        if (!string.IsNullOrEmpty(existing))
            updatedParent.item.attributes["container"] = existing + "," + newChildUniqueId;
        else
        {
            if (updatedParent.item.attributes == null)
                updatedParent.item.attributes = new System.Collections.Generic.Dictionary<string, string>();
            updatedParent.item.attributes["container"] = newChildUniqueId;
        }
        currentPlacedItem = updatedParent;

        // UI 追加：使用 container 行的“子物品按钮预制体”
        if (attributeRows.TryGetValue("container", out var containerRow) &&
            containerRow != null && containerRow.containerChildButtonPrefab != null)
        {
            GameObject childButtonObj = Instantiate(containerRow.containerChildButtonPrefab, childListContainer);
            childButtonObj.name = newChildUniqueId;
            var ccbc = childButtonObj.GetComponent<ContainerChildButtonController>();
            if (ccbc != null)
            {
                ccbc.Initialize(newChildUniqueId, childInstance.thumbnail, childInstance.itemName);
            }
            else
            {
                Debug.LogError("AddChildToContainer: 子按钮预制体缺少 ContainerChildButtonController！");
            }

            // 保证加号按钮始终在最后
            if (containerRow.addChildButton != null)
                containerRow.addChildButton.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogWarning("AddChildToContainer: 未找到 container 行或其子按钮预制体未设置！");
        }

        Transform parentTransform = MapManager.Instance.mapContent.Find(updatedParent.uniqueId);
        if (parentTransform != null)
        {
            var logoCtrl = parentTransform.GetComponent<ContainerLogoController>();
            if (logoCtrl != null)
            {
                // 有子物品时显示并刷新位置
                logoCtrl.UpdateLogoVisibility(true);
                logoCtrl.RefreshLogoPosition();
            }
        }

        EditorManager.Instance.SetSelectedItem(null);
    }

    /// <summary>
    /// 移动按钮点击事件处理：进入移动模式，关闭当前弹窗，并设置当前选中物品用于预览移动。
    /// </summary>
    private void OnMoveButtonClicked()
    {
        if (!currentPlacedItem.HasValue)
        {
            Debug.LogWarning("OnMoveButtonClicked: 当前没有选中物品进行移动");
            return;
        }
        Debug.Log($"OnMoveButtonClicked: 物品 {currentPlacedItem.Value.uniqueId} 进入移动模式");
        MapManager.Instance.isMoveMode = true;
        MapManager.Instance.movingItem = currentPlacedItem.Value;
        EditorManager.Instance.SetSelectedItem(currentPlacedItem.Value.item);
        ClosePopup();
        Debug.Log("OnMoveButtonClicked: 进入移动模式，请点击地图确定新的位置");
    }

    /// <summary>
    /// 打开子物品弹窗（支持多层嵌套）：推入 UI 与上下文栈。
    /// </summary>
    public void OpenChildPopup(string childId)
    {
        if (currentPopup != null)
        {
            popupStack.Push(currentPopup);
            currentPopup.SetActive(false);
            // 记录当前层的物品上下文
            if (currentPlacedItem.HasValue)
                contextStack.Push(currentPlacedItem.Value);
        }
        PlacedItem? childItem = MapManager.Instance.placedItems.Find(x => x.uniqueId == childId);
        if (!childItem.HasValue)
        {
            Debug.LogWarning("OpenChildPopup: 未找到子物品: " + childId);
            if (popupStack.Count > 0)
            {
                currentPopup = popupStack.Pop();
                currentPopup.SetActive(true);
                // 上下文回退
                if (contextStack.Count > 0) currentPlacedItem = contextStack.Pop();
            }
            return;
        }
        currentPlacedItem = childItem;

        originalName = childItem.Value.item.itemName;
        originalAttributes = childItem.Value.item.attributes != null
            ? new Dictionary<string, string>(childItem.Value.item.attributes)
            : new Dictionary<string, string>();
        isApplied = false;

        GameObject childPopup = Instantiate(objectBuildingPopupPrefab, popupParent);
        currentPopup = childPopup;
        SetCloseButtonText(childPopup, "←");

        var closeButton = childPopup.transform.Find("CloseButton")?.GetComponent<Button>();
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                // 关闭当前子弹窗
                Destroy(childPopup);

                // 弹回上一层弹窗
                if (popupStack.Count > 0)
                {
                    currentPopup = popupStack.Pop();
                    currentPopup.SetActive(true);

                    // 恢复上一层的物品上下文
                    if (contextStack.Count > 0)
                        currentPlacedItem = contextStack.Pop();
                    else
                        currentPlacedItem = null;

                    // 重新绑定当前层的属性行 & 刷新子物品区
                    RebindAttributeRowsFromCurrentPopup();
                    if (currentPlacedItem.HasValue)
                        UpdateParentItemUI(currentPlacedItem.Value);

                    // 若还有更上层，可以继续返回，用“←”；否则“×”
                    SetCloseButtonText(currentPopup, popupStack.Count > 0 ? "←" : "×");
                }
                else
                {
                    EnableMapOperations();
                    currentPlacedItem = null;
                    contextStack.Clear();
                }
            });
        }

        // 子弹窗布局
        var basicPanel = childPopup.transform.Find("Basic");
        if (basicPanel != null)
        {
            var typeText = basicPanel.Find("TypeText")?.GetComponent<TMP_Text>();
            if (typeText != null)
                typeText.text = currentPlacedItem.Value.category.ToString();

            nameInputField = basicPanel.Find("NameInputField")?.GetComponent<TMP_InputField>();
            if (nameInputField != null)
            {
                nameInputField.text = originalName;
                nameInputField.onValueChanged.AddListener(OnNameChanged);
            }
            var image = basicPanel.Find("Image")?.GetComponent<Image>();
            if (image != null && currentPlacedItem.Value.item.thumbnail != null)
            {
                image.sprite = currentPlacedItem.Value.item.thumbnail;
                float origW = currentPlacedItem.Value.item.thumbnail.rect.width;
                float origH = currentPlacedItem.Value.item.thumbnail.rect.height;
                float scaleFactor = Mathf.Min(100f / origW, 100f / origH, 1f);
                image.rectTransform.sizeDelta = new Vector2(origW * scaleFactor, origH * scaleFactor);
            }
        }

        applyButton = childPopup.transform.Find("ApplyButton")?.GetComponent<Button>();
        if (applyButton != null)
            applyButton.onClick.AddListener(ApplyChanges);
        var deleteButton = childPopup.transform.Find("DeleteButton")?.GetComponent<Button>();
        if (deleteButton != null)
            deleteButton.onClick.AddListener(() => DeleteItem(currentPlacedItem.Value));

        // 子弹窗属性行（独立一套）
        BuildAttributeRowsFor(currentPlacedItem.Value);

        DisableMapOperations();

        if (readOnlyMode)
            ApplyReadOnlyMask();
    }

    /// <summary>
    /// 刷新当前激活弹窗（当前层物品）的子物品 UI。
    /// </summary>
    public void RefreshParentPopupChildren()
    {
        if (!currentPlacedItem.HasValue)
        {
            Debug.LogWarning("RefreshParentPopupChildren: currentPlacedItem 为空");
            return;
        }
        UpdateParentItemUI(currentPlacedItem.Value);
    }

    private void UpdateParentItemUI(PlacedItem parentItem)
    {
        Debug.Log($"UpdateParentItemUI: 刷新当前层弹窗，物品 ID: {parentItem.uniqueId}");
        if (childListContainer == null)
        {
            Debug.LogWarning("UpdateParentItemUI: childListContainer 未找到！");
            return;
        }
        // 清除除加号按钮（AddChildButton）外的其他子物品按钮
        List<Transform> toRemove = new List<Transform>();
        foreach (Transform child in childListContainer)
        {
            if (child.name != "AddChildButton")
                toRemove.Add(child);
        }
        foreach (Transform child in toRemove)
        {
            Destroy(child.gameObject);
        }
        // 根据该物品 container 属性生成子物品按钮
        if (parentItem.item.attributes != null && parentItem.item.attributes.ContainsKey("container"))
        {
            string containerStr = parentItem.item.attributes["container"];
            Debug.Log($"UpdateParentItemUI: container 值: '{containerStr}'");
            if (!string.IsNullOrEmpty(containerStr))
            {
                string[] childIds = containerStr.Split(',');
                foreach (string childId in childIds)
                {
                    PlacedItem? childItem = MapManager.Instance.placedItems.Find(x => x.uniqueId == childId);
                    if (childItem.HasValue)
                    {
                        CreateChildButton(childItem.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"UpdateParentItemUI: 未找到全局数据中子物品 ID: {childId}");
                    }
                }
            }
        }
        Debug.Log($"UpdateParentItemUI: 刷新完成，物品 ID: {parentItem.uniqueId}");
    }

    private void CreateChildButton(PlacedItem childItem)
    {
        if (childListContainer == null)
        {
            Debug.LogWarning("CreateChildButton: childListContainer 未找到！");
            return;
        }

        // 使用“container 行”的子物品按钮预制体（不要再用 optionalAttributeRowPrefab）
        if (attributeRows.TryGetValue("container", out var containerRow) &&
            containerRow != null && containerRow.containerChildButtonPrefab != null)
        {
            GameObject childButtonObj = Instantiate(containerRow.containerChildButtonPrefab, childListContainer);
            childButtonObj.name = childItem.uniqueId;
            ContainerChildButtonController ccbc = childButtonObj.GetComponent<ContainerChildButtonController>();
            if (ccbc != null)
            {
                ccbc.Initialize(childItem.uniqueId, childItem.item.thumbnail, childItem.item.itemName);
                Debug.Log($"CreateChildButton: 创建子物品按钮成功，ID: {childItem.uniqueId}");
            }
            else
            {
                Debug.LogError("CreateChildButton: 预制体缺少 ContainerChildButtonController！");
            }

            // 保证加号按钮在最后
            if (containerRow.addChildButton != null)
                containerRow.addChildButton.transform.SetAsLastSibling();
        }
        else
        {
            Debug.LogError("CreateChildButton: 未找到 container 行或未设置子物品按钮预制体！");
        }
    }

    /*============================================================
     *                 只读模式: 隐藏编辑功能
     *==========================================================*/
    private void ApplyReadOnlyMask()
    {
        Transform t;
        // 1) 隐藏编辑相关按钮
        if ((t = currentPopup.transform.Find("ApplyButton")) != null)
            t.gameObject.SetActive(false);
        if ((t = currentPopup.transform.Find("DeleteButton")) != null)
            t.gameObject.SetActive(false);
        if ((t = currentPopup.transform.Find("MoveButton")) != null)
            t.gameObject.SetActive(false);

        // 2) 禁止输入框交互
        if (nameInputField != null)
            nameInputField.interactable = false;

        // 3) 隐藏属性编辑区域（如需只读可自行改造）
        if ((t = currentPopup.transform.Find("AttributesPanel")) != null)
            t.gameObject.SetActive(false);
    }

    // ===== 新增：返回某层后，重建 attributeRows 指向“当前弹窗”的行控件 =====
    private void RebindAttributeRowsFromCurrentPopup()
    {
        attributeRows.Clear();
        if (currentPopup == null) return;

        var attributesPanel = currentPopup.transform.Find("AttributesPanel");
        if (attributesPanel == null) return;

        foreach (Transform row in attributesPanel)
        {
            var ctrl = row.GetComponent<OptionalAttributeRowController>();
            if (ctrl == null) continue;

            // 行节点名是 "<attrName>_Row"
            string key = row.name;
            if (key.EndsWith("_Row")) key = key.Substring(0, key.Length - 4);

            attributeRows[key] = ctrl;

            // 重新绑定 container 的“+”按钮
            if (key == "container" && ctrl.addChildButton != null)
            {
                ctrl.addChildButton.onClick.RemoveAllListeners();
                ctrl.addChildButton.onClick.AddListener(AddChildToContainer);
            }
        }
    }
}
