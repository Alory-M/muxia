using Godot;
using System.Collections.Generic;

/// <summary>
/// 背包界面,挂在 packsys.tscn 的根 Control 上。
///
/// 按 B 开关:开着的时候控件可交互,关掉之后既不可见也不吃鼠标事件。
/// 道具数量统一从 player/pack(Pack.cs)读,不在界面里另存一份。
/// </summary>
public partial class Packsys : Control
{
	// 开关背包的键。单独拎出来,以后改键不用翻代码
	private const string ToggleKey = "B";

	// 背包里摆道具的格子,按从左到右的顺序排。数量>0 的道具就顺着往这里填
	private static readonly string[] SlotPaths = { "button11", "button12", "button13" };

	// 会出现在背包里的道具,顺序 = SupplyKind 的枚举顺序。
	// 子弹排在枚举第一位,但 Bin/pack 下没给它准备图标,所以不参与显示
	private static readonly SupplyKind[] DisplayOrder =
	{
		SupplyKind.Drug,
		SupplyKind.Bandage,
		SupplyKind.Antidote,
	};

	// 图标缩放。药品和绷带都是 750×750 的方图,共用一套;
	// 解毒剂是 920×1067 的非方图,得单独给一套,否则换到别的格子会被拉变形
	private static readonly Vector2 DefaultIconScale = new(0.09466665f, 0.09466665f);
	private static readonly Vector2 AntidoteIconScale = new(0.055f, 0.054357592f);

	// 格子(按下要用的按钮)和格子上的图标,两个列表一一对应
	private readonly List<Button> _slots = new();
	private readonly List<Sprite2D> _slotIcons = new();

	// 道具 → 图标资源。只在 _Ready 里 Load 一次,之后换图只是换引用
	private readonly Dictionary<SupplyKind, Texture2D> _textures = new();

	// 每个格子当前摆的是哪样道具。空格子不在表里。
	// 按钮被按下时靠它反查"我这一格是什么",而不是靠下标或图标去猜
	private readonly Dictionary<Button, SupplyKind> _slotKind = new();

	private Pack _pack;

	// 开局强制收起。场景实例上本来就有 visible=false,这里再兜一次底,
	// 免得以后谁在检查器里改了覆盖值,背包一进游戏就敞着
	public override void _Ready()
	{
		// 背包是 HUD 下的实例,pack 挂在同级 player 下面
		_pack = GetNodeOrNull<Pack>("../../player/pack");
		if (_pack == null)
		{
			GD.PushWarning("Packsys: 找不到 ../../player/pack,拿不到道具数量,图标不会显示。");
		}

		foreach (SupplyKind kind in DisplayOrder)
		{
			_textures[kind] = LoadIcon(kind);
		}

		// 按钮和图标缺一不可:少了图标就没法显示,这一格直接放弃,免得两个列表错位
		foreach (string slotPath in SlotPaths)
		{
			Button slot = GetNodeOrNull<Button>(slotPath);
			Sprite2D icon = slot?.GetNodeOrNull<Sprite2D>("image");
			if (slot == null || icon == null)
			{
				GD.PushWarning($"Packsys: {slotPath} 或它的 image 子节点找不到,这一格跳过。");
				continue;
			}

			_slots.Add(slot);
			_slotIcons.Add(icon);
		}

		SetOpen(false);
	}

	// 和 Player / Clear 一样用 _UnhandledInput:按键是逐个事件投递的,不会漏掉连按
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(ToggleKey))
		{
			SetOpen(!Visible);
		}
	}

	/// <summary>
	/// 按当前数量重排图标:还有货的道具按 SupplyKind 顺序,从第一个格子起依次占格,
	/// 剩下的格子清空。所以数量归零的道具会消失,后面的自动往前补。
	/// 开关背包、用掉道具之后都要调一次。
	/// </summary>
	public void RefreshItems()
	{
		if (_pack == null)
		{
			return;
		}

		// 先把"还有货"的挑出来,挑的顺序就是枚举顺序
		List<SupplyKind> available = new();
		foreach (SupplyKind kind in DisplayOrder)
		{
			if (_pack.GetCount(kind) > 0)
			{
				available.Add(kind);
			}
		}

		for (int i = 0; i < _slots.Count; i++)
		{
			bool filled = i < available.Count;

			_slotIcons[i].Visible = filled;
			if (filled)
			{
				SupplyKind kind = available[i];
				_slotIcons[i].Texture = _textures[kind];
				// 三张图大小不一,每格都按道具重新定缩放,不能沿用上一张留在那儿的
				_slotIcons[i].Scale = ScaleFor(kind);

				// 让这个按钮记住它现在代表哪样道具
				_slotKind[_slots[i]] = kind;
			}
			else
			{
				_slotKind.Remove(_slots[i]);
			}

			// 空格子不吃点击,不然点空位也会弹确认框
			_slots[i].MouseFilter = filled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		}
	}

	/// <summary>开/关背包。关的时候顺手把里面所有展开的东西一起收掉,不留半开状态</summary>
	private void SetOpen(bool open)
	{
		Visible = open;

		// 不可见的 Control 本来就点不到,这里显式切一下 MouseFilter:
		// 开着时挡住底下的世界点击和 HUD 按钮(模态),关掉时把事件让回去
		MouseFilter = open ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

		if (open)
		{
			// 每次打开都按最新数量重排一遍,不沿用上次的摆放
			RefreshItems();
		}
	}

	/// <summary>道具图标的缩放。解毒剂的源图尺寸和另外两张不一样,单独一套</summary>
	private static Vector2 ScaleFor(SupplyKind kind)
	{
		return kind == SupplyKind.Antidote ? AntidoteIconScale : DefaultIconScale;
	}

	/// <summary>道具对应的图标。注意绷带的文件名是大写 .PNG,路径写错会静默变成空白格</summary>
	private static Texture2D LoadIcon(SupplyKind kind)
	{
		return kind switch
		{
			SupplyKind.Drug => GD.Load<Texture2D>("res://bin/pack/药品.png"),
			SupplyKind.Bandage => GD.Load<Texture2D>("res://bin/pack/绷带.PNG"),
			SupplyKind.Antidote => GD.Load<Texture2D>("res://bin/pack/解毒剂.png"),
			_ => null,
		};
	}
}
