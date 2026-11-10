using System;
using AmongUs.GameOptions;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal sealed class SHRBansheeCooldowns
{
    private float releaseReadyAt;
    private float whisperReadyAt;
    internal void Reset(float now, float release, float whisper) { releaseReadyAt = now + release; whisperReadyAt = now + whisper; }
    internal void Released(float now, float cooldown) => releaseReadyAt = now + cooldown;
    internal void Whispered(float now, float cooldown) => whisperReadyAt = now + cooldown;
    internal float Remaining(float now, bool hasFairy) => Math.Max(0f, (hasFairy ? whisperReadyAt : releaseReadyAt) - now);
}

public sealed class SHRBansheeAbility : AbilityBase
{
    private readonly SHRBansheeCooldowns cooldowns = new();
    private bool initialized;
    private BansheeAbility BansheeAbility => Player.GetAbility<BansheeAbility>();
    internal float Remaining => cooldowns.Remaining(Time.unscaledTime, BansheeAbility?.SHRFairy != null);

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player.Role != RoleId.Banshee || !SHRPlayerOptions.IsVanillaGuest(player.Player)
            || player.GetAbility<SHRBansheeAbility>() != null) return;
        player.AttachAbility(new SHRBansheeAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Shapeshift | SHRAbilityTrigger.ShapeshiftSelf,
            (_, target) => Use(target), CanUse), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(WrapUpEvent.Instance, _ => ResetCooldowns());
    }

    internal void ResetCooldowns()
    {
        cooldowns.Reset(Time.unscaledTime, Banshee.BansheeReleaseCooldown, Banshee.BansheeWhisperCooldown);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: Player.IsAlive());
    }

    private bool CanUse() => initialized && Player.IsAlive() && Remaining <= 0f && BansheeAbility is { SHRWhispering: false };
    private void Use(PlayerControl target)
    {
        if (!CanUse()) return;
        if (BansheeAbility.SHRFairy != null)
        {
            BansheeAbility.RpcSHRFairy(BansheeAbility.SHRFairy, true);
            cooldowns.Whispered(Time.unscaledTime, Banshee.BansheeWhisperCooldown);
        }
        else
        {
            var selected = TargetCustomButtonBase.SetTarget(targetingPlayer: Player.Player, isTargetable: candidate => candidate.Player == target);
            if (selected == null) return;
            BansheeAbility.RpcSHRFairy(selected, false);
            cooldowns.Released(Time.unscaledTime, Banshee.BansheeReleaseCooldown);
        }
        SHRRoleNameDisplay.Refresh();
    }

    private void Tick()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed || initialized) return;
        initialized = true;
        cooldowns.Reset(Time.unscaledTime,
            SHRKillRequests.InitialCooldown(Banshee.BansheeReleaseCooldown, GameSettingOptions.InitialCooldown),
            SHRKillRequests.InitialCooldown(Banshee.BansheeWhisperCooldown, GameSettingOptions.InitialCooldown));
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: true);
    }

    internal string Status => BansheeAbility?.SHRFairy is { } fairy
        ? " [" + ModTranslation.GetString(BansheeAbility.SHRWhispering ? "BansheeWhisperButtonText" : "BansheeReleaseButtonText")
            + ": " + SHRCamouflagerController.OriginalName(fairy.Player) + "]" : "";
}
