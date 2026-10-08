using Godot;

/// <summary>利爪攻击命中时附加流血，靠近而未被攻击不会流血。</summary>
public partial class SharpZom : Zombie
{
    [Export(PropertyHint.Range, "0,1,0.05")] public float BleedChance { get; set; } = 1f;
    protected override void Attack()
    {
        if (_player is not Player player || !CanEngage() || !IsWithinMeleeRange()) return;
        base.Attack();
        if (player.hp > 0 && GD.Randf() < BleedChance)
            player.GetNode<State>("state").ChangeState(PlayerState.Bleed);
    }
}
