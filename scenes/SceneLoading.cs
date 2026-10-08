using Godot;
using System.Collections.Generic;

/// <summary>后台资源任务由树根持有；菜单提前退出也会收取任务结果，避免悬挂加载请求。</summary>
public partial class SceneLoading : Node
{
    private static SceneLoading _instance;
    private SceneTree _tree;
    public sealed class Ticket
    {
        public PackedScene Scene { get; internal set; }
        public bool Failed { get; internal set; }
        internal bool Pending;
    }

    private readonly Dictionary<string, Ticket> _tickets = new();

    public static SceneLoading For(SceneTree tree)
    {
        if (GodotObject.IsInstanceValid(_instance) && _instance._tree == tree) return _instance;
        var loader = tree.Root.GetNodeOrNull<SceneLoading>("SceneLoading");
        if (loader != null) return loader;
        loader = new SceneLoading { Name = "SceneLoading", _tree = tree, ProcessMode = ProcessModeEnum.Always };
        _instance = loader;
        // Main.Ready 可能仍处于 Root.AddChild 的递归初始化；延迟附加，避免父节点 busy。
        tree.Root.CallDeferred(Node.MethodName.AddChild, loader);
        return loader;
    }

    public Ticket Request(string path, bool retry = false)
    {
        if (_tickets.TryGetValue(path, out var existing) && !(retry && existing.Failed)) return existing;
        var ticket = new Ticket();
        _tickets[path] = ticket;
        if (!ResourceLoader.Exists(path, "PackedScene")) { ticket.Failed = true; return ticket; }
        var error = ResourceLoader.LoadThreadedRequest(path, "PackedScene", useSubThreads: true);
        ticket.Pending = error == Error.Ok;
        ticket.Failed = !ticket.Pending;
        return ticket;
    }

    public override void _Process(double delta)
    {
        foreach (var entry in _tickets)
        {
            var ticket = entry.Value;
            if (!ticket.Pending) continue;
            var status = ResourceLoader.LoadThreadedGetStatus(entry.Key);
            if (status == ResourceLoader.ThreadLoadStatus.InProgress) continue;
            ticket.Pending = false;
            // Get 只在 Loaded 后调用：点击和菜单帧都不会等待资源线程。
            if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                ticket.Scene = ResourceLoader.LoadThreadedGet(entry.Key) as PackedScene;
            ticket.Failed = ticket.Scene == null;
        }
    }

    public override void _ExitTree()
    {
        _tickets.Clear();
        if (_instance == this) _instance = null;
    }
}
