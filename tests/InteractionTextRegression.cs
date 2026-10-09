using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>验证随机交互文案完全来自 lang 表，且仅在新对话开启时改变。</summary>
public partial class InteractionTextRegression : Node
{
    private int _checks;
    private Node2D _game;
    private Player _player;
    private InteractionController _dialogue;
    private Label _dialogueLabel;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool result, string message)
    {
        if (!result) throw new InvalidOperationException(message);
        GD.Print($"PASS {++_checks}: {message}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private static HashSet<string> Texts(string prefix)
    {
        var result = new HashSet<string>();
        foreach (var row in GameData.Table("lang").EnumerateArray())
        {
            string key = row.GetProperty("key").GetString();
            if (key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(key[prefix.Length..], out _))
                result.Add(row.GetProperty("text").GetString());
        }
        return result;
    }
    private static PanelContainer FindPanel(Node node)
    {
        if (node is PanelContainer panel) return panel;
        foreach (Node child in node.GetChildren())
        {
            var found = FindPanel(child);
            if (found != null) return found;
        }
        return null;
    }
    private void CheckCategory(string prefix, int count)
    {
        var expected = Texts(prefix);
        Check(expected.Count == count, $"{prefix} 分类保留 lang 表全部 {count} 条原文");
        var seen = new HashSet<string>();
        string previous = null;
        for (int i = 0; i < count * 3; i++)
        {
            string text = GameData.RandomText(prefix);
            if (!expected.Contains(text) || text == previous)
                throw new InvalidOperationException($"{prefix} 取到了错误分类、非原文或连续重复文案");
            seen.Add(text); previous = text;
        }
        Check(seen.SetEquals(expected), $"{prefix} 洗牌覆盖全部原文，跨轮次不立即重复");
    }
    private async Task CheckDialogue(Node2D target, string prefix)
    {
        var item = (IWorldInteractable)target;
        var expected = Texts(prefix);
        _player.GlobalPosition = target.GlobalPosition + new Vector2(75, 0);
        string previous = null;
        for (int i = 0; i < 3; i++)
        {
            _dialogue.OpenDialogue(target);
            Check(_dialogue.IsDialogOpen && Stop.IsPaused, $"{prefix} 第 {i + 1} 次交互打开并暂停");
            string fullText = _dialogueLabel.Text;
            string chosen = fullText[(fullText.IndexOf('\n') + 1)..];
            Check(expected.Contains(chosen) && chosen != previous, $"{prefix} 取消后重开从对应 lang 原文重新选择");
            string stableDefault = item.InteractionText;
            for (int frame = 0; frame < 4; frame++)
            {
                _dialogue._Process(0.1);
                _ = item.InteractionText;
                await Frames(1);
            }
            Check(_dialogueLabel.Text == fullText && item.InteractionText == stableDefault,
                $"{prefix} 对话打开后文案稳定，提示刷新不触发重新随机");
            _dialogue.CloseDialogue();
            Check(!Stop.IsPaused && item.CanInteract, $"{prefix} 取消只释放暂停，不执行开箱、开棺或交易");
            previous = chosen;
        }
    }
    private async void Run()
    {
        try
        {
            CheckCategory("text_box", 5);
            CheckCategory("text_coffin", 10);
            CheckCategory("text_mo", 10);
            CheckCategory("text_busin", 5);
            Check(GameData.RandomText("missing_dialogue_", "text_no") == GameData.Text("text_no"),
                "缺失随机分类时使用指定 lang 文案作为后备");
            Check(GameData.RandomText("missing_dialogue_without_fallback_") == "missing_dialogue_without_fallback_1",
                "缺失分类和后备文案时保留可识别的表键");
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game; await Frames(3);
            _player = _game.GetNode<Player>("player");
            _dialogue = _game.GetNode<InteractionController>("Interactions");
            _dialogueLabel = FindPanel(_dialogue).GetChild<VBoxContainer>(0).GetChild<Label>(0);
            await CheckDialogue(_game.GetNode<Treasure>("Treasure2"), "text_box");
            var coffin = _game.GetNode<Coffin>("coffin");
            _player.GlobalPosition = coffin.GlobalPosition + new Vector2(150, 0);
            await Frames();
            _dialogue.OpenDialogue(coffin);
            Check(!coffin.CanInteract && !_dialogue.IsDialogOpen && !Stop.IsPaused,
                "封闭棺材不再显示手动开棺对话；原开棺文本分类仍完整保留");
            _player.GlobalPosition = coffin.GlobalPosition + new Vector2(90, 0);
            await Frames(3);
            Check(coffin.Status == Coffin.CoffinState.Fighting, "靠近自动出棺后等待战斗结束，不使用 F 开棺");
            // 此测试只关注文案状态切换，直接模拟守卫死亡信号，不依赖战斗动画时长。
            foreach (Node child in coffin.GetChildren())
                if (child is Zombie guardian) guardian.EmitSignal(Zombie.SignalName.Died);
            Check(coffin.Status == Coffin.CoffinState.Unlocked, "守卫死亡后交互分类切换为摸棺");
            await CheckDialogue(coffin, "text_mo");
            Businessman merchant = null;
            foreach (Node node in GetTree().GetNodesInGroup("interactable"))
                if (node is Businessman found) { merchant = found; break; }
            Check(merchant != null, "真实游戏场景中存在阴掌柜交互实体");
            await CheckDialogue(merchant, "text_busin");
            var chest = _game.GetNode<Treasure>("Treasure2");
            chest.Interact(_player);
            Check(!chest.CanInteract && chest.GetNode<AnimatedSprite2D>("AnimatedSprite2D").Animation == "open",
                "开箱后停止交互并保持打开的箱体图像");
            var gold = _player.GetNode<Gold>("gold");
            var pack = _player.GetNode<Pack>("pack");
            int awardedGold = gold.Amount, awardedBullets = pack.GetReserveBullet();
            await ToSignal(GetTree().CreateTimer(1.35), SceneTreeTimer.SignalName.Timeout);
            Check(GodotObject.IsInstanceValid(chest) && !chest.IsQueuedForDeletion() &&
                chest.GetNode<StaticBody2D>("SolidBody").CollisionLayer == 1 &&
                !chest.GetNode<CollisionShape2D>("SolidBody/CollisionShape2D").Disabled,
                "开箱动画结束后箱体继续存在，实体碰撞保持启用");
            chest.Interact(_player);
            Check(gold.Amount == awardedGold && pack.GetReserveBullet() == awardedBullets,
                "已经打开的宝箱不会再次发放财宝和补给");
            _game.QueueFree(); await Frames(4);
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            Check(!Stop.IsPaused, "退出随机文案测试场景释放全部暂停锁");
            GD.Print($"INTERACTION TEXT RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception ex)
        { GD.PushError($"INTERACTION TEXT FAILED after {_checks} passes: {ex}"); GetTree().Quit(1); }
    }
}
