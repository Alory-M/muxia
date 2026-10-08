using Godot;
using System;

/// <summary>在菜单期间预读并离树准备游戏，点击开始时只进行场景交接。</summary>
public partial class Main : Node2D
{
    public string GameplayScenePath { get; set; } = "res://scenes/game.tscn";
    public bool GamePrepared => GodotObject.IsInstanceValid(_preparedGame);
    public bool IsStarting => _starting;

    private bool _starting, _switchQueued, _preparationFailed;
    private AudioStreamPlayer _music;
    private Button _start;
    private Label _loading;
    private SceneLoading _loader;
    private SceneLoading.Ticket _ticket;
    private Node _preparedGame;

    public override void _EnterTree() => AudioSettings.Ensure();

    public override void _Ready()
    {
        AudioSettings.Route(this);
        _music = ScreenMusic.Start(this, "res://music/BGM_begin.ogg", "bgm");
        _start = GetNodeOrNull<Button>("HUD/Start/start");
        if (_start == null)
        {
            GD.PushWarning("Main: 找不到 HUD/Start/start 按钮。");
            return;
        }
        _start.Pressed += OnStartPressed;
        var hud = GetNode("HUD");
        var hint = UiKit.Label("WASD 移动 · F 交互 · J/左键 攻击 · Shift/右键 冲刺\nB 背包 · M 地图 · R 换弹 · E 药品 · Q 绷带 · Z 解毒", 16);
        hint.Theme = UiKit.Theme();
        hud.AddChild(hint);
        hint.Position = new Vector2(270, 560);
        hint.Size = new Vector2(660, 70);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        _loading = UiKit.Label("正在进入古墓…", 16);
        _loading.Name = "LoadingHint";
        _loading.Theme = hint.Theme;
        _loading.Position = new Vector2(350, 338);
        _loading.Size = new Vector2(430, 38);
        _loading.Visible = false;
        hud.AddChild(_loading);

        _loader = SceneLoading.For(GetTree());
        _ticket = _loader.Request(GameplayScenePath);
    }

    public override void _Process(double delta)
    {
        if (_ticket == null || GamePrepared || _preparationFailed) return;
        if (_ticket.Failed)
        {
            if (_starting) ShowLoadFailure();
            return;
        }
        if (_ticket.Scene == null) return;

        try
        {
            // 离树实例没有 Ready、碰撞、暂停锁或自动播放；切换前不激活游戏。
            _preparedGame = _ticket.Scene.Instantiate();
            if (!GamePrepared) { _preparationFailed = true; ShowLoadFailure(); return; }
            if (_starting) QueueGameSwitch();
        }
        catch (Exception error)
        {
            GD.PushWarning($"Main: 无法准备游戏场景：{error.Message}");
            _preparationFailed = true;
            ShowLoadFailure();
        }
    }

    public override void _ExitTree()
    {
        ScreenMusic.Release(_music);
        // 玩家从菜单直接退出时释放未交给 SceneTree 的离树实例。
        if (GodotObject.IsInstanceValid(_preparedGame)) _preparedGame.Free();
        _preparedGame = null;
        _ticket = null;
    }

    private void OnStartPressed()
    {
        if (_starting) return;
        _starting = true;
        _start.Disabled = true;
        _loading.Text = "正在进入古墓…";
        _loading.Visible = true;
        // 冷启动时继续播放菜单曲，并让菜单正常渲染到场景准备完毕。
        if (_ticket?.Failed == true) _ticket = _loader.Request(GameplayScenePath, retry: true);
        _preparationFailed = false;
        if (GamePrepared) QueueGameSwitch();
    }

    private void QueueGameSwitch()
    {
        if (_switchQueued) return;
        _switchQueued = true;
        Callable.From(EnterGame).CallDeferred();
    }

    private void EnterGame()
    {
        if (!IsInsideTree() || GetTree().CurrentScene != this || !GamePrepared) return;
        var game = _preparedGame;
        // ChangeSceneToNode 会立刻触发本菜单 ExitTree；先转移所有权避免释放新场景。
        _preparedGame = null;
        Error result = GetTree().ChangeSceneToNode(game);
        if (result == Error.Ok) return;
        _preparedGame = game;
        ShowLoadFailure();
    }

    private void ShowLoadFailure()
    {
        _starting = false;
        _switchQueued = false;
        _start.Disabled = false;
        _loading.Text = "暂时无法进入古墓，请再试一次";
        _loading.Visible = true;
    }
}
