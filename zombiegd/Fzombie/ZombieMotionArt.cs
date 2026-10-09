using Godot;
using System.Collections.Generic;

/// <summary>
/// 八帧真实手绘图集：上排四帧行走，下排四帧攻击。只改变美术节点，
/// 待机、出现、受击及死亡仍使用场景原立绘和原有 Tween。
/// </summary>
public partial class ZombieMotionArt : AnimatedSprite2D
{
    private sealed class AtlasData
    {
        public SpriteFrames Frames;
        public Vector2 CellSize;
        public Rect2 WalkBounds;
        public Rect2 AttackBounds;
    }

    private static readonly Dictionary<string, AtlasData> AtlasCache = new();
    private static readonly Dictionary<string, Rect2I> TextureBoundsCache = new();
    private static int _liveInstances;
    private AtlasData _atlas;
    private float _height;
    private float _foot;
    private float _center;
    private double _walkElapsed;
    private double _attackElapsed;
    public double AttackDuration { get; private set; }

    public override void _EnterTree() => _liveInstances++;

    public override void _ExitTree()
    {
        if (--_liveInstances > 0) return;
        // 场景退出后不让静态缓存继续持有原生资源；下一张地图重新按需加载。
        AtlasCache.Clear();
        TextureBoundsCache.Clear();
    }

    public static ZombieMotionArt Create(Sprite2D original, int monsterId)
    {
        if (original?.Texture == null) return null;
        string species = monsterId switch
        {
            2 => "shunyi", 3 => "gongjian", 4 => "lizhua", 5 => "daju", 6 => "dujian", _ => "mini"
        };
        string path = $"res://assets/generated/zombie_motion/{species}.png";
        if (!AtlasCache.TryGetValue(path, out AtlasData atlas))
        {
            if (!ResourceLoader.Exists(path)) return null;
            Texture2D texture = GD.Load<Texture2D>(path);
            if (texture == null || texture.GetWidth() < 4 || texture.GetHeight() < 2) return null;
            atlas = BuildAtlas(texture);
            AtlasCache[path] = atlas;
        }

        string originalPath = original.Texture.ResourcePath;
        if (!TextureBoundsCache.TryGetValue(originalPath, out Rect2I bounds))
        {
            using Image originalPixels = original.Texture.GetImage();
            bounds = originalPixels.GetUsedRect();
            TextureBoundsCache[originalPath] = bounds;
        }
        var result = new ZombieMotionArt
        {
            Name = "MotionArt", SpriteFrames = atlas.Frames, Animation = "walk", Visible = false,
            TextureFilter = original.TextureFilter, ZIndex = original.ZIndex,
            Modulate = original.Modulate, _atlas = atlas,
            _height = bounds.Size.Y * original.Scale.Y,
            _foot = original.Position.Y + (bounds.End.Y - original.Texture.GetHeight() / 2f) * original.Scale.Y,
            _center = original.Position.X + (bounds.Position.X + bounds.Size.X / 2f - original.Texture.GetWidth() / 2f) * original.Scale.X
        };
        result.ApplyAlignment(false);
        return result;
    }

    private static AtlasData BuildAtlas(Texture2D texture)
    {
        float width = texture.GetWidth() / 4f;
        float height = texture.GetHeight() / 2f;
        using Image pixels = texture.GetImage();
        pixels.Convert(Image.Format.Rgba8);
        byte[] rgba = pixels.GetData();
        int imageWidth = pixels.GetWidth();
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        var atlas = new AtlasData { Frames = frames, CellSize = new Vector2(width, height) };
        for (int row = 0; row < 2; row++)
        {
            string name = row == 0 ? "walk" : "attack";
            frames.AddAnimation(name);
            frames.SetAnimationLoopMode(name, row == 0 ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
            frames.SetAnimationSpeed(name, 10);
            Rect2 rowBounds = default;
            for (int column = 0; column < 4; column++)
            {
                var region = new Rect2(column * width, row * height, width, height);
                frames.AddFrame(name, new AtlasTexture { Atlas = texture, Region = region, FilterClip = true });
                // 图像生成器输出的尺寸不一定能被4/2整除。渲染保留等宽的半像素
                // 格子，像素分析取相邻的整数边界，再换回格子内坐标。
                int left = Mathf.FloorToInt(region.Position.X + 0.5f);
                int top = Mathf.FloorToInt(region.Position.Y + 0.5f);
                int right = Mathf.FloorToInt(region.End.X + 0.5f);
                int bottom = Mathf.FloorToInt(region.End.Y + 0.5f);
                Rect2I used = VisibleBounds(rgba, imageWidth, new Rect2I(left, top, right - left, bottom - top));
                if (used.Size == Vector2I.Zero) continue;
                var bounds = new Rect2((Vector2)used.Position + new Vector2(left, top) - region.Position, used.Size);
                rowBounds = rowBounds.Size == Vector2.Zero ? bounds : rowBounds.Merge(bounds);
            }
            if (rowBounds.Size == Vector2.Zero) rowBounds = new Rect2(0, 0, width, height);
            if (row == 0) atlas.WalkBounds = rowBounds;
            else atlas.AttackBounds = rowBounds;
        }
        return atlas;
    }

    // 生成透明图的留白中可能有 alpha=1/255 的不可见杂点。直接 GetUsedRect
    // 会把整张格子当作角色，导致身体缩小、脚底漂移；仅用至少10%不透明度校准。
    private static Rect2I VisibleBounds(byte[] rgba, int imageWidth, Rect2I region)
    {
        const byte minimumAlpha = 26;
        int left = region.End.X, top = region.End.Y;
        int right = region.Position.X - 1, bottom = region.Position.Y - 1;
        for (int y = region.Position.Y; y < region.End.Y; y++)
        {
            int index = (y * imageWidth + region.Position.X) * 4 + 3;
            for (int x = region.Position.X; x < region.End.X; x++, index += 4)
            {
                if (rgba[index] < minimumAlpha) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }
        return right < left ? default : new Rect2I(left - region.Position.X, top - region.Position.Y,
            right - left + 1, bottom - top + 1);
    }

    // 同一排共用尺度与脚底锚点，帧变化时不会随画布边距上下跳动。
    private void ApplyAlignment(bool attack)
    {
        Rect2 bounds = attack ? _atlas.AttackBounds : _atlas.WalkBounds;
        float scale = _height / Mathf.Max(1, bounds.Size.Y);
        Scale = Vector2.One * scale;
        float center = bounds.Position.X + bounds.Size.X / 2f;
        float offsetX = _center - (center - _atlas.CellSize.X / 2f) * scale;
        Position = new Vector2(FlipH ? -offsetX : offsetX, _foot - (bounds.End.Y - _atlas.CellSize.Y / 2f) * scale);
    }

    public void SetFacing(bool flipped)
    {
        FlipH = flipped;
        ApplyAlignment(Animation == "attack");
    }

    public void AdvanceWalk(double delta, float framesPerSecond)
    {
        if (!Visible || Animation != "walk")
        {
            Animation = "walk";
            _walkElapsed = 0;
            Frame = 0;
            ApplyAlignment(false);
        }
        Visible = true;
        _walkElapsed += delta * framesPerSecond;
        Frame = (int)_walkElapsed % 4;
    }

    public void BeginAttack(double duration)
    {
        Stop();
        AttackDuration = duration;
        _attackElapsed = 0;
        Animation = "attack";
        Frame = 0;
        ApplyAlignment(true);
        Visible = true;
    }

    public bool AdvanceAttack(double delta)
    {
        _attackElapsed += delta;
        Frame = Mathf.Min(3, (int)(_attackElapsed * 4 / AttackDuration));
        return _attackElapsed >= AttackDuration;
    }

    public void RestoreOriginal()
    {
        Stop();
        Visible = false;
        _walkElapsed = 0;
        _attackElapsed = 0;
    }
}
