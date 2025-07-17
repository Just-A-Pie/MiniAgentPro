// �ļ�: PopupManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using static MapManager;

public class PopupManager : MonoBehaviour
{
    /*==============================*
     *        ���������ֶ�          *
     *==============================*/
    [Header("����ģʽ")]
    [Tooltip("SimulationPage ��ѡ��ֻ�鿴��Ϣ��������༭/ɾ��/�ƶ�")]
    public bool readOnlyMode = false;

    [Header("����Ԥ����")]
    public GameObject objectBuildingPopupPrefab; // ������Ԥ����

    [Header("���㵯��������")]
    [Tooltip("��ָ�򳡾���ר��������ʾ�Ի���Ķ��� Canvas �µ�������������Ϊ DialogCanvas �Ķ���")]
    public Transform popupParent;

    [Header("��ѡ������Ԥ����")]
    public GameObject optionalAttributeRowPrefab; // �������������У����� container �У�

    // Ԥ�����ѡ�����б��˴� container �и�������Ʒ��ʾ
    private List<string> optionalAttributes = new List<string>()
    {
        "quantity",
        "container"
    };

    // ���ڹ������Ķ�ջ�������ӵ����ķ��أ�
    private Stack<GameObject> popupStack = new Stack<GameObject>();

    private GameObject currentPopup;
    private PlacedItem? currentPlacedItem;
    // ������ӵ���ǰ�ĸ���Ʒ����
    private PlacedItem? parentPlacedItem;
    private string originalName;
    private Dictionary<string, string> originalAttributes;
    private TMP_InputField nameInputField;
    private Button applyButton;
    private bool isApplied = false;
    // ������������ж�Ӧ�� OptionalAttributeRowController��container �о������У�
    private Dictionary<string, OptionalAttributeRowController> attributeRows = new Dictionary<string, OptionalAttributeRowController>();

    // ����Ʒ��ʾ����������Ԥ����ṹΪ��objectBuildingPopupPrefab -> AttributesPanel -> container_Row -> ChildContainerPanel
    private Transform childListContainer
    {
        get
        {
            if (currentPopup == null)
            {
                Debug.LogWarning("childListContainer: currentPopup Ϊ null");
                return null;
            }
            Transform t = currentPopup.transform.Find("AttributesPanel/container_Row/ChildContainerPanel");
            if (t == null)
                Debug.LogWarning("childListContainer δ�ҵ�������Ԥ������ 'AttributesPanel/container_Row/ChildContainerPanel' ������");
            return t;
        }
    }

    /// <summary>
    /// ��ʾ��Ʒ�༭������ȷ���������ڶ��㣨���� DialogCanvas �£���
    /// ���û�����Ϣ����ʼ�������еȡ�
    /// </summary>
    public void ShowPopup(PlacedItem placedItem)
    {
        // ȷ�� popupParent �Ѿ���ֵ
        if (popupParent == null)
        {
            GameObject dialogGO = GameObject.Find("DialogCanvas");
            if (dialogGO != null)
            {
                popupParent = dialogGO.transform;
            }
            else
            {
                Debug.LogError("PopupManager: δ���� popupParent ��δ�ҵ���Ϊ 'DialogCanvas' �Ķ���");
                return;
            }
        }

        if (EditorManager.Instance.currentSelectedItem != null)
            return;

        Debug.Log($"ShowPopup: ��ʾ��Ʒ������ID: {placedItem.uniqueId}");
        popupStack.Clear();
        if (currentPopup != null)
            Destroy(currentPopup);

        // �ڶ��㵯����������ʵ�����Ի���
        currentPopup = Instantiate(objectBuildingPopupPrefab, popupParent);
        // ȷ����ǰ����λ�ڸ�������������ϲ㣩
        currentPopup.transform.SetAsLastSibling();

        // ��������Ϲ��� Canvas������������ʹ��ʼ���ڶ�����ʾ
        Canvas cv = currentPopup.GetComponent<Canvas>();
        if (cv != null)
        {
            cv.overrideSorting = true;
            cv.sortingOrder = 1000;
        }

        SetCloseButtonText(currentPopup, "��");

        currentPlacedItem = placedItem;
        // ÿ����ʾ������ʱ��ո�����
        parentPlacedItem = null;
        originalName = placedItem.item.itemName;
        originalAttributes = placedItem.item.attributes != null
            ? new Dictionary<string, string>(placedItem.item.attributes)
            : new Dictionary<string, string>();
        isApplied = false;

        // ���û������
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
            Debug.LogWarning("ShowPopup: δ�ҵ� Basic ���");
        }

        // ���� Apply��Delete �� Move ��ť
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

        // ���������У����� container �У����ؽ�ȫ��������
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
            Debug.LogWarning("ShowPopup: δ�ҵ� AttributesPanel");
        }

        DisableMapOperations();
        Debug.Log("ShowPopup: ������ʾ���");

        /*=================== ֻ��ģʽ���� ===================*/
        if (readOnlyMode)
            ApplyReadOnlyMask();
    }

    /// <summary>
    /// ���õ����رհ�ť���ı�
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
    /// ������ѡ�����У��������ü��¼���
    /// </summary>
    private void CreateOptionalAttributeRow(string attrName, Transform parent, bool defaultEnabled, string defaultValue)
    {
        if (!optionalAttributeRowPrefab)
        {
            Debug.LogError("PopupManager: optionalAttributeRowPrefab δ���ã�");
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
                controller.inputField.onValueChanged.AddListener((value) => { /* ����չ���� */ });
            }
        }
        else
        {
            Debug.LogError("CreateOptionalAttributeRow: OptionalAttributeRowController �ű�δ������ " + rowObj.name);
        }
    }

    private void OnNameChanged(string newText)
    {
        Debug.Log($"OnNameChanged: ������Ϊ {newText}");
    }

    /// <summary>
    /// Ӧ���޸ģ�������Ʒ���Ƽ����ԣ���ͬ����ȫ������
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
        // ����ȫ���б��ж�Ӧ��Ʒ������
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
        Debug.Log($"ApplyChanges: ��Ʒ [{updatedItem.uniqueId}] ���Ƹ�Ϊ: {newName}, ���� {newAttributes.Count} ������");

        // ͬ�����¸���Ʒ�ϵ�����ͼ����ʾ״̬
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
    /// ɾ����Ʒ�������ݹ�ɾ�� container �е�����Ʒ��������ĸ��Ʒ�� container ���ԣ�
    /// </summary>
    private void DeleteItem(PlacedItem placedItem)
    {
        Debug.Log($"DeleteItem: ����ɾ����Ʒ {placedItem.uniqueId} ({placedItem.item.itemName})");
        // �ݹ�ɾ������Ʒ container �е�����Ʒ
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
                        Debug.Log($"DeleteItem: �ݹ�ɾ������Ʒ {child.Value.uniqueId} from {placedItem.uniqueId}");
                        DeleteItem(child.Value);
                    }
                }
            }
        }
        // ���ɾ����������Ʒ���������ĸ��Ʒ�� container ����
        if (placedItem.gridX == -1 && placedItem.gridY == -1)
        {
            Debug.Log($"DeleteItem: ��Ʒ {placedItem.uniqueId} ����Ϊ����Ʒ��������ĸ��Ʒ�� container ����");
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
                        Debug.Log($"DeleteItem: ĸ��Ʒ {pi.uniqueId} container ����Ϊ: '{newContainer}'");
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

                        // ʹ�� parentPlacedItem �жϵ�ǰ�����Ƿ�Ϊ������
                        if (parentPlacedItem.HasValue && parentPlacedItem.Value.uniqueId == pi.uniqueId)
                        {
                            Debug.Log($"DeleteItem: ��ǰ����Ϊ����Ʒ {pi.uniqueId}��ˢ�µ���");
                            popupStack.Clear();
                            ClosePopup();
                            PlacedItem? latestParent = MapManager.Instance.placedItems.Find(x => x.uniqueId == pi.uniqueId);
                            if (latestParent.HasValue)
                            {
                                Debug.Log($"DeleteItem: ���´򿪸���Ʒ������ID: {latestParent.Value.uniqueId}");
                                ShowPopup(latestParent.Value);
                                parentPlacedItem = null; // ��ո���Ʒ����
                                return; // ˢ�º��˳�ɾ������
                            }
                        }
                    }
                }
            }
        }
        // ɾ������Ʒ��Ӧ�� UI ���󣨴� popupParent �в��ң�
        foreach (Transform child in popupParent)
        {
            if (child.gameObject.name == placedItem.uniqueId)
            {
                Debug.Log($"DeleteItem: ���� UI ����ID: {placedItem.uniqueId}");
                Destroy(child.gameObject);
                break;
            }
        }
        MapManager.Instance.RemoveItem(placedItem.uniqueId);
        MapManager.Instance.isDirty = true;
        Debug.Log($"DeleteItem: ��Ʒ {placedItem.uniqueId} ɾ���ɹ�");
        ClosePopup();
    }

    /// <summary>
    /// �رյ�ǰ������������ӵ����򵯳���һ������������ָ���ͼ����
    /// </summary>
    private void ClosePopup()
    {
        if (currentPopup != null)
        {
            Debug.Log("ClosePopup: �رյ�ǰ����");
            Destroy(currentPopup);
            currentPopup = null;
        }
        if (popupStack.Count > 0)
        {
            currentPopup = popupStack.Pop();
            currentPopup.SetActive(true);
            if (popupStack.Count == 0)
            {
                SetCloseButtonText(currentPopup, "��");
                parentPlacedItem = null; // ������ջΪ��ʱ����ո�����
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
    /// ���õ�ͼ��ק���������ú����Ų���
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
        Debug.Log("DisableMapOperations: ���õ�ͼ����");
    }

    /// <summary>
    /// �ָ���ͼ��ק���������ú����Ų���
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
        Debug.Log("EnableMapOperations: �ָ���ͼ����");
    }

    /// <summary>
    /// �������Ʒ��������ӺŰ�ťʱ���ã���������Ʒ���ݡ����µ�ǰ����Ʒ�� container ���ԣ�
    /// ������ OptionalAttributeRowController �� AddChild ������������Ʒ��ť��
    /// </summary>
    public void AddChildToContainer()
    {
        if (EditorManager.Instance.currentSelectedItem == null)
        {
            Debug.LogWarning("AddChildToContainer: �����ڹ�����ѡ��Ҫ��ӵ�����Ʒ��");
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
        Debug.Log($"AddChildToContainer: ȫ����������Ʒ {newChildUniqueId}");

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
            Debug.Log($"AddChildToContainer: ���� OptionalAttributeRowController.AddChild �������Ʒ��ť��ID: {newChildUniqueId}");
        }
        else
        {
            Debug.LogWarning("AddChildToContainer: δ�ҵ� container �е� OptionalAttributeRowController");
        }

        Transform parentTransform = MapManager.Instance.mapContent.Find(updatedParent.uniqueId);
        if (parentTransform != null)
        {
            var logoCtrl = parentTransform.GetComponent<ContainerLogoController>();
            if (logoCtrl != null)
            {
                // ������Ʒʱ��ʾ��ˢ��λ��
                logoCtrl.UpdateLogoVisibility(true);
                logoCtrl.RefreshLogoPosition();
            }
        }

        EditorManager.Instance.SetSelectedItem(null);

    }

    /// <summary>
    /// �ƶ���ť����¼����������ƶ�ģʽ���رյ�ǰ�����������õ�ǰѡ����Ʒ����Ԥ���ƶ���
    /// </summary>
    private void OnMoveButtonClicked()
    {
        if (!currentPlacedItem.HasValue)
        {
            Debug.LogWarning("OnMoveButtonClicked: ��ǰû��ѡ����Ʒ�����ƶ�");
            return;
        }
        Debug.Log($"OnMoveButtonClicked: ��Ʒ {currentPlacedItem.Value.uniqueId} �����ƶ�ģʽ");
        MapManager.Instance.isMoveMode = true;
        MapManager.Instance.movingItem = currentPlacedItem.Value;
        EditorManager.Instance.SetSelectedItem(currentPlacedItem.Value.item);
        ClosePopup();
        Debug.Log("OnMoveButtonClicked: �����ƶ�ģʽ��������ͼȷ���µ�λ��");
    }

    /// <summary>
    /// ������Ʒ����
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
            Debug.LogWarning("OpenChildPopup: δ�ҵ�����Ʒ: " + childId);
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
        SetCloseButtonText(childPopup, "��");

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
                        SetCloseButtonText(currentPopup, "��");
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
    /// ˢ�¸���Ʒ����������Ʒ�� UI��ʹ�������µ� container ����һ�¡�
    /// </summary>
    public void RefreshParentPopupChildren()
    {
        if (!parentPlacedItem.HasValue)
        {
            Debug.LogWarning("RefreshParentPopupChildren: parentPlacedItem Ϊ null");
            return;
        }
        Debug.Log($"RefreshParentPopupChildren: ˢ�¸�����������Ʒ��ť������Ʒ ID: {parentPlacedItem.Value.uniqueId}");
        UpdateParentItemUI(parentPlacedItem.Value);
    }

    private void UpdateParentItemUI(PlacedItem parentItem)
    {
        Debug.Log($"UpdateParentItemUI: ˢ�¸�����������Ʒ ID: {parentItem.uniqueId}");
        if (childListContainer == null)
        {
            Debug.LogWarning("UpdateParentItemUI: childListContainer δ�ҵ���");
            return;
        }
        // ������ӺŰ�ť��AddChildButton�������������Ʒ��ť
        List<Transform> toRemove = new List<Transform>();
        foreach (Transform child in childListContainer)
        {
            if (child.name != "AddChildButton")
                toRemove.Add(child);
        }
        foreach (Transform child in toRemove)
        {
            Debug.Log($"UpdateParentItemUI: ɾ������Ʒ��ť: {child.name}");
            Destroy(child.gameObject);
        }
        // ���ݸ���Ʒ container ����������������Ʒ��ť
        if (parentItem.item.attributes != null && parentItem.item.attributes.ContainsKey("container"))
        {
            string containerStr = parentItem.item.attributes["container"];
            Debug.Log($"UpdateParentItemUI: ����Ʒ container ֵ: '{containerStr}'");
            if (!string.IsNullOrEmpty(containerStr))
            {
                string[] childIds = containerStr.Split(',');
                foreach (string childId in childIds)
                {
                    PlacedItem? childItem = MapManager.Instance.placedItems.Find(x => x.uniqueId == childId);
                    if (childItem.HasValue)
                    {
                        Debug.Log($"UpdateParentItemUI: ��������Ʒ��ť��ID: {childItem.Value.uniqueId}");
                        CreateChildButton(childItem.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"UpdateParentItemUI: δ�ҵ�ȫ������������Ʒ ID: {childId}");
                    }
                }
            }
        }
        Debug.Log($"UpdateParentItemUI: ˢ����ɣ�����Ʒ ID: {parentItem.uniqueId}");
    }

    private void CreateChildButton(PlacedItem childItem)
    {
        if (childListContainer == null)
        {
            Debug.LogWarning("CreateChildButton: childListContainer δ�ҵ���");
            return;
        }
        GameObject childButtonObj = Instantiate(optionalAttributeRowPrefab, childListContainer);
        childButtonObj.name = childItem.uniqueId;
        ContainerChildButtonController ccbc = childButtonObj.GetComponent<ContainerChildButtonController>();
        if (ccbc != null)
        {
            ccbc.Initialize(childItem.uniqueId, childItem.item.thumbnail, childItem.item.itemName);
            Debug.Log($"CreateChildButton: ��������Ʒ��ť�ɹ���ID: {childItem.uniqueId}");
        }
        else
        {
            Debug.LogWarning($"CreateChildButton: ContainerChildButtonController δ������ {childButtonObj.name}");
        }
    }

    /*============================================================
     *                 ֻ��ģʽ: ���ر༭����
     *==========================================================*/
    private void ApplyReadOnlyMask()
    {
        Transform t;
        // 1) ���ر༭��ذ�ť
        if ((t = currentPopup.transform.Find("ApplyButton")) != null)
            t.gameObject.SetActive(false);
        if ((t = currentPopup.transform.Find("DeleteButton")) != null)
            t.gameObject.SetActive(false);
        if ((t = currentPopup.transform.Find("MoveButton")) != null)
            t.gameObject.SetActive(false);

        // 2) ��ֹ����򽻻�
        if (nameInputField != null)
            nameInputField.interactable = false;

        // 3) �������Ա༭��������ֻ�������и��죩
        if ((t = currentPopup.transform.Find("AttributesPanel")) != null)
            t.gameObject.SetActive(false);
    }
}
