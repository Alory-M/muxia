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
    public static Texture2D ItemIcon(int id) => id switch
    {
        1001 => GD.Load<Texture2D>("res://bin/item/item/item/icon_soul.png"),
        1002 => GD.Load<Texture2D>("res://bin/item/item/item/icon_coin.png"),
        1003 => GD.Load<Texture2D>("res://bin/pack/绷带.PNG"),
        1004 => GD.Load<Texture2D>("res://bin/pack/药品.png"),
        1005 => GD.Load<Texture2D>("res://bin/pack/子弹.png"),
        1006 => GD.Load<Texture2D>("res://bin/pack/解毒剂.png"),
        _ => null
    };
    public static SupplyKind Supply(int id) => id switch
    {
        1003 => SupplyKind.Bandage, 1004 => SupplyKind.Drug,
        1005 => SupplyKind.Bullet, 1006 => SupplyKind.Antidote,
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
}
