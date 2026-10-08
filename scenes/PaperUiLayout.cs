using Godot;

/// <summary>羊皮纸界面共用的字体、图标和美术按钮布局。</summary>
public static class PaperUiLayout
{
    public static readonly Color Ink = new(0.12f, 0.1f, 0.07f);
    public static Theme Theme() => new() { DefaultFont = new SystemFont
        { FontNames = new[] { "Noto Sans CJK SC", "WenQuanYi Zen Hei", "sans-serif" } } };

    public static void Label(Label label, Vector2 position, Vector2 size, int fontSize, Color color,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        label.Position = position; label.Size = size;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.HorizontalAlignment = alignment;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("line_spacing", 2);
        label.AddThemeColorOverride("font_color", color);
    }

    public static void Fit(Sprite2D sprite, Vector2 size, bool cropTransparentMargin = false, bool preserveAspect = false)
    {
        if (sprite.Texture == null) return;
        Vector2 source = sprite.Texture.GetSize();
        if (cropTransparentMargin)
        {
            var used = sprite.Texture.GetImage().GetUsedRect();
            if (used.Size.X > 0 && used.Size.Y > 0)
            {
                sprite.RegionEnabled = true; sprite.RegionRect = used; source = used.Size;
            }
        }
        sprite.Scale = preserveAspect ? Vector2.One * Mathf.Min(size.X / source.X, size.Y / source.Y) : size / source;
    }

    public static void Icon(Sprite2D sprite, float size) => Fit(sprite, Vector2.One * size, true, true);

    public static void ArtButton(Button button, Vector2 position, Vector2 size, int fontSize = 18)
    {
        button.Position = position; button.Size = size;
        button.SelfModulate = new Color(1, 1, 1, 0);
        foreach (Node child in button.GetChildren())
        {
            if (child is Sprite2D sprite) { sprite.Position = size / 2; Fit(sprite, size); }
            if (child is TextureRect texture)
            {
                texture.Position = Vector2.Zero; texture.Size = size;
                texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                texture.MouseFilter = Control.MouseFilterEnum.Ignore;
            }
            if (child is Label label) Label(label, Vector2.Zero, size, fontSize, Ink, HorizontalAlignment.Center);
        }
    }

    public static void Close(Control owner)
    {
        var button = owner.GetNode<Button>("close2");
        button.Position = new Vector2(922, 64); button.Size = new Vector2(52, 52);
        foreach (var path in new[] { "点击", "未点击" })
        {
            var sprite = owner.GetNode<Sprite2D>(path);
            sprite.Position = button.Position + button.Size / 2;
            Fit(sprite, button.Size, true, true);
        }
        button.TooltipText = "关闭（Esc）";
    }
}
