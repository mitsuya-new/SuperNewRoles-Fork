using System;
using AmongUs.GameOptions;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Crewmate;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal sealed class SHRCrewEffectTimer
{
    private float readyAt;
    private float endsAt;
    internal bool Active { get; private set; }
    internal float Remaining(float now) => Math.Max(0f, (Active ? endsAt : readyAt) - now);
    internal void Reset(float now, float cooldown) { Active = false; readyAt = now + cooldown; }
    internal bool Activate(float now, float duration)
    {
        if (Active || now < readyAt) return false;
        Active = true;
        endsAt = now + duration;
        return true;
    }
    internal bool Expire(float now, float cooldown)
    {
        if (!Active || now < endsAt) return false;
        Reset(now, cooldown);
        return true;
    }
}

public sealed class SHRCrewBoostAbility : AbilityBase
{
    private readonly SHRCrewEffectTimer timer = new();
    private bool initialized;
    private float nextInput;
    private float lastLight = -1f;
    internal static bool Supports(RoleId role) => role is RoleId.SpeedBooster or RoleId.Lighter or RoleId.Clergy or RoleId.WiseMan or RoleId.EvilSpeedBooster or RoleId.EvilScientist;
    internal static bool UsesEngineer(RoleId role) => Supports(role) && role is not (RoleId.EvilSpeedBooster or RoleId.EvilScientist);
    private float Cooldown => Player.Role switch
    {
        RoleId.SpeedBooster => SpeedBooster.SpeedBoosterCooldown,
        RoleId.Clergy => Clergy.ClergyButtonCooldown,
        RoleId.WiseMan => WiseMan.WiseManCoolTime,
        RoleId.EvilSpeedBooster => EvilSpeedBooster.EvilSpeedBoosterCooldown,
        RoleId.EvilScientist => EvilScientist.EvilScientistCoolTime,
        _ => Lighter.LighterCoolTime,
    };
    private float Duration => Player.Role switch
    {
        RoleId.SpeedBooster => SpeedBooster.SpeedBoosterDuration,
        RoleId.Clergy => Clergy.ClergyEffectDuration,
        RoleId.WiseMan => WiseMan.WiseManDurationTime,
        RoleId.EvilSpeedBooster => EvilSpeedBooster.EvilSpeedBoosterDuration,
        RoleId.EvilScientist => EvilScientist.EvilScientistDurationTime,
        _ => Lighter.LighterDurationTime,
    };
    internal float Remaining => timer.Remaining(Time.unscaledTime);
    internal bool Active => initialized && timer.Active;

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || !SHRPlayerOptions.IsVanillaGuest(player.Player) || !Supports(player.Role)
            || player.GetAbility<SHRCrewBoostAbility>() != null) return;
        player.AttachAbility(new SHRCrewBoostAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(UsesEngineer(Player.Role) ? SHRAbilityTrigger.Pet : SHRAbilityTrigger.Phantom,
            _ => Activate(), CanUse), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(PreCalledMeetingEvent.Instance, _ => Stop());
        SubscribeWithAbility(WrapUpEvent.Instance, _ => Reset());
    }

    private void Reset()
    {
        timer.Reset(Time.unscaledTime, SHRKillRequests.InitialCooldown(Cooldown, GameSettingOptions.InitialCooldown));
        Send(reset: true);
    }

    private bool CanUse() => initialized && Player.IsAlive() && Time.unscaledTime >= nextInput
        && (timer.Active ? Player.Role is not (RoleId.Clergy or RoleId.WiseMan) : Remaining <= 0f);

    internal void Activate()
    {
        if (!CanUse()) return;
        nextInput = Time.unscaledTime + 0.35f;
        if (timer.Active) { Stop(); return; }
        if (!timer.Activate(Time.unscaledTime, Duration)) return;
        if (Player.Role == RoleId.Clergy) Player.GetAbility<ClergyAbility>()?.RpcSetClergyEffect(true);
        if (Player.Role == RoleId.WiseMan) Player.GetAbility<WiseManAbility>()?.RpcSetWiseManStatus(true, Player.transform.position);
        if (Player.Role == RoleId.EvilScientist) Player.GetAbility<InvisibleAbility>()?.RpcSetInvisible(Player, true);
        Send(reset: true);
    }

    private void Send(bool reset = false)
    {
        if (Player?.Data == null || Player.Data.Disconnected) return;
        lastLight = SHRMadKillerSupport.LightFraction();
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: reset && Player.IsAlive());
    }

    private void Tick()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (!initialized) { initialized = true; Reset(); }
        if (timer.Active && (Player.IsDead() || MeetingHud.Instance != null)) { Stop(); return; }
        if (timer.Expire(Time.unscaledTime, Cooldown))
        {
            if (Player.Role == RoleId.Clergy) Player.GetAbility<ClergyAbility>()?.RpcSetClergyEffect(false);
            if (Player.Role == RoleId.WiseMan) Player.GetAbility<WiseManAbility>()?.RpcSetWiseManStatus(false, Player.transform.position);
            if (Player.Role == RoleId.EvilScientist) Player.GetAbility<InvisibleAbility>()?.RpcSetInvisible(Player, false);
            Send(reset: true);
        }
        else if (timer.Active && Player.Role == RoleId.Lighter && Math.Abs(SHRMadKillerSupport.LightFraction() - lastLight) >= 0.01f) Send();
    }

    private void Stop()
    {
        if (!timer.Active) return;
        timer.Reset(Time.unscaledTime, Cooldown);
        if (Player.Role == RoleId.Clergy) Player.GetAbility<ClergyAbility>()?.RpcSetClergyEffect(false);
        if (Player.Role == RoleId.WiseMan) Player.GetAbility<WiseManAbility>()?.RpcSetWiseManStatus(false, Player.transform.position);
        if (Player.Role == RoleId.EvilScientist) Player.GetAbility<InvisibleAbility>()?.RpcSetInvisible(Player, false);
        if (AmongUsClient.Instance?.AmHost == true && ModeManager.IsMode(ModeId.SuperHostRoles)) Send(reset: true);
    }

    public override void DetachToAlls() => Stop();

    internal static float LighterCrewVision(float multiplier, float minRadius, float maxRadius, float lightFraction)
        => maxRadius * multiplier / Math.Max(0.001f, minRadius + (maxRadius - minRadius) * Math.Clamp(lightFraction, 0f, 1f));
}
