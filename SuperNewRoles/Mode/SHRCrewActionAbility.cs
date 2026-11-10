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
using SuperNewRoles.Roles.CrewMate;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal sealed class SHRCrewActionTimer
{
    private float readyAt;
    private float? pendingAt;
    internal bool Pending => pendingAt.HasValue;
    internal float Remaining(float now) => Math.Max(0f, (pendingAt ?? readyAt) - now);
    internal void Reset(float now, float cooldown) { pendingAt = null; readyAt = now + cooldown; }
    internal bool Queue(float now, float delay)
    {
        if (Pending || now < readyAt) return false;
        pendingAt = now + Math.Max(0f, delay);
        return true;
    }
    internal bool Consume(float now, float cooldown)
    {
        if (!pendingAt.HasValue || now < pendingAt.Value) return false;
        Reset(now, cooldown);
        return true;
    }
}

public sealed class SHRCrewActionAbility : AbilityBase
{
    private readonly SHRCrewActionTimer timer = new();
    private Vector2? storedPosition;
    private bool initialized;
    private bool ventQueued;
    private float ventReadyAt;
    private float nextInputAt;
    private int generation;
    internal static bool Supports(RoleId role) => role is RoleId.Moving or RoleId.NiceTeleporter or RoleId.Doorr or RoleId.Santa or RoleId.Nun or RoleId.Pteranodon;
    internal static bool UsesEngineer(RoleId role) => role is RoleId.Moving or RoleId.NiceTeleporter or RoleId.Santa or RoleId.Pteranodon;
    internal float Remaining => ventQueued ? Math.Max(0f, ventReadyAt - Time.unscaledTime) : timer.Remaining(Time.unscaledTime);
    private float Cooldown => Player.Role switch
    {
        RoleId.Moving => Moving.MovingCooldown,
        RoleId.NiceTeleporter => NiceTeleporter.NiceTeleporterCooldown,
        RoleId.Santa => Santa.SantaAbilityCooldown,
        RoleId.Nun => Nun.NunCoolTime,
        RoleId.Pteranodon => Pteranodon.PteranodonCoolTime,
        _ => Doorr.DoorrCoolTime,
    };

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || !SHRPlayerOptions.IsVanillaGuest(player.Player) || !Supports(player.Role)
            || player.GetAbility<SHRCrewActionAbility>() != null) return;
        player.AttachAbility(new SHRCrewActionAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(UsesEngineer(Player.Role) ? SHRAbilityTrigger.Pet : SHRAbilityTrigger.Phantom,
            _ => Activate(), CanUse), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(PreCalledMeetingEvent.Instance, _ => Reset());
        SubscribeWithAbility(WrapUpEvent.Instance, _ => Reset());
    }

    private bool CanUse() => initialized && Player.IsAlive() && !ventQueued && !timer.Pending && Remaining <= 0f
        && Time.unscaledTime >= nextInputAt && (Player.Role != RoleId.Doorr || Player.GetAbility<DoorrAbility>()?.CanUseSHR == true)
        && (Player.Role != RoleId.Santa || Player.GetAbility<SantaAbility>()?.FindTargetSHR() != null);

    internal static float CooldownAfterReset(RoleId role, float cooldown, InitialCooldownType setting, bool starting)
        => role == RoleId.Nun && !starting ? cooldown : SHRKillRequests.InitialCooldown(cooldown, setting);

    private void Reset(bool starting = false)
    {
        generation++;
        ventQueued = false;
        timer.Reset(Time.unscaledTime, CooldownAfterReset(Player.Role, Cooldown, GameSettingOptions.InitialCooldown, starting));
        Send();
    }

    private void Send() => SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: Player.IsAlive());

    internal void Activate()
    {
        if (!CanUse()) return;
        nextInputAt = Time.unscaledTime + 0.35f;
        if (Player.Role == RoleId.Pteranodon)
        {
            if (Player.GetAbility<PteranodonAbility>()?.TryStartSHR() != true) return;
            timer.Reset(Time.unscaledTime, Cooldown);
        }
        else if (Player.Role == RoleId.Nun)
        {
            if (Player.GetAbility<NunAbility>()?.TryUseSHR() != true) return;
            timer.Reset(Time.unscaledTime, Cooldown);
        }
        else if (Player.Role == RoleId.Santa)
        {
            var santa = Player.GetAbility<SantaAbility>();
            if (santa?.TryGiveSHR(santa.FindTargetSHR()) != true) return;
            timer.Reset(Time.unscaledTime, Cooldown);
        }
        else if (Player.Role == RoleId.NiceTeleporter)
        {
            timer.Queue(Time.unscaledTime, NiceTeleporter.NiceTeleporterWaitingTime);
            CompleteTeleport();
        }
        else if (Player.Role == RoleId.Moving)
        {
            if (!storedPosition.HasValue) storedPosition = Player.transform.position;
            else
            {
                if (!SHRVanillaRpcRouter.Teleport(Player, storedPosition.Value)) return;
                storedPosition = null;
            }
            timer.Reset(Time.unscaledTime, Cooldown);
        }
        else
        {
            if (Player.GetAbility<DoorrAbility>()?.TryUseSHR() != true) return;
            timer.Reset(Time.unscaledTime, Cooldown);
        }
        Send();
        SHRRoleNameDisplay.Refresh();
    }

    private void CompleteTeleport()
    {
        if (!timer.Consume(Time.unscaledTime, Cooldown)) return;
        Player.GetAbility<TeleporterAbility>()?.TryTeleportSHR();
        Send();
    }

    internal void ActivateFromVent()
    {
        if (!CanUse()) return;
        ventQueued = true;
        ventReadyAt = Time.unscaledTime + 0.9f;
        int current = generation;
        int gameId = AmongUsClient.Instance.GameId;
        Send();
        new LateTask(() =>
        {
            if (current != generation || AmongUsClient.Instance?.GameId != gameId
                || Player.GetAbility<SHRCrewActionAbility>() != this) return;
            ventQueued = false;
            if (SHRAbilityTriggerPatches.CanActivate(Player.Player)) Activate();
        }, 0.9f, "SHR crew action after vent exit");
    }

    private void Tick()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (!initialized) { initialized = true; Reset(starting: true); }
        if (Player.IsDead() || MeetingHud.Instance != null || ExileController.Instance != null)
        {
            if (timer.Pending || ventQueued) Reset();
            return;
        }
        if (timer.Pending && timer.Remaining(Time.unscaledTime) <= 0f)
        {
            if (SHRAbilityTriggerPatches.CanActivate(Player.Player)) CompleteTeleport();
            else { timer.Reset(Time.unscaledTime, Cooldown); Send(); }
        }
    }

    public override void DetachToAlls() { generation++; ventQueued = false; }
    internal string Status => Player.Role switch
    {
        RoleId.Moving when storedPosition.HasValue => " [" + ModTranslation.GetString("MovingPositionSetText") + "]",
        RoleId.Santa when Player.GetAbility<SantaAbility>() is { } santa => " [" + string.Format(ModTranslation.GetString("RemainingText"), santa._count) + "]",
        _ => "",
    };
}
