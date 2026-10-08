using Godot;

/// <summary>瞬移独立于普通攻击冷却；只落在领地内、不与玩家或墙体重叠的位置。</summary>
public partial class FastMoveZom : Zombie
{
    [Export] public float TeleportTriggerRange { get; set; } = 300f;
    [Export] public float TeleportDistance { get; set; } = 40f;
    [Export] public float TeleportCooldown { get; set; } = 10f;
    private float _teleportTimer;
    public override void _PhysicsProcess(double delta)
    {
        if (!_active || IsAppearing || IsYielding) { base._PhysicsProcess(delta); return; }
        if (Stop.IsPaused || !EnsurePlayer()) return;
        _teleportTimer = Mathf.Max(0, _teleportTimer - (float)delta);
        float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (CanEngage() && _teleportTimer <= 0 && distance > AttackRange && distance <= TeleportTriggerRange)
        {
            if (TryFindTeleportPosition(out Vector2 target))
            {
                GlobalPosition = target;
                Velocity = Vector2.Zero;
                _teleportTimer = TeleportCooldown;
            }
            else
            {
                // 窄通道或玩家被墙包围时仍普通追击，不每帧重复大量空间查询。
                _teleportTimer = 0.25f;
            }
        }
        base._PhysicsProcess(delta);
    }

    private bool TryFindTeleportPosition(out Vector2 target)
    {
        target = GlobalPosition;
        var shapeNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shapeNode?.Shape == null || shapeNode.Disabled) return false;

        Vector2 direction = _player.GlobalPosition.DirectionTo(GlobalPosition);
        if (direction.IsZeroApprox()) direction = Vector2.Right;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shapeNode.Shape,
            CollisionMask = CollisionMask | (_player is CollisionObject2D body ? body.CollisionLayer : 0),
            CollideWithBodies = true,
            CollideWithAreas = false,
            Margin = 3f,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        float nearestDistance = Mathf.Clamp(TeleportDistance, 1f, AttackRange);
        for (int ring = 0; ring < 3; ring++)
        {
            float radius = Mathf.Lerp(nearestDistance, AttackRange, ring / 2f);
            for (int index = 0; index < 16; index++)
            {
                // 先尝试原方向，再交替尝试两侧；必须在改变位置前检查最终落点。
                int step = (index + 1) / 2;
                float angle = step * Mathf.Tau / 16f * (index % 2 == 0 ? -1f : 1f);
                Vector2 candidate = _player.GlobalPosition + direction.Rotated(angle) * radius;
                if (!IsWithinTerritory(candidate)) continue;
                Transform2D transform = shapeNode.GlobalTransform;
                transform.Origin += candidate - GlobalPosition;
                query.Transform = transform; // 保留碰撞体自身的偏移、旋转及 0.068 缩放。
                if (GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count != 0) continue;
                target = candidate;
                return true;
            }
        }
        return false;
    }
}
