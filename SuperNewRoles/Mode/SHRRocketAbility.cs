using System;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRRocketAbility : AbilityBase
{
    private bool initialized;
    private float readyAt;
    private RocketAbility RocketAbility => Player.GetAbility<RocketAbility>();
    internal float Remaining => Math.Max(0f, readyAt - Time.unscaledTime);
    private float Cooldown => RocketAbility?.GrabAbility?.DefaultTimer ?? Rocket.RocketInitialGrabCooldown;

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player.Role != RoleId.Rocket || !SHRPlayerOptions.IsVanillaGuest(player.Player)
            || player.GetAbility<SHRRocketAbility>() != null) return;
        player.AttachAbility(new SHRRocketAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Phantom, _ => Grab(), CanGrab), new AbilityParentAbility(this));
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => Launch(), CanLaunch), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(WrapUpEvent.Instance, _ => Reset());
    }

    private void Reset()
    {
        readyAt = Time.unscaledTime + Cooldown;
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: Player.IsAlive(),
            killCooldown: 0.001f, resetKill: Player.IsAlive());
    }

    private bool CanGrab() => initialized && Player.IsAlive() && Remaining <= 0f && RocketAbility?.GrabAbility?.FindSHRTarget() != null;
    private bool CanLaunch() => initialized && Player.IsAlive() && RocketAbility?.GrabAbility?.GrabbedPlayers.Count > 0;
    private void Grab()
    {
        if (!CanGrab() || RocketAbility.GrabAbility.TryGrabSHR() != true) return;
        Reset();
        SHRRoleNameDisplay.Refresh();
    }
    internal bool TryLaunchFromKill() => SHRAbilityTriggerPatches.CanActivate(Player.Player) && Launch();
    private bool Launch()
    {
        if (!CanLaunch() || RocketAbility.LaunchAbility.TryLaunchSHR() != true) return false;
        Reset();
        SHRRoleNameDisplay.Refresh();
        return true;
    }

    private void Tick()
    {
        if (initialized || AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        initialized = true;
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(Cooldown, GameSettingOptions.InitialCooldown);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: true, killCooldown: 0.001f, resetKill: true);
    }

    internal string Status => " [" + ModTranslation.GetString("RocketGrabButtonText") + ": " + (RocketAbility?.GrabAbility?.GrabbedPlayers.Count ?? 0) + "]";
}
