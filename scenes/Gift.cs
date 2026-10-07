using Godot;
using System.Collections.Generic;
using GodotArray = Godot.Collections.Array;
using GodotDict = Godot.Collections.Dictionary;

/// <summary>
/// 掉落组件,挂在 treasure.tscn 的 gift 节点上(照 rotate / disappear 的组件写法)。
///
/// 开箱时由宿主 Treasure 调用 Drop():从 data/drop.json 里按 DropId 取一组候选,
/// 按 basewight 加权抽中**一行**,再照 data/item.json 把这份东西加到玩家身上。
///
/// 两张表都在 res://data/ 下,改掉落只改 json,不用动代码、不用重编译。
/// 表在第一次掉落时读进来,之后一直用这份缓存 —— 改了 json 要重开游戏才生效。
///
/// 掉落物写成 "物品ID,数量",例 "1002,100" = 100 个财宝;
/// item.json 的 location 写成 "节点路径:方法",例 "player/gold:Add"。
/// 路径第一段 player 按 "player" 组找(和 Store / State 一样不数斜杠),
/// 后面几段是它下面的子节点;方法固定收一个数量参数。
/// </summary>
public partial class Gift : Node
{
	/// <summary>用 drop.json 里哪一张掉落表。宝箱 2004,僵尸 2001-2003,棺材 2005</summary>
	[Export] public int DropId { get; set; } = 2004;

	private const string DropTablePath = "res://data/drop.json";
	private const string ItemTablePath = "res://data/item.json";

	// 两张表读一次就够。静态的,同一个进程里所有宝箱共用
	private static GodotArray _dropRows;
	private static GodotDict _itemRows;   // 物品ID → item.json 里那一整行

	/// <summary>结算一次掉落并发给玩家。由宿主在宝箱消失的那一刻调用</summary>
	public void Drop()
	{
		GodotDict picked = PickRow();
		if (picked == null)
		{
			return;
		}

		string rawItem = picked["item"].AsString();

		// "1002,100" → 物品 ID 和数量
		string[] parts = rawItem.Split(',');
		if (parts.Length < 2
			|| !int.TryParse(parts[0].Trim(), out int itemId)
			|| !int.TryParse(parts[1].Trim(), out int count)
			|| count <= 0)
		{
			GD.PushWarning($"Gift: 掉落表 ID {DropId} 里的 item \"{rawItem}\" 不是 \"物品ID,数量\" 的格式,这次不掉。");
			return;
		}

		GodotDict items = LoadItemTable();
		if (items == null)
		{
			return;
		}

		if (!items.ContainsKey(itemId))
		{
			GD.PushWarning($"Gift: item.json 里没有 ID {itemId},这次不掉。");
			return;
		}

		GodotDict item = items[itemId].AsGodotDictionary();
		string location = item["location"].AsString();
		string note = item.ContainsKey("note") ? item["note"].AsString() : "物品";

		if (GiveToPlayer(location, count))
		{
			GD.Print($"Gift: 掉落 {count} 个{note}(物品 {itemId}),已加到 {location}");
		}
	}

	/// <summary>
	/// 从 drop.json 里挑一行:先筛出 DropId 相同的,再按 basewight 加权抽。
	/// 权重是相对值,不用凑成 100 —— 2004 的四行是 200/150/100/50,
	/// 归一化后 40% / 30% / 20% / 10%
	/// </summary>
	private GodotDict PickRow()
	{
		GodotArray rows = LoadDropRows();
		if (rows == null)
		{
			return null;
		}

		var candidates = new List<GodotDict>();
		float total = 0f;

		foreach (Variant entry in rows)
		{
			if (entry.VariantType != Variant.Type.Dictionary)
			{
				continue;
			}

			GodotDict row = entry.AsGodotDictionary();
			if (!row.ContainsKey("ID") || !row.ContainsKey("item") || !row.ContainsKey("basewight"))
			{
				continue;
			}

			if (row["ID"].AsInt32() != DropId)
			{
				continue;
			}

			float weight = (float)row["basewight"].AsDouble();
			if (weight <= 0f)
			{
				// 权重为 0 的行永远抽不到,直接不进候选
				continue;
			}

			candidates.Add(row);
			total += weight;
		}

		if (candidates.Count == 0)
		{
			GD.PushWarning($"Gift: drop.json 里没有 ID {DropId} 的可用掉落行,这次不掉。");
			return null;
		}

		// 抽一个 [0, total) 的点,顺着减权重,减到谁归零就是谁。
		// 起点设在最后一行,万一浮点误差导致谁都没命中,也有个兜底
		float roll = GD.Randf() * total;
		GodotDict picked = candidates[^1];

		foreach (GodotDict row in candidates)
		{
			roll -= (float)row["basewight"].AsDouble();
			if (roll <= 0f)
			{
				picked = row;
				break;
			}
		}

		return picked;
	}

	/// <summary>把 location("player/gold:Add")拆成节点和方法,调过去</summary>
	private bool GiveToPlayer(string location, int count)
	{
		int colon = location.LastIndexOf(':');
		if (colon <= 0 || colon == location.Length - 1)
		{
			GD.PushWarning($"Gift: location \"{location}\" 不是 \"节点路径:方法\" 的格式,这次不掉。");
			return false;
		}

		string nodePath = location.Substring(0, colon);
		string method = location.Substring(colon + 1);

		Node target = ResolveNode(nodePath);
		if (target == null)
		{
			GD.PushWarning($"Gift: 找不到节点 {nodePath},这次不掉。");
			return false;
		}

		if (!target.HasMethod(method))
		{
			GD.PushWarning($"Gift: {nodePath}({target.GetType().Name})没有 {method} 方法,这次不掉。");
			return false;
		}

		target.Call(method, count);
		return true;
	}

	/// <summary>
	/// 把 "player/gold" 解析成节点。第一段是 player 就按 "player" 组找,
	/// 其余段当子节点往下走;第一段不是 player 就当作从当前场景根节点起的路径
	/// </summary>
	private Node ResolveNode(string nodePath)
	{
		string[] segments = nodePath.Split('/');
		int start = 0;
		Node current;

		if (segments[0] == "player")
		{
			current = GetTree().GetFirstNodeInGroup("player");
			start = 1;

			if (current == null)
			{
				return null;
			}
		}
		else
		{
			current = GetTree().CurrentScene;
		}

		for (int i = start; i < segments.Length && current != null; i++)
		{
			if (segments[i].Length == 0)
			{
				continue;
			}

			current = current.GetNodeOrNull(segments[i]);
		}

		return current;
	}

	// ---- 两张表的读取。都是读一次缓存起来 ----

	private static GodotArray LoadDropRows()
	{
		if (_dropRows != null)
		{
			return _dropRows;
		}

		Variant parsed = ParseJsonFile(DropTablePath);
		if (parsed.VariantType != Variant.Type.Array)
		{
			return null;
		}

		_dropRows = parsed.AsGodotArray();
		return _dropRows;
	}

	/// <summary>把 item.json 读成 "物品ID → 那一整行" 的表</summary>
	private static GodotDict LoadItemTable()
	{
		if (_itemRows != null)
		{
			return _itemRows;
		}

		Variant parsed = ParseJsonFile(ItemTablePath);
		if (parsed.VariantType != Variant.Type.Array)
		{
			return null;
		}

		var table = new GodotDict();

		foreach (Variant entry in parsed.AsGodotArray())
		{
			if (entry.VariantType != Variant.Type.Dictionary)
			{
				continue;
			}

			GodotDict row = entry.AsGodotDictionary();
			if (!row.ContainsKey("ID") || !row.ContainsKey("location"))
			{
				GD.PushWarning("Gift: item.json 有一行缺 ID 或 location,跳过。");
				continue;
			}

			table[row["ID"].AsInt32()] = row;
		}

		_itemRows = table;
		return _itemRows;
	}

	private static Variant ParseJsonFile(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning($"Gift: 找不到 {path},掉落不生效。");
			return default;
		}

		// res:// 下的文件直接按文本读,json 里有中文,别当二进制处理
		Variant parsed = Json.ParseString(FileAccess.GetFileAsString(path));
		if (parsed.VariantType == Variant.Type.Nil)
		{
			GD.PushWarning($"Gift: {path} 解析失败,检查格式(缺逗号、单个反斜杠最容易出错)。");
		}

		return parsed;
	}
}
