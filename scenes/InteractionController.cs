using Godot;
using System.Collections.Generic;

public interface IWorldInteractable
{
    bool CanInteract { get; }
    string InteractionName { get; }
    string InteractionText { get; }
    string ActionText { get; }
    float InteractionRadius { get; }
    void Interact(Player player);
}

/// <summary>统一最近目标、确认/离开和滚轮选择，防止一次 F 同时触发多个物体。</summary>
public partial class InteractionController : CanvasLayer
{
    private static InteractionController _instance;
    private Player _player;
    private Node2D _target, _dialogTarget;
    private Stop _stop;
    private Control _root, _shade;
    private Label _prompt, _toast, _text;
    private PanelContainer _dialog;
    private Button _confirm, _cancel;
    private int _choice;
    private float _toastTime;
    private AudioStreamPlayer _click;
    public bool IsDialogOpen => _dialog?.Visible == true;
    public override void _Ready()
    {
        _instance = this;
        Layer = 20;
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _stop = new Stop(); AddChild(_stop);
        _click = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://music/interaction.wav"), Bus = "Sfx" }; AddChild(_click);
        _root = new Control { Theme = UiKit.Theme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_root); _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _prompt = UiKit.Label(""); _root.AddChild(_prompt);
        _prompt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _prompt.OffsetLeft = -260; _prompt.OffsetRight = 260; _prompt.OffsetTop = -100; _prompt.OffsetBottom = -64;
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _toast = UiKit.Label(""); _root.AddChild(_toast);
        _toast.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _toast.OffsetLeft = -300; _toast.OffsetRight = 300; _toast.OffsetTop = 82; _toast.OffsetBottom = 122;
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        _shade = new Godot.ColorRect { Color = new Color(0, 0, 0, 0.65f), Visible = false };
        _root.AddChild(_shade); _shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _dialog = new PanelContainer { Visible = false }; _root.AddChild(_dialog);
        UiKit.Center(_dialog, new Vector2(580, 235));
        var contents = new VBoxContainer(); _dialog.AddChild(contents);
        _text = UiKit.Label(""); _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(520, 90); contents.AddChild(_text);
        _confirm = UiKit.Button("", Confirm); _cancel = UiKit.Button(GameData.Text("text_no"), CloseDialogue);
        contents.AddChild(_confirm); contents.AddChild(_cancel);
    }
    public static void Notify(string text)
    {
        if (_instance == null || !GodotObject.IsInstanceValid(_instance)) { GD.Print(text); return; }
        _instance._toast.Text = text; _instance._toastTime = 4;
    }
    public override void _ExitTree() { if (_instance == this) _instance = null; }
    public override void _Process(double delta)
    {
        if (_toastTime > 0) _toastTime -= (float)delta; else _toast.Text = "";
        if (IsDialogOpen) { _prompt.Text = "F 确认 · 滚轮选择 · Esc 离开"; return; }
        _target = null;
        if (_player == null || _player.hp <= 0 || Stop.IsPaused) { _prompt.Text = ""; return; }
        float nearest = float.MaxValue;
        foreach (Node node in GetTree().GetNodesInGroup("interactable"))
        {
            if (node is not Node2D world || node is not IWorldInteractable item || !item.CanInteract) continue;
            float dist = world.GlobalPosition.DistanceTo(_player.GlobalPosition);
            if (dist <= item.InteractionRadius && dist < nearest) { nearest = dist; _target = world; }
        }
        _prompt.Text = _target is IWorldInteractable target ? $"[ F ] {target.ActionText}" : "";
    }
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key && key.Echo) return;
        if (IsDialogOpen)
        {
            if (ev.IsActionPressed("esc")) { CloseDialogue(); GetViewport().SetInputAsHandled(); }
            else if (ev.IsActionPressed("interact"))
            { if (_choice == 0) Confirm(); else CloseDialogue(); GetViewport().SetInputAsHandled(); }
            else if (ev is InputEventMouseButton mouse && mouse.Pressed &&
                (mouse.ButtonIndex == MouseButton.WheelUp || mouse.ButtonIndex == MouseButton.WheelDown))
            { Select(1 - _choice); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (!Stop.IsPaused && ev.IsActionPressed("interact") && _target != null)
        { OpenDialogue(_target); GetViewport().SetInputAsHandled(); }
    }
    public void OpenDialogue(Node2D target)
    {
        if (Stop.IsPaused || target is not IWorldInteractable item || !item.CanInteract ||
            _player.GlobalPosition.DistanceTo(target.GlobalPosition) > item.InteractionRadius) return;
        _dialogTarget = target; _click.Play();
        _text.Text = $"{item.InteractionName}\n{item.InteractionText}";
        _confirm.Text = item.ActionText;
        _shade.Visible = _dialog.Visible = true;
        _stop.SetPaused(true); Select(0);
    }
    private void Select(int choice)
    {
        _choice = choice;
        if (choice == 0) _confirm.GrabFocus(); else _cancel.GrabFocus();
    }
    private void Confirm()
    {
        Node2D target = _dialogTarget;
        CloseDialogue();
        if (GodotObject.IsInstanceValid(target) && target is IWorldInteractable item && item.CanInteract &&
            _player.hp > 0 && _player.GlobalPosition.DistanceTo(target.GlobalPosition) <= item.InteractionRadius)
            item.Interact(_player);
    }
    public void CloseDialogue()
    {
        _shade.Visible = _dialog.Visible = false; _dialogTarget = null; _stop.SetPaused(false);
    }
}
