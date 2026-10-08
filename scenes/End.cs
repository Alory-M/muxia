using Godot;
using System;

/// <summary>
/// 通关界面,挂在 end.tscn 的根节点上(在 game.tscn 里是 HUD/end)。
///
/// 三个按钮各管一件事:
///   continue(继续探索)   —— 开始全新一局
///   tomain(返回主菜单)   —— 换回开始界面 main.tscn
///   exit(退出游戏)       —— 退出程序
///
/// 界面什么时候出现不归这里管 —— 那是 Endarea 碰到玩家时把本节点设成可见的。
/// 这里只管界面开着的时候点按钮。
/// </summary>
public partial class End : Node2D
{
	// 和 Main.GameScenePath 一样拎出来,以后改名不用翻代码
	private const string MainScenePath = "res://scenes/main.tscn";

	// 通关音效播放器:界面显示(玩家到达终点)时播放一次 win.wav
	private AudioStreamPlayer _winPlayer;
    private AudioStreamPlayer _music;
    private AudioStreamPlayer _gameMusic;
    private bool _resumeGameMusic;
    private float _gameMusicPosition;
    private Stop _stop;
    private Label _score;
    public int FinalScore { get; private set; }
    public void ShowResults(Player player)
    {
        if (Visible || player.hp <= 0) return;
        foreach (Node ui in GetTree().GetNodesInGroup("modal_ui"))
        {
            if (ui == this) continue;
            if (ui.HasMethod("SetOpen")) ui.Call("SetOpen", false);
            else ui.GetNodeOrNull<Store>("store")?.SetOpen(false);
        }
        FinalScore = player.GetNode<Gold>("gold").Amount;
        var hud = GetTree().CurrentScene.GetNode<ExpeditionHud>("ExpeditionHud");
        int seconds = (int)hud.ElapsedSeconds;
        _score.Text = $"成功带出财宝：{FinalScore}　·　得分：{FinalScore}\n探索时间 {seconds / 60:00}:{seconds % 60:00}";
        SetOpen(true);
    }
    public void SetOpen(bool open) { Visible = open; _stop?.SetPaused(open); }

	public override void _Ready()
	{
		ZIndex = 100;
        // 三个按钮都是本节点的直接子节点,和背景那张 victory new 图同级
		Hook("continue", OnContinue);
		Hook("tomain", OnToMain);
		Hook("exit", OnExit);

		_stop = new Stop(); AddChild(_stop);
        _score = UiKit.Label("", 20); _score.Theme = UiKit.Theme(); AddChild(_score);
        _score.Position = new Vector2(300, 270); _score.Size = new Vector2(555, 66);
        _score.HorizontalAlignment = HorizontalAlignment.Center;
        SetupWinAudio();
        AudioSettings.Route(this);
		// 界面从隐藏变成显示就是"通关"的瞬间,靠这个信号触发 win 音效
		VisibilityChanged += OnVisibilityChanged;
		if (Visible) OnVisibilityChanged();
	}

    public override void _ExitTree()
    {
        ScreenMusic.Release(_music);
        ScreenMusic.Release(_winPlayer);
    }

	/// <summary>创建通关音效播放器并加载 win.wav(只播一次,不循环)</summary>
	private void SetupWinAudio()
	{
		_winPlayer = new AudioStreamPlayer();
		AddChild(_winPlayer);

		AudioStream stream = GD.Load<AudioStream>("res://music/win.wav");
		if (stream == null)
		{
			GD.PushWarning("End: 找不到音频 res://music/win.wav");
			return;
		}
		_winPlayer.Stream = stream;
	}

	/// <summary>界面从隐藏变成显示的那一瞬间,播放通关音效</summary>
	private void OnVisibilityChanged()
	{
		if (Visible)
		{
			_gameMusic = GetTree().CurrentScene?.GetNodeOrNull<AudioStreamPlayer>("bgm");
            _resumeGameMusic = GodotObject.IsInstanceValid(_gameMusic) && _gameMusic.Playing;
            if (_resumeGameMusic) _gameMusicPosition = _gameMusic.GetPlaybackPosition();
            _music = ScreenMusic.Start(this, "res://music/BGM_victory.wav");
			PlayWin();
		}
        else
        {
            _music?.Stop();
            _winPlayer?.Stop();
            if (_resumeGameMusic && GodotObject.IsInstanceValid(_gameMusic)) _gameMusic.Play(_gameMusicPosition);
            _resumeGameMusic = false;
        }
	}

	/// <summary>播放 win 音效;音频没加载到就静默跳过</summary>
	private void PlayWin()
	{
		if (_winPlayer != null && _winPlayer.Stream != null)
		{
			_winPlayer.Play();
		}
	}

	/// <summary>
	/// 接一个按钮,找不到只警告不报错 —— 少一个按钮不该让另外两个也失灵。
	/// 注意按钮名 continue 是 C# 关键字,不能拿来当变量名,所以统一走这个函数
	/// </summary>
	private void Hook(string buttonName, Action handler)
	{
		// 和项目里其它按钮一致:接 Pressed,一次点击只触发一次
		if (GetNodeOrNull<Button>(buttonName) is Button button)
		{
			button.Pressed += handler;
			return;
		}

		GD.PushWarning($"End: 找不到按钮 {buttonName},这个按钮点了不会有反应。");
	}

	/// <summary>继续探索:关闭结算界面并重新加载游戏</summary>
	private void OnContinue()
	{
		SetOpen(false);
        ScreenMusic.StopAll(GetTree().CurrentScene);
        GetTree().ChangeSceneToFile("res://scenes/game.tscn");
	}

	/// <summary>返回主菜单。ChangeSceneToFile 会先把本场景整个释放掉,不用自己 QueueFree</summary>
	private void OnToMain()
	{
		SetOpen(false);
		ScreenMusic.StopAll(GetTree().CurrentScene);
		GetTree().ChangeSceneToFile(MainScenePath);
	}

	/// <summary>退出游戏</summary>
	private void OnExit()
	{
		GetTree().Quit();
	}
}
