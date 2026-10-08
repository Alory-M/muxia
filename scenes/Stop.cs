using Godot;
using System.Collections.Generic;

/// <summary>每个窗口持有一把暂停锁，重复开关和场景退出都不会留下暂停状态。</summary>
public partial class Stop : Node
{
    private static readonly HashSet<Stop> Owners = new();
    private static readonly Dictionary<Node, ProcessModeEnum> OriginalModes = new();
    private bool _paused;
    public static bool IsPaused => Owners.Count > 0;
    public override void _Ready() { }
    public void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        if (paused) Owners.Add(this); else Owners.Remove(this);
        Refresh();
    }
    public override void _ExitTree()
    {
        if (!_paused) return;
        _paused = false;
        Owners.Remove(this);
        Refresh();
    }
    private void Refresh()
    {
        if (IsPaused)
        {
            foreach (string group in new[] { "player", "zombie", "bullet", "world_event" })
                foreach (Node node in GetTree().GetNodesInGroup(group))
                {
                    if (!OriginalModes.ContainsKey(node)) OriginalModes[node] = node.ProcessMode;
                    node.ProcessMode = ProcessModeEnum.Disabled;
                }
        }
        else
        {
            foreach (var entry in OriginalModes)
                if (GodotObject.IsInstanceValid(entry.Key)) entry.Key.ProcessMode = entry.Value;
            OriginalModes.Clear();
        }
    }
    // 新生成的实体也遵循暂停锁；恢复时保留它原本的处理模式。
    public static void RegisterWorldNode(Node node)
    {
        if (!IsPaused) return;
        if (!OriginalModes.ContainsKey(node)) OriginalModes[node] = node.ProcessMode;
        node.ProcessMode = ProcessModeEnum.Disabled;
    }
}
