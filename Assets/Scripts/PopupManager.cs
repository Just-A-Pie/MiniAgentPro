// 文件: PopupManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using static MapManager;

public class PopupManager : MonoBehaviour
{
    /*==============================*
     *        公共配置字段          *
     *==============================*/
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

    // 用于管理弹窗的堆栈（用于子弹窗的返回）
    private Stack<GameObject> popupStack = new Stack<GameObject>();

    private GameObject currentPopup;
    private PlacedItem? currentPlacedItem;
    // 保存打开子弹窗前的父物品数据
    private PlacedItem? parentPlacedItem;
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
    /// 显示物品编辑弹窗，确保弹窗总在顶层（挂在 DialogCanvas 下）。
    /// 设置基本信息、初始化属性行等。
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
        popupStack.Clear();
        if (currentPopup != null)
            Destroy(currentPopup);

        // 在顶层弹窗父容器下实例化对话框
        currentPopup = Instantiate(objectBuildingPopupPrefab, popupParent);
        // 确保当前弹窗位于父容器的最后（最上层）
        currentPopup.transform.SetAsLastSibling();

        // 如果弹窗上挂有 Canvas，则设置排序，使其始终在顶层显示
        Canvas cv = currentPopup.GetComponent<Canvas>();
        if (cv != null)
        {
            cv.overrideSorting = true;
            cv.sortingOrder = 1000;
        }

        SetCloseButtonText(currentPopup, "×");

        currentPlacedItem = placedItem;
        // 每次显示父弹窗时清空父数据
        parentPlacedItem = null;
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
                if (placedItem.item.attributes != null && placedItem.item.attributes.ContainsKey(attrName))
                {
                    enabled = true;
                    defaultVal = placedItem.item.attributes[attrName];
                }
                CreateOptionalAttributeRow(attrName, attributesPanel, enabled, defaultVal);
            }
        }
        else
        {
            Debug.LogWarning("ShowPopup: 未找到 AttributesPanel");
        }

        DisableMapOperations();
        Debug.Log("ShowPopup: 弹窗显示完成");

        /*=================== 只读模式处理 ===================*/
        if (readOnlyMode)
            ApplyReadOnlyMask();
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

                        // 使用 parentPlacedItem 判断当前弹窗是否为父弹窗
                        if (parentPlacedItem.HasValue && parentPlacedItem.Value.uniqueId == pi.uniqueId)
                        {
                            Debug.Log($"DeleteItem: 当前弹窗为父物品 {pi.uniqueId}，刷新弹窗");
                            popupStack.Clear();
                            ClosePopup();
                            PlacedItem? latestParent = MapManager.Instance.placedItems.Find(x => x.uniqueId == pi.uniqueId);
                            if (latestParent.HasValue)
                            {
                                Debug.Log($"DeleteItem: 重新打开父物品弹窗，ID: {latestParent.Value.uniqueId}");
                                ShowPopup(latestParent.Value);
                                parentPlacedItem = null; // 清空父物品数据
                                return; // 刷新后退出删除方法
                            }
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
    /// 关闭当前弹窗，如果有子弹窗则弹出上一级弹窗，否则恢复地图操作
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
            if (popupStack.Count == 0)
            {
                SetCloseButtonText(currentPopup, "×");
                parentPlacedItem = null; // 弹窗堆栈为空时，清空父数据
            }
        }
        else
        {
            EnableMapOperations();
            parentPlacedItem = null;
        }
        currentPlacedItem = null;
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
    /// 添加子物品：当点击加号按钮时调用，创建子物品数据、更新当前父物品的 container 属性，
    /// 并调用 OptionalAttributeRowController 的 AddChild 方法生成子物品按钮。
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

        if (attributeRows.ContainsKey("container"))
        {
            OptionalAttributeRowController containerRowController = attributeRows["container"];
            containerRowController.AddChild(newChildUniqueId, childInstance.thumbnail, childInstance.itemName);
            Debug.Log($"AddChildToContainer: 调用 OptionalAttributeRowController.AddChild 添加子物品按钮，ID: {newChildUniqueId}");
        }
        else
        {
            Debug.LogWarning("AddChildToContainer: 未找到 container 行的 OptionalAttributeRowController");
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
    /// 打开子物品弹窗
    /// </summary>
    public void OpenChildPopup(string childId)
    {
        if (currentPopup != null)
        {
            popupStack.Push(currentPopup);
            currentPopup.SetActive(false);
            if (!parentPlacedItem.HasValue)
                parentPlacedItem = currentPlacedItem;
        }
        PlacedItem? childItem = MapManager.Instance.placedItems.Find(x => x.uniqueId == childId);
        if (!childItem.HasValue)
        {
            Debug.LogWarning("OpenChildPopup: 未找到子物品: " + childId);
            if (popupStack.Count > 0)
            {
                currentPopup = popupStack.Pop();
                currentPopup.SetActive(true);
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
                Destroy(childPopup);
                if (popupStack.Count > 0)
                {
                    currentPopup = popupStack.Pop();
                    currentPopup.SetActive(true);
                    if (popupStack.Count == 0)
                    {
                        SetCloseButtonText(currentPopup, "×");
                    }
                }
                else
                {
                    EnableMapOperations();
                    parentPlacedItem = null;
                }
            });
        }

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
        var attributesPanel = childPopup.transform.Find("AttributesPanel");
        if (attributesPanel != null)
        {
            foreach (Transform child in attributesPanel)
                Destroy(child.gameObject);
            attributeRows.Clear();
            foreach (string attrName in optionalAttributes)
            {
                bool enabled = false;
                string defaultVal = "";
                if (currentPlacedItem.Value.item.attributes != null && currentPlacedItem.Value.item.attributes.ContainsKey(attrName))
                {
                    enabled = true;
                    defaultVal = currentPlacedItem.Value.item.attributes[attrName];
                }
                CreateOptionalAttributeRow(attrName, attributesPanel, enabled, defaultVal);
            }
        }
        DisableMapOperations();

        if (readOnlyMode)
            ApplyReadOnlyMask();
    }

    /// <summary>
    /// 刷新父物品弹窗中子物品的 UI，使其与最新的 container 数据一致。
    /// </summary>
    public void RefreshParentPopupChildren()
    {
        if (!parentPlacedItem.HasValue)
        {
            Debug.LogWarning("RefreshParentPopupChildren: parentPlacedItem 为 null");
            return;
        }
        Debug.Log($"RefreshParentPopupChildren: 刷新父弹窗中子物品按钮，父物品 ID: {parentPlacedItem.Value.uniqueId}");
        UpdateParentItemUI(parentPlacedItem.Value);
    }

    private void UpdateParentItemUI(PlacedItem parentItem)
    {
        Debug.Log($"UpdateParentItemUI: 刷新父弹窗，父物品 ID: {parentItem.uniqueId}");
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
            Debug.Log($"UpdateParentItemUI: 删除子物品按钮: {child.name}");
            Destroy(child.gameObject);
        }
        // 根据父物品 container 属性重新生成子物品按钮
        if (parentItem.item.attributes != null && parentItem.item.attributes.ContainsKey("container"))
        {
            string containerStr = parentItem.item.attributes["container"];
            Debug.Log($"UpdateParentItemUI: 父物品 container 值: '{containerStr}'");
            if (!string.IsNullOrEmpty(containerStr))
            {
                string[] childIds = containerStr.Split(',');
                foreach (string childId in childIds)
                {
                    PlacedItem? childItem = MapManager.Instance.placedItems.Find(x => x.uniqueId == childId);
                    if (childItem.HasValue)
                    {
                        Debug.Log($"UpdateParentItemUI: 生成子物品按钮，ID: {childItem.Value.uniqueId}");
                        CreateChildButton(childItem.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"UpdateParentItemUI: 未找到全局数据中子物品 ID: {childId}");
                    }
                }
            }
        }
        Debug.Log($"UpdateParentItemUI: 刷新完成，父物品 ID: {parentItem.uniqueId}");
    }

    private void CreateChildButton(PlacedItem childItem)
    {
        if (childListContainer == null)
        {
            Debug.LogWarning("CreateChildButton: childListContainer 未找到！");
            return;
        }
        GameObject childButtonObj = Instantiate(optionalAttributeRowPrefab, childListContainer);
        childButtonObj.name = childItem.uniqueId;
        ContainerChildButtonController ccbc = childButtonObj.GetComponent<ContainerChildButtonController>();
        if (ccbc != null)
        {
            ccbc.Initialize(childItem.uniqueId, childItem.item.thumbnail, childItem.item.itemName);
            Debug.Log($"CreateChildButton: 创建子物品按钮成功，ID: {childItem.uniqueId}");
        }
        else
        {
            Debug.LogWarning($"CreateChildButton: ContainerChildButtonController 未挂载在 {childButtonObj.name}");
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
}
