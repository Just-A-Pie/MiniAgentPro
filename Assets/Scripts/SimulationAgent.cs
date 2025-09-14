using System;
using System.Linq;
using System.Collections.Generic;          // ★ 为 NoteworthyEntry 提供 List/Array
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Data model for a single agent at one time‑step.
/// </summary>
[Serializable]
public class SimulationAgent
{
    public string name;
    public string activity;
    public string location;

    /// <summary>
    /// Inventory list.
    /// The JSON source may be either
    ///   "bag": ["bread", "shampoo"]
    /// or
    ///   "bag": [["bread","uuid‑1"], ["shampoo","uuid‑2"]]
    /// The custom BagConverter below normalises both formats
    /// into a simple string[] that only keeps the item names.
    /// </summary>
    [JsonConverter(typeof(BagConverter))]
    public string[] bag;

    public int[] curr_tile;
    public string short_activity;
    public string walkingSpriteSheetName;
}

/// <summary>
/// Custom Newtonsoft.Json converter that strips any extra metadata
/// from the "bag" field and returns just the item names.
/// </summary>
public class BagConverter : JsonConverter<string[]>
{
    public override string[] ReadJson(JsonReader reader,
                                      Type objectType,
                                      string[] existingValue,
                                      bool hasExistingValue,
                                      JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        JArray arr = JArray.Load(reader);

        // Case A: already a flat string array.
        if (arr.Count == 0 || arr[0]?.Type != JTokenType.Array)
        {
            return arr.Select(t => t.ToString()).ToArray();
        }

        // Case B: nested arrays -> take first element of each sub‑array.
        return arr
            .Where(sub => sub is JArray && sub.Count() > 0)
            .Select(sub => sub[0]?.ToString())
            .ToArray();
    }

    public override void WriteJson(JsonWriter writer,
                                   string[] value,
                                   JsonSerializer serializer)
    {
        // Serialise back as simple string array.
        new JArray(value).WriteTo(writer);
    }
}

/// <summary>
/// ★ 单个 noteworthy 事件的数据结构
/// </summary>
[Serializable]
public class NoteworthyEntry
{
    public string eventText;     // JSON 中的 "event"
    public string[] people;      // JSON 中的 "people" 数组
}
