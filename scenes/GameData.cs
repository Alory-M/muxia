using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>策划表的统一入口。速度单位转为 20px/s，距离单位转为 32px。</summary>
public static class GameData
{
    public const float SpeedUnit = 20f;
    public const float DistanceUnit = 32f;
    private static readonly Dictionary<string, JsonElement> Tables = new();
    private static readonly Dictionary<string, DialogueBag> DialogueBags = new();
    private sealed class DialogueBag
    {
        public readonly List<string> Choices = new();
        public int Next;
        public string Previous;
    }
    public static JsonElement Table(string name)
    {
        if (!Tables.TryGetValue(name, out var table))
        {
            string path = $"res://data/{name}.json";
            using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"{path} must contain an array");
            table = doc.RootElement.Clone();
            Tables[name] = table;
        }
        return table;
    }
    public static JsonElement Row(string table, int id)
    {
        foreach (var row in Table(table).EnumerateArray())
            if (row.GetProperty("ID").GetInt32() == id) return row;
        throw new InvalidOperationException($"Missing {table} ID {id}");
    }
    public static float Number(JsonElement row, string key) => row.GetProperty(key).GetSingle();
    public static string Text(string key)
    {
        foreach (var row in Table("lang").EnumerateArray())
            if (row.GetProperty("key").GetString() == key) return row.GetProperty("text").GetString();
        return key;
    }
    /// <summary>从 lang 表的同一编号文本组随机取一句；每轮用完再洗牌，避免连续重复。</summary>
    public static string RandomText(string prefix, string fallbackKey = null)
    {
        if (!DialogueBags.TryGetValue(prefix, out var bag))
        {
            bag = new DialogueBag();
            foreach (var row in Table("lang").EnumerateArray())
            {
                string key = row.GetProperty("key").GetString();
                if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal) ||
                    !int.TryParse(key[prefix.Length..], out int number) || number <= 0) continue;
                string value = row.GetProperty("text").GetString();
                if (!string.IsNullOrWhiteSpace(value) && !bag.Choices.Contains(value)) bag.Choices.Add(value);
            }
            bag.Next = bag.Choices.Count;
            DialogueBags[prefix] = bag;
        }
        if (bag.Choices.Count == 0) return Text(fallbackKey ?? prefix + "1");
        if (bag.Next >= bag.Choices.Count)
        {
            for (int i = bag.Choices.Count - 1; i > 0; i--)
            {
                int other = GD.RandRange(0, i);
                (bag.Choices[i], bag.Choices[other]) = (bag.Choices[other], bag.Choices[i]);
            }
            if (bag.Choices.Count > 1 && bag.Choices[0] == bag.Previous)
                (bag.Choices[0], bag.Choices[1]) = (bag.Choices[1], bag.Choices[0]);
            bag.Next = 0;
        }
        bag.Previous = bag.Choices[bag.Next++];
        return bag.Previous;
    }
    public static Texture2D ItemIcon(int id) => id switch
    {
        1001 => GD.Load<Texture2D>("res://assets/user/icon/icon_soul.png"),
        1002 => GD.Load<Texture2D>("res://assets/user/icon/icon_coin.png"),
        1003 => GD.Load<Texture2D>("res://assets/user/item/bandage.PNG"),
        1004 => GD.Load<Texture2D>("res://assets/user/item/drug.png"),
        1005 => GD.Load<Texture2D>("res://assets/user/icon/icon_bullet.png"),
        1006 => GD.Load<Texture2D>("res://assets/user/item/antidote.png"),
        _ => null
    };
    public static SupplyKind Supply(int id) => id switch
    {
        1003 => SupplyKind.Bandage, 1004 => SupplyKind.Drug,
        1005 => SupplyKind.Bullet, 1006 => SupplyKind.Antidote,
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
}
