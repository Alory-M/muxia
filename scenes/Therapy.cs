using Godot;

public partial class Therapy : Button
{
	public float MaxHpLength { get; set; } = 555f;
	public float MaxHp { get; set; } = 100f;
	[Export] public float Healing { get; set; } = 10f;

	private ColorRect _redBlock;
	private float _width;
	private Player _player;

	public override void _Ready()
	{
		// 在 _Ready 里获取节点（此时节点已进入场景树）
		// 路径根据你的实际场景结构调整
		_redBlock = GetNode<ColorRect>("../ColorRect");
		_player =GetNode<Player>("../../player");

		if (_redBlock != null)
		{
			_width = _redBlock.Size.X;
			GD.Print($"RedBlock 宽度: {_width}");
		}
		else
		{
			GD.PrintErr("找不到 RedBlock，检查路径！");
		}

		// 订阅自己的 Pressed 信号
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		float currentWidth = _redBlock.Size.X;
		if (currentWidth <MaxHpLength && _player._drugCounter>0)
		{
			float Dohealing=(MaxHpLength/MaxHp)*Healing;
			_player._drugCounter-=1;
			if((currentWidth+Dohealing)<MaxHpLength)
			{
				_redBlock.Size = new Vector2(currentWidth + Dohealing, _redBlock.Size.Y);
			}
			else
			{
				_redBlock.Size = new Vector2(MaxHpLength, _redBlock.Size.Y);
			}
		}
	}
}
