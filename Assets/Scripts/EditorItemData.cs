using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EditorItem
{
    public string uniqueId;
    public int typeId;
    public string itemName;
    public int gridWidth;
    public int gridHeight;
    public EditorItemCategory category;
    public Sprite thumbnail;
    public Dictionary<string, string> attributes; // ¿ÉÑ¡ÊôÐÔ×Öµä
}

public enum EditorItemCategory
{
    All,
    Object,
    Building,
    Suit
}
