using Godot;

public static class UiKit
{
    public static readonly Color Gold = new("d9b86c"), Ink = new("201b19"), Paper = new("e8d6af");
    public static Theme Theme()
    {
        var theme = new Theme { DefaultFontSize = 20 };
        var font = new SystemFont { FontNames = new[] { "Noto Sans CJK SC", "WenQuanYi Zen Hei", "sans-serif" } };
        theme.DefaultFont = font;
        foreach (string type in new[] { "Label", "Button", "CheckButton" }) theme.SetColor("font_color", type, Paper);
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            theme.SetStylebox(state, "Button", Box(state == "hover" || state == "focus" ? new Color("514332") : Ink));
        theme.SetStylebox("panel", "PanelContainer", Box(new Color("211d1bf5")));
        return theme;
    }
    public static StyleBoxFlat Box(Color color) => new()
    {
        BgColor = color, BorderColor = Gold, BorderWidthLeft = 1, BorderWidthRight = 1,
        BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12
    };
    public static Label Label(string text, int size = 20)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
    public static Button Button(string text, System.Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44) };
        button.Pressed += action;
        return button;
    }
    public static void Center(Control panel, Vector2 size)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -size.X / 2; panel.OffsetRight = size.X / 2;
        panel.OffsetTop = -size.Y / 2; panel.OffsetBottom = size.Y / 2;
    }
}
