using System;

namespace SuperNewRoles.Roles.Ability;

[Flags]
public enum ShrAbilityTrigger
{
    Pet = 1,
    Phantom = 2,
    Shapeshift = 4,
    ShapeshiftSelf = 8,
}

public sealed class ShrAbilityTriggerAbility : AbilityBase
{
    public ShrAbilityTrigger Triggers { get; }
    private readonly Action<ShrAbilityTrigger> activate;
    private readonly Action<ShrAbilityTrigger, PlayerControl> activateWithTarget;
    private readonly Func<bool> canActivate;
    private readonly float cooldown;
    private float readyAt;
    private float nextInputAt;

    internal float RemainingCooldown(float now) => Math.Max(0f, readyAt - now);
    internal void ResetCooldown(float now, float duration)
    {
        readyAt = now + Math.Max(0f, duration);
        nextInputAt = now;
    }

    internal bool TryConsumeInput(float now)
    {
        if (now < nextInputAt) return false;
        nextInputAt = now + 0.35f;
        return true;
    }

    public ShrAbilityTriggerAbility(ShrAbilityTrigger triggers, Action<ShrAbilityTrigger> activate,
        Func<bool> canActivate = null, float cooldown = 0f)
    {
        if (triggers == 0 || (triggers & ~(ShrAbilityTrigger.Pet | ShrAbilityTrigger.Phantom
            | ShrAbilityTrigger.Shapeshift | ShrAbilityTrigger.ShapeshiftSelf)) != 0)
            throw new ArgumentOutOfRangeException(nameof(triggers));
        if (!float.IsFinite(cooldown) || cooldown < 0f) throw new ArgumentOutOfRangeException(nameof(cooldown));
        Triggers = triggers;
        this.activate = activate ?? throw new ArgumentNullException(nameof(activate));
        this.canActivate = canActivate;
        this.cooldown = cooldown;
    }

    public ShrAbilityTriggerAbility(ShrAbilityTrigger triggers, Action<ShrAbilityTrigger, PlayerControl> activate,
        Func<bool> canActivate = null, float cooldown = 0f)
        : this(triggers, _ => { }, canActivate, cooldown)
    {
        activateWithTarget = activate ?? throw new ArgumentNullException(nameof(activate));
    }

    internal bool TryActivate(ShrAbilityTrigger source, float now, PlayerControl target = null)
    {
        if (!Triggers.HasFlag(source) || now < readyAt || canActivate?.Invoke() == false) return false;
        readyAt = now + Math.Max(0.35f, cooldown);
        if (activateWithTarget != null) activateWithTarget(source, target);
        else activate(source);
        return true;
    }
}
