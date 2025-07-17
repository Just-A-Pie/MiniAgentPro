// 文件：SimulationItemCreator.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public static class SimulationItemCreator
{
    /// <summary>
    /// 在 Simulation 场景中创建并放置一个物品实例，添加 Outline、事件和调试日志，并向 UIController 注册。
    /// </summary>
    public static void CreateItemInstance(
        EditorItem item,
        int gridX,
        int gridY,
        EditorItemCategory cat,
        RectTransform mapContent)
    {
        // 1️⃣ 计算位置 & 缩放
        float factor = SimulationMapManager.Instance?.backgroundScaleFactor ?? 1f;
        float gridSize = 32f;
        Vector2 pos = new Vector2(gridX * gridSize * factor, -gridY * gridSize * factor);
        Debug.Log($"[SimulationItemCreator] Creating {cat} instance id={item.uniqueId} at ({gridX},{gridY}), factor={factor}");

        // 2️⃣ 创建 GameObject + Image
        GameObject go = new GameObject($"{cat}_{item.uniqueId}", typeof(Image));
        go.transform.SetParent(mapContent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = item.thumbnail;

        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(item.gridWidth * gridSize * factor,
                                   item.gridHeight * gridSize * factor);
        rt.anchoredPosition = pos;

        // 3️⃣ 添加 Outline
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.yellow;
        outline.effectDistance = new Vector2(1, 1);
        outline.enabled = false;

        // 4️⃣ 构造一个 PlacedItem 用于 UIController
        var placedItem = new MapManager.PlacedItem
        {
            uniqueId = item.uniqueId,
            item = item,
            category = item.category,
            typeId = item.typeId,
            gridX = gridX,
            gridY = gridY,
            gridWidth = item.gridWidth,
            gridHeight = item.gridHeight
        };

        // 5️⃣ 注册到 SimulationUIController
        if (SimulationUIController.Instance != null)
        {
            SimulationUIController.Instance.RegisterItem(item.uniqueId, outline, placedItem);
            Debug.Log($"[SimulationItemCreator] Registered item id={item.uniqueId}");
        }
        else
        {
            Debug.LogError("[SimulationItemCreator] SimulationUIController.Instance == null!");
        }

        // 6️⃣ 添加鼠标事件回调
        EventTrigger trigger = go.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        // PointerEnter
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) =>
        {
            Debug.Log($"[SimulationItemCreator] PointerEnter on id={item.uniqueId}");
            SimulationUIController.Instance?.OnItemPointerEnter(item.uniqueId);
        });
        trigger.triggers.Add(entryEnter);

        // PointerExit
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) =>
        {
            Debug.Log($"[SimulationItemCreator] PointerExit on id={item.uniqueId}");
            SimulationUIController.Instance?.OnItemPointerExit(item.uniqueId);
        });
        trigger.triggers.Add(entryExit);

        // PointerClick
        var entryClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entryClick.callback.AddListener((data) =>
        {
            Debug.Log($"[SimulationItemCreator] PointerClick on id={item.uniqueId}");
            SimulationUIController.Instance?.OnItemPointerClick(item.uniqueId);
        });
        trigger.triggers.Add(entryClick);
    }
}
