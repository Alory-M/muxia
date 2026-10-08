using Godot;

/// <summary>快捷键和背包共用的消耗品规则；只有成功产生效果时才扣一份。</summary>
public partial class Clear : Node
{
    private Player _player;
    private State _state;
    private Pack _pack;

    public override void _Ready()
    {
        _player = GetParent() as Player;
        _state = GetNodeOrNull<State>("../state");
        _pack = GetNodeOrNull<Pack>("../pack");
    }

    // 专用快捷键在 GUI 处理之前接收，避免焦点控件截走；模态窗口由各自 UI 处理。
    public override void _Input(InputEvent ev)
    {
        if (Stop.IsPaused || (ev is InputEventKey key && key.Echo)) return;
        SupplyKind? kind = ev.IsActionPressed("Q") ? SupplyKind.Bandage :
            ev.IsActionPressed("Z") ? SupplyKind.Antidote :
            ev.IsActionPressed("E") ? SupplyKind.Drug : null;
        if (kind == null) return;
        Use(kind.Value);
        GetViewport().SetInputAsHandled();
    }

    public bool CanUse(SupplyKind kind, out string reason)
    {
        if (_player == null || _state == null || _pack == null)
        { reason = "物品暂时不可用"; return false; }
        if (_player.hp <= 0)
        { reason = "生命已归零，无法使用物品"; return false; }
        if (!_pack.Has(kind))
        { reason = "没有该物品，可在阴掌柜处购买"; return false; }
        switch (kind)
        {
            case SupplyKind.Drug when _player.hp >= _player.MaxHp:
                reason = "生命已满，无需使用药品"; return false;
            case SupplyKind.Bandage when !_state.IsBleeding:
                reason = "当前没有流血，无需使用绷带"; return false;
            case SupplyKind.Antidote when !_state.IsPoisoned:
                reason = "当前没有中毒，无需使用解毒剂"; return false;
            case SupplyKind.Bullet:
                reason = "请装填弹药"; return false;
        }
        reason = ""; return true;
    }

    public bool TryUse(SupplyKind kind, out string feedback)
    {
        if (!CanUse(kind, out feedback) || !_pack.TryConsume(kind)) return false;
        switch (kind)
        {
            case SupplyKind.Drug:
                float healed = Mathf.Min(_state.HealAmount, _player.MaxHp - _player.hp);
                _player.hp += healed;
                feedback = $"使用药品，恢复 {healed:0} 点生命"; break;
            case SupplyKind.Bandage:
                _state.ClearState(PlayerState.Bleed); feedback = "使用绷带，流血已解除"; break;
            case SupplyKind.Antidote:
                _state.ClearState(PlayerState.Slow); feedback = "使用解毒剂，中毒已解除"; break;
        }
        return true;
    }

    public void Use(SupplyKind kind)
    {
        TryUse(kind, out string feedback);
        InteractionController.Notify(feedback);
    }
    public void UseBandage() => Use(SupplyKind.Bandage);
    public void UseAntidote() => Use(SupplyKind.Antidote);
    public void UseDrug() => Use(SupplyKind.Drug);
}
