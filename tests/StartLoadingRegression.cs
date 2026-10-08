using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>实际 GUI 点击到首个游戏帧、预热隔离、加载失败和取消菜单生命周期。</summary>
public partial class StartLoadingRegression : Node
{
    private int _checks;
    private double _coldMilliseconds, _warmMilliseconds;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        GD.Print($"PASS {++_checks}: {message}");
    }
    private async Task Frames(int count = 1)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Click(Button button)
    {
        Vector2 point = button.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        Input.FlushBufferedEvents();
    }
    private int MusicCount(Node node)
    {
        int count = node is AudioStreamPlayer audio && audio.Bus == "Music" && audio.Playing ? 1 : 0;
        foreach (Node child in node.GetChildren()) count += MusicCount(child);
        return count;
    }
    private Main AddMenu(string path = "res://scenes/game.tscn")
    {
        var menu = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
        menu.GameplayScenePath = path;
        GetTree().Root.AddChild(menu);
        GetTree().CurrentScene = menu;
        return menu;
    }
    private async Task WaitPrepared(Main menu)
    {
        ulong start = Time.GetTicksMsec();
        while (!menu.GamePrepared && Time.GetTicksMsec() - start < 10000) await Frames();
        Check(menu.GamePrepared, "后台预读并离树实例化在限时内完成");
    }
    private async Task<double> WaitGameFrame(ulong clickTime)
    {
        ulong deadline = Time.GetTicksMsec() + 10000;
        while (!(GetTree().CurrentScene?.HasNode("player") ?? false) && Time.GetTicksMsec() < deadline) await Frames();
        Check(GetTree().CurrentScene?.HasNode("player") == true, "实际点击后进入完整游戏场景");
        await Frames();
        if (DisplayServer.GetName() != "headless")
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return (Time.GetTicksUsec() - clickTime) / 1000.0;
    }
    private async Task<MenuSwitchResult> Start(Main menu)
    {
        var music = menu.GetNode<AudioStreamPlayer>("bgm");
        var button = menu.GetNode<Button>("HUD/Start/start");
        int presses = 0;
        button.Pressed += () => presses++;
        ulong started = Time.GetTicksUsec();
        Click(button); Click(button); Click(button);
        Check(menu.IsStarting && button.Disabled && presses == 1, "真实开始按钮连点只接受一次并暂时禁用");
        Check(music.Playing && GetTree().CurrentScene == menu, "点击后的等待帧继续播放菜单 BGM");
        Check(menu.GetNode<Label>("HUD/LoadingHint").Visible, "只有等待进入时显示轻量加载提示");
        double elapsed = await WaitGameFrame(started);
        Check(GetTree().GetNodesInGroup("player").Count == 1, "场景交接只创建一局玩家");
        Check(!Stop.IsPaused && GetTree().CurrentScene.GetNode<AudioStreamPlayer>("bgm").Playing && MusicCount(GetTree().Root) == 1,
            "首个游戏帧正常行动并仅播放探索 BGM");
        return new MenuSwitchResult { Elapsed = elapsed, Scene = GetTree().CurrentScene };
    }
    private sealed class MenuSwitchResult { public double Elapsed; public Node Scene; }
    private async Task RemoveCurrent()
    {
        Node current = GetTree().CurrentScene;
        GetTree().CurrentScene = null;
        current?.QueueFree();
        await Frames(4);
    }
    private async Task RunBaseline()
    {
        // 同样的菜单美术与真实按钮，但使用修复前的同步加载交接；独立进程运行确保资源冷缓存。
        Node original = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate();
        var menu = new Node2D();
        Node hud = original.GetNode("HUD");
        original.RemoveChild(hud); menu.AddChild(hud); original.Free();
        GetTree().Root.AddChild(menu); GetTree().CurrentScene = menu;
        var music = ScreenMusic.Start(menu, "res://music/BGM_begin.ogg", "bgm");
        var start = menu.GetNode<Button>("HUD/Start/start");
        start.Pressed += () => { music.Stop(); GetTree().ChangeSceneToFile("res://scenes/game.tscn"); };
        await Frames();
        ulong clicked = Time.GetTicksUsec();
        Click(start);
        double elapsed = await WaitGameFrame(clicked);
        GD.Print($"START_TIMING baseline_cold_click_to_first_game_frame_ms={elapsed:F1} renderer={DisplayServer.GetName()}");
        await RemoveCurrent();
    }
    private async void Run()
    {
        int failure = 0;
        try
        {
            if (Array.Exists(OS.GetCmdlineUserArgs(), value => value == "--start-baseline")) await RunBaseline();
            else
            {
                var menu = AddMenu();
                Check(MusicCount(GetTree().Root) == 1 && !menu.GetNode<Label>("HUD/LoadingHint").Visible, "菜单正常显示且开始前不显示加载提示");
                Check(GetTree().GetNodesInGroup("player").Count == 0 && !Stop.IsPaused, "后台预加载不会激活玩家、物理或暂停锁");
                await Frames();
                var cold = await Start(menu); _coldMilliseconds = cold.Elapsed;
                await RemoveCurrent();

                menu = AddMenu();
                ulong warmReady = Time.GetTicksUsec();
                await WaitPrepared(menu);
                double preparedMilliseconds = (Time.GetTicksUsec() - warmReady) / 1000.0;
                Check(GetTree().GetNodesInGroup("player").Count == 0 && MusicCount(GetTree().Root) == 1 && !Stop.IsPaused,
                    "准备完成的离树游戏仍不提前运行世界或播放探索曲");
                Node unused = typeof(Main).GetField("_preparedGame", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(menu) as Node;
                Check(GodotObject.IsInstanceValid(unused) && !unused.IsInsideTree(), "预实例保持在场景树之外");
                await RemoveCurrent();
                Check(!GodotObject.IsInstanceValid(unused), "取消主菜单释放整棵未使用预实例");
                Check(MusicCount(GetTree().Root) == 0 && !Stop.IsPaused, "取消主菜单不残留音乐或暂停锁");

                menu = AddMenu(); await WaitPrepared(menu);
                var warm = await Start(menu); _warmMilliseconds = warm.Elapsed;
                Check(_warmMilliseconds < 1000, "预热后实际点击至首个游戏帧少于一秒");
                await RemoveCurrent();

                menu = AddMenu("res://scenes/no_such_game.tscn");
                await Frames();
                Click(menu.GetNode<Button>("HUD/Start/start"));
                await Frames(3);
                Check(GetTree().CurrentScene == menu && !menu.IsStarting && !menu.GetNode<Button>("HUD/Start/start").Disabled,
                    "加载失败保留菜单并重新开放开始按钮");
                Check(MusicCount(GetTree().Root) == 1 && menu.GetNode<Label>("HUD/LoadingHint").Text.Contains("再试"),
                    "加载失败继续播放菜单曲并提供重试提示");
                Click(menu.GetNode<Button>("HUD/Start/start")); await Frames(3);
                Check(GetTree().CurrentScene == menu && !menu.IsStarting, "重复失败重试不会崩溃或产生游戏实例");
                menu.GameplayScenePath = "res://scenes/game.tscn";
                var recovered = await Start(menu);
                Check(recovered.Scene.HasNode("player") && GetTree().GetNodesInGroup("player").Count == 1, "修复资源地址后再次点击可恢复进入单局游戏");
                await RemoveCurrent();
                GD.Print($"START_TIMING cold_click_to_first_game_frame_ms={_coldMilliseconds:F1} warm_click_to_first_game_frame_ms={_warmMilliseconds:F1} warm_menu_prepare_ms={preparedMilliseconds:F1} renderer={DisplayServer.GetName()}");
            }
        }
        catch (Exception error) { GD.PushError($"START LOADING FAILED: {error}"); failure = 1; }
        finally
        {
            if (GetTree().CurrentScene != this) await RemoveCurrent();
            GetTree().Root.GetNodeOrNull<SceneLoading>("SceneLoading")?.QueueFree();
            await Frames(4);
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames(2);
            GD.Print($"START LOADING RESULT: {_checks} passed, {failure} failed");
            GetTree().Quit(failure);
        }
    }
}
