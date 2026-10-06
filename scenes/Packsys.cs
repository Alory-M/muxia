using Godot;
using System.Collections.Generic;

/// <summary>
/// 背包界面,挂在 packsys.tscn 的根 Control 上。
///
/// 按 B 开关:开着的时候控件可交互,关掉之后既不可见也不吃鼠标事件。
/// 道具数量统一从 player/pack(Pack.cs)读,不在界面里另存一份;
/// 真正"用掉"道具的动作交给 player/clear(Clear.cs),这里只负责选和转发。
///
/// 选中流程:点格子 → item 显示那样道具的大图和名字 + EnsureButton 出现
///          → 点 EnsureButton 才真的用掉一份
/// </summary>
public partial class Packsys : Control
{
	// 开关背包的键。单独拎出来,以后改键不用翻代码
	private const string ToggleKey = "B";

	// 关背包的备用键。和 ToggleKey 分开放:它只负责关,不负责开
	private const string EscapeKey = "esc";

	// 背包里摆道具的格子,按从左到右的顺序排。数量>0 的道具就顺着往这里填
	private static readonly string[] SlotPaths = { "button11", "button12", "button13" };

	// 会出现在背包里的道具,顺序 = SupplyKind 的枚举顺序。
	// 子弹排在枚举第一位,但 bin/pack 下没给它准备图标,所以不参与显示
	private static readonly SupplyKind[] DisplayOrder =
	{
		SupplyKind.Drug,
		SupplyKind.Bandage,
		SupplyKind.Antidote,
	};

	// 格子图标的缩放。药品和绷带都是 750×750 的方图,共用一套;
	// 解毒剂是 920×1067 的非方图,得单独给一套,否则换到别的格子会被拉变形
	private static readonly Vector2 DefaultIconScale = new(0.09466665f, 0.09466665f);
	private static readonly Vector2 AntidoteIconScale = new(0.055f, 0.054357592f);

	// 格子(按下要用的按钮)、格子上的图标、格子上的数量文字,三个列表一一对应
	private readonly List<Button> _slots = new();
	private readonly List<Sprite2D> _slotIcons = new();
	private readonly List<Label> _slotCounters = new();

	// 道具 → 图标资源。只在 _Ready 里 Load 一次,之后换图只是换引用
	private readonly Dictionary<SupplyKind, Texture2D> _textures = new();

	// 每个格子当前摆的是哪样道具。空格子不在表里。
	// 按钮被按下时靠它反查"我这一格是什么",而不是靠下标或图标去猜
	private readonly Dictionary<Button, SupplyKind> _slotKind = new();

	private Pack _pack;

	// 真正扣数量、产生效果的是 player/clear 上的 Clear.cs,这里只负责把请求转过去
	private Clear _clear;

	// 开背包时的暂停开关(冻结玩家、停掉减益),细节都封在子节点 stop 里
	private Stop _stop;

	// 选中详情区:item 是那张大图,item 的子节点 itemname 是道具名,
	// EnsureButton 是按下去才真的用掉一份的那颗按钮。
	// 三个都藏起来,点格子才一起出现
	private Sprite2D _item;
	private Label _itemName;
	private Button _ensureButton;
	private bool _detailReady;

	// item 大图的基准缩放 = 场景里调好的那套(以药品为准),_Ready 时读进来。
	// 其余道具按"格子图标之间的比例"等比换算,格子和预览的大小关系就始终一致
	private Vector2 _previewBaseScale = Vector2.One;

	// 当前选中的是哪样道具。空 = 什么都没选,详情区收着
	private SupplyKind? _selected;

	// 开局强制收起。场景实例上本来就有 visible=false,这里再兜一次底,
	// 免得以后谁在检查器里改了覆盖值,背包一进游戏就敞着
	public override void _Ready()
	{
		// 背包是 HUD 下的实例,pack 和 clear 都挂在同级 player 下面
		_pack = GetNodeOrNull<Pack>("../../player/pack");
		if (_pack == null)
		{
			GD.PushWarning("Packsys: 找不到 ../../player/pack,拿不到道具数量,图标不会显示。");
		}

		_clear = GetNodeOrNull<Clear>("../../player/clear");
		if (_clear == null)
		{
			GD.PushWarning("Packsys: 找不到 ../../player/clear,点 EnsureButton 不会真的用掉道具。");
		}

		_stop = GetNodeOrNull<Stop>("stop");
		if (_stop == null)
		{
			GD.PushWarning("Packsys: 找不到子节点 stop,开背包时不会暂停任何东西。");
		}

		foreach (SupplyKind kind in DisplayOrder)
		{
			_textures[kind] = LoadIcon(kind);
		}

		// 按钮、图标、数量文字缺一不可:少了哪个这一格就放弃,免得三个列表错位
		foreach (string slotPath in SlotPaths)
		{
			Button slot = GetNodeOrNull<Button>(slotPath);
			Sprite2D icon = slot?.GetNodeOrNull<Sprite2D>("image");
			Label counter = slot?.GetNodeOrNull<Label>("counter");
			if (slot == null || icon == null || counter == null)
			{
				GD.PushWarning($"Packsys: {slotPath} 少了 image 或 counter 子节点,这一格跳过。");
				continue;
			}

			_slots.Add(slot);
			_slotIcons.Add(icon);
			_slotCounters.Add(counter);

			// 闭包捕获的是循环内这个 slot 变量,每一轮都是新的,不会串格
			slot.Pressed += () => OnSlotPressed(slot);
		}

		_item = GetNodeOrNull<Sprite2D>("item");
		// itemname 是根 Control 的子节点,不是 item 的 —— 挂在 item 下面的话
		// 会连图标的缩放一起继承,换个道具名字的字号和位置就跟着变
		_itemName = GetNodeOrNull<Label>("itemname");
		_ensureButton = GetNodeOrNull<Button>("EnsureButton");

		// 记下场景里给 item 调的 scale 当基准。以后在检查器里改药品预览的大小,
		// 其余道具会自动跟着等比变化,不用回来改代码
		if (_item != null)
		{
			_previewBaseScale = _item.Scale;
		}
		_detailReady = _item != null && _itemName != null && _ensureButton != null;
		if (_detailReady)
		{
			_ensureButton.Pressed += OnEnsurePressed;
		}
		else
		{
			GD.PushWarning("Packsys: item / itemname / EnsureButton 没找齐,点格子不会弹出道具详情。");
		}

		SetOpen(false);
	}

	// 和 Player / Clear 一样用 _UnhandledInput:按键是逐个事件投递的,不会漏掉连按
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(ToggleKey))
		{
			SetOpen(!Visible);
			return;
		}

		// Esc 只在背包开着时有效,等同于"再按一次 B"。关着的时候什么都不做——
		// 不然它就成了第二个打开键,以后别的界面想用 Esc 返回也会被它抢
		if (Visible && @event.IsActionPressed(EscapeKey))
		{
			SetOpen(false);
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
			// counter 也得跟着藏。它和 image 一样是 button 的子节点,
			// 而 button 本身没被藏(只是把自己的框画成透明的),藏 button 带不走它
			_slotCounters[i].Visible = filled;

			if (filled)
			{
				SupplyKind kind = available[i];
				_slotIcons[i].Texture = _textures[kind];
				// 三张图大小不一,每格都按道具重新定缩放,不能沿用上一张留在那儿的
				_slotIcons[i].Scale = ScaleFor(kind);

				// 数量直接读 pack,不在这里缓存副本
				_slotCounters[i].Text = _pack.GetCount(kind).ToString();

				// 让这个按钮记住它现在代表哪样道具
				_slotKind[_slots[i]] = kind;
			}
			else
			{
				_slotKind.Remove(_slots[i]);
			}

			// 空格子不吃点击,不然点空位也会弹详情
			_slots[i].MouseFilter = filled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		}
	}

	/// <summary>点了某个格子的图标:选中那样道具,把详情区亮出来</summary>
	private void OnSlotPressed(Button slot)
	{
		// 空格子不吃点击,真走到这儿说明表里没它,忽略
		if (!_slotKind.TryGetValue(slot, out SupplyKind kind))
		{
			return;
		}

		Select(kind);
	}

	/// <summary>
	/// 选中一样道具:大图、名字、EnsureButton 一起出现。
	/// 大图的缩放交给场景里调好的值,这里只换贴图和文字,不碰 scale
	/// </summary>
	private void Select(SupplyKind kind)
	{
		_selected = kind;

		if (!_detailReady)
		{
			return;
		}

		_item.Texture = _textures[kind];
		_item.Scale = PreviewScaleFor(kind);
		_itemName.Text = NameFor(kind);

		// 这三个必须一起开关。itemname 现在是根 Control 的子节点、不再是 item 的子节点,
		// 藏 item 已经带不走它了,漏一个就会出现"图没了字还在"
		_item.Visible = true;
		_itemName.Visible = true;
		_ensureButton.Visible = true;
	}

	/// <summary>收起详情区,回到"什么都没选"的状态</summary>
	private void HideDetail()
	{
		_selected = null;

		if (!_detailReady)
		{
			return;
		}

		// 和 Select 对着来,三个一起收
		_item.Visible = false;
		_itemName.Visible = false;
		_ensureButton.Visible = false;
	}

	/// <summary>点了 EnsureButton:真的用掉一份,然后重排列、刷新详情区</summary>
	private void OnEnsurePressed()
	{
		// 先取出来:下面 HideDetail 会把 _selected 清掉
		SupplyKind? kind = _selected;

		if (kind == null || _clear == null)
		{
			return;
		}

		// Clear 里这三个方法自己就会扣数量,而且"血满了/当前没中对应的减益"时会拒绝、不扣。
		// 所以这里千万别再扣一次,否则一份道具会凭空少两份
		switch (kind.Value)
		{
			case SupplyKind.Drug:
				_clear.UseDrug();
				break;
			case SupplyKind.Bandage:
				_clear.UseBandage();
				break;
			case SupplyKind.Antidote:
				_clear.UseAntidote();
				break;
		}

		// 数量变了,格子要重排(用光了就消失,后面的往前补)
		RefreshItems();

		// 还有货就保持选中(方便连点),用光了才收起——
		// 否则会留着一张"已经没有了的道具"的大图
		if (_pack != null && _pack.GetCount(kind.Value) > 0)
		{
			Select(kind.Value);
		}
		else
		{
			HideDetail();
		}
	}

	/// <summary>开/关背包。关的时候把详情区一起收掉,下次打开不该还挂着上次选的道具。
	/// 对外公开:Win 关别的窗口时要调这个方法而不是直接改 Visible ——
	/// 只改 Visible 会漏掉 MouseFilter 和 stop 的暂停</summary>
	public void SetOpen(bool open)
	{
		Visible = open;

		// 不可见的 Control 本来就点不到,这里显式切一下 MouseFilter:
		// 开着时挡住底下的世界点击和 HUD 按钮(模态),关掉时把事件让回去
		MouseFilter = open ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

		// 背包是模态的:开着的时候把角色和减益一起停住,不然玩家盯着背包还在掉血、还能乱跑
		if (_stop != null)
		{
			_stop.SetPaused(open);
		}

		if (open)
		{
			// 每次打开都按最新数量重排一遍,不沿用上次的摆放
			RefreshItems();
		}
		else
		{
			HideDetail();
		}
	}

	/// <summary>
	/// item 大图的缩放:以场景里调好的基准(药品)为准,再按格子图标的比例换算。
	/// 解毒剂的源图比另外两张大一截,直接套基准 scale 会顶出边框、压到名字上
	/// </summary>
	private Vector2 PreviewScaleFor(SupplyKind kind)
	{
		return _previewBaseScale * (ScaleFor(kind) / DefaultIconScale);
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

	/// <summary>详情区里显示的中文名</summary>
	private static string NameFor(SupplyKind kind)
	{
		return kind switch
		{
			SupplyKind.Drug => "药品",
			SupplyKind.Bandage => "绷带",
			SupplyKind.Antidote => "解毒剂",
			_ => "道具",
		};
	}
}
