using Godot;

/// <summary>
/// 商人:玩家进检测范围(im)时把"F开启商店"提示亮出来,离开就收起;
/// 在范围内按 F 开关商店界面。
///
/// 检测用的是 Area2D 自己的 body_entered / body_exited —— im 是挂在 Area2D
/// 本体上的碰撞形状,所以信号也接在本节点上。旁边那个 solid/re 是挡人的物理实体,
/// 不参与这里的检测。
/// </summary>
public partial class Businessman : Area2D
{
	// 把商店界面(store.tscn 的根节点)加进这个组,这里按组名去找。
	// 不写死 ../HUD/store —— 场景层级已经改过好几次了,写死路径每改一次都要跟着动
	private const string StoreUiGroup = "store_ui";

	private Button _getButton;
	private CanvasItem _storeUi;
	private bool _playerInRange;

	public override void _Ready()
	{
		_getButton = GetNodeOrNull<Button>("get");
		if (_getButton == null)
		{
			GD.PushWarning("Businessman: 找不到 get 按钮,提示不会显示。");
		}

		_storeUi = GetTree().GetFirstNodeInGroup(StoreUiGroup) as CanvasItem;
		if (_storeUi == null)
		{
			GD.PushWarning($"Businessman: 找不到 {StoreUiGroup} 组的商店界面,按 F 打不开商店。");
		}

		// 开局先收起,免得场景里的初始状态被人改过
		SetGetVisible(false);

		// 玩家进入 / 离开碰撞范围
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	// 用 _UnhandledInput 而不是 _Process + IsActionJustPressed:
	// 按键是逐个事件投递的,不会漏掉连按(Player / Clear / Packsys 都是这个写法)
	public override void _UnhandledInput(InputEvent @event)
	{
		if (_storeUi == null || !@event.IsActionPressed("interact"))
		{
			return;
		}

		// 开店要求站在旁边;关店不要求。
		// 因为开店会把玩家冻住(Stop 里设 ProcessMode.Disabled),而一冻上,
		// 玩家就从 Area2D 的重叠列表里掉出去了 —— 商人收到 body_exited、
		// _playerInRange 变 false。这时候要是还要求"在范围内",那"再按一次 F 关掉"
		// 就永远按不动了(玩家被冻着,也走不回来)
		if (!_storeUi.Visible && !_playerInRange)
		{
			return;
		}

		ToggleStore();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			_playerInRange = true;
			SetGetVisible(true);
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			_playerInRange = false;
			SetGetVisible(false);
		}
	}

	/// <summary>开关商店界面。做出"开关"而不是只开,是为了现在就能关掉 ——
	/// 关闭按钮还没接,只开不关的话点开就关不上了</summary>
	private void ToggleStore()
	{
		_storeUi.Visible = !_storeUi.Visible;
	}

	/// <summary>
	/// 显示 / 收起提示。
	/// 顺便切 Disabled:隐藏的按钮本来就点不到,但显式关掉能让"现在能不能用"更明确。
	/// </summary>
	private void SetGetVisible(bool visible)
	{
		if (_getButton == null)
		{
			return;
		}

		_getButton.Visible = visible;
		_getButton.Disabled = !visible;
	}
}
