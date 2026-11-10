using System;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRPusherAbility : AbilityBase
{
    private bool initialized;
    private float readyAt;
    internal float Remaining => Math.Max(0f, readyAt - Time.unscaledTime);
    internal static bool SupportsMap(byte map) => map is 0 or 2 or 4 or 5;
    internal static bool IsWithinPushSite(float dx, float dy, float radius)
        => radius >= 0f && dx * dx + dy * dy <= radius * radius;
    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player.Role != RoleId.Pusher || !SHRPlayerOptions.IsVanillaGuest(player.Player)
            || player.GetAbility<SHRPusherAbility>() != null) return;
        player.AttachAbility(new SHRPusherAbility(), new AbilityParentRole(player, player.roleBase));
    }
    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Phantom, _ => Use(),
            () => initialized && Player.IsAlive() && Remaining <= 0f && Player.GetAbility<PusherAbility>()?.FindSHRTarget() != null), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(MurderEvent.Instance, data =>
        {
            if (data.killer != Player || data.target == Player || !Player.IsAlive()) return;
            readyAt = Time.unscaledTime + Pusher.PusherCooldown;
            SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: true);
        });
        SubscribeWithAbility(WrapUpEvent.Instance, _ =>
        {
            readyAt = Time.unscaledTime + 0.1f;
            SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: Player.IsAlive());
        });
    }
    private void Use()
    {
        if (Remaining > 0f || Player.GetAbility<PusherAbility>()?.TryUseSHR() != true) return;
        readyAt = Time.unscaledTime + Pusher.PusherCooldown;
        if (Player.IsAlive()) SHRKillRequests.SetRemainingCooldown(Player.Player, SHRPlayerOptions.KillCooldown(Player));
    }
    private void Tick()
    {
        if (initialized || AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        initialized = true;
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(Pusher.PusherCooldown, GameSettingOptions.InitialCooldown);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: true);
    }
}
