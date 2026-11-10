using System.Linq;
using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Patches;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRTestRoleAbility : AbilityBase
{
    private bool configured;
    private float readyAt;
    private RoleTypes originalRole;
    private static bool roundReady;
    internal bool SidekickCompleted { get; private set; }
    internal bool PhantomAbilityCompleted => SidekickCompleted
        || (Player.Role == RoleId.SideKiller && Player.GetAbility<SideKillerAbility>()?.madKillerAbility != null)
        || (Player.Role == RoleId.Samurai && Player.GetAbility<AreaKillButtonAbility>()?.IsUsed == true);
    internal bool TaskerImpostor { get; private set; }
    private int lastTrackerSecond = -1;
    private static bool IsHostSHR => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
        && ModeManager.IsMode(ModeId.SuperHostRoles);

    internal static SHRAbilityTrigger TriggerFor(RoleId role) => role switch
    {
        RoleId.SelfBomber => SHRAbilityTrigger.Phantom,
        RoleId.Samurai => SHRAbilityTrigger.Phantom,
        RoleId.WaveCannon => SHRAbilityTrigger.Phantom,
        RoleId.Jackal => SHRAbilityTrigger.Phantom,
        RoleId.SideKiller => SHRAbilityTrigger.Phantom,
        RoleId.Penguin => SHRAbilityTrigger.Phantom,
        RoleId.SuicideWisher => SHRAbilityTrigger.Phantom,
        _ => 0,
    };

    internal static bool NeedsVanillaControls(bool isOwner, bool hasSnr)
        => !isOwner && !hasSnr;

    private static bool NeedsVanillaControls(ExPlayerControl player)
    {
        bool hasSnr = SyncVersion.VersionData.VersionMap.TryGetValue(player.PlayerId, out string version)
            && !string.IsNullOrEmpty(version);
        return NeedsVanillaControls(player.AmOwner, hasSnr);
    }

    public static void AttachIfSupported(ExPlayerControl player)
    {
        SHRBatchRoleAbility.AttachIfSupported(player);
        SHRCrewRoleAbility.AttachIfSupported(player);
        SHRCrewBoostAbility.AttachIfSupported(player);
        SHRCrewActionAbility.AttachIfSupported(player);
        SHRConjurerAbility.AttachIfSupported(player);
        SHREvilSeerAbility.AttachIfSupported(player);
        SHRBansheeAbility.AttachIfSupported(player);
        SHRRocketAbility.AttachIfSupported(player);
        SHRPusherAbility.AttachIfSupported(player);
        SHREvilHackerAbility.AttachIfSupported(player);
        if (!IsHostSHR || !NeedsVanillaControls(player) || TriggerFor(player.Role) == 0) return;
        if (player.Role == RoleId.Tasker && !Tasker.TaskerCanKill) return;
        Logger.Debug($"Binding player={player.PlayerId} role={player.Role} trigger={TriggerFor(player.Role)}", "SHR.Controls");
        player.AttachAbility(new SHRTestRoleAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        float cooldown = Player.Role switch
        {
            RoleId.SelfBomber => SelfBomber.SelfBomberCooldown,
            RoleId.Samurai => Samurai.SamuraiKillCooldown,
            RoleId.WaveCannon => WaveCannon.WaveCannonCooldown,
            RoleId.Jackal => Player.GetAbility<JackalAbility>()?.JackData.SidekickCooldown ?? 0f,
            RoleId.SideKiller => SideKiller.SideKillerKillCooldown,
            RoleId.Penguin => Penguin.PenguinCooldown,
            RoleId.SuicideWisher => SuicideWisher.SuicideWisherCoolTime,
            _ => 0f,
        };
        var trigger = new SHRAbilityTriggerAbility(TriggerFor(Player.Role), _ => Use(),
            CanUse, cooldown);
        Player.AttachAbility(trigger, new AbilityParentAbility(this));
        SubscribeWithAbility(WrapUpEvent.Instance, _ => ResetInitialCooldown(cooldown));
        if (Player.Role == RoleId.Tasker)
            SubscribeWithAbility(FixedUpdateEvent.Instance, () =>
            {
                if (!configured || !Player.IsAlive() || TaskerImpostor || !Tasker.TaskerCanKill
                    || MeetingHud.Instance != null || ExileController.Instance != null) return;
                int second = Mathf.CeilToInt(SHRKillRequests.RemainingCooldown(Player.Player));
                if (second == lastTrackerSecond) return;
                lastTrackerSecond = second;
                SHRPlayerOptions.Send(Player.Player);
            });
        if (Player.Role == RoleId.SideKiller)
            SubscribeWithAbility(MurderEvent.Instance, data =>
            {
                if (data.killer != Player || data.target == null || data.target == Player || !data.target.IsDead()
                    || !data.resultFlags.HasFlag(MurderResultFlags.Succeeded) || PhantomAbilityCompleted) return;
                readyAt = Time.unscaledTime + cooldown;
                trigger.ResetCooldown(Time.unscaledTime, cooldown);
                SHRPlayerOptions.Send(Player.Player, abilityCooldown: cooldown, resetAbility: true);
            });
        SubscribeWithAbility(IntroCutsceneInitializeEvent.Instance, () =>
        {
            roundReady = true;
            Configure();
        });
        if (roundReady) Configure();
    }

    private void ResetInitialCooldown(float cooldown)
    {
        readyAt = Time.unscaledTime + (Player.Role == RoleId.Tasker ? 0f : GameSettingOptions.InitialCooldown switch
        {
            InitialCooldownType.Immediate => 0f,
            InitialCooldownType.OneThird => cooldown / 3f,
            _ => 10f,
        });
        Player.GetAbility<SHRAbilityTriggerAbility>()?.ResetCooldown(Time.unscaledTime, readyAt - Time.unscaledTime);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Mathf.Max(0f, readyAt - Time.unscaledTime), resetAbility: true);
    }

    private bool CanUse()
    {
        bool ready = configured && Time.unscaledTime >= readyAt && Player.IsAlive();
        bool needsTarget = Player.Role is RoleId.Jackal or RoleId.SideKiller;
        var target = needsTarget && ready ? FindSidekickTarget() : null;
        bool available = ready && (!needsTarget || (!PhantomAbilityCompleted && target != null));
        if (Player.Role == RoleId.Tasker) available &= Tasker.TaskerCanKill;
        if (Player.Role == RoleId.Samurai)
            available &= Player.GetAbility<AreaKillButtonAbility>() is { IsUsed: false } area && area.CanKill();
        if (Player.Role == RoleId.WaveCannon)
            available &= Player.GetAbility<WaveCannonAbility>()?.SHRController?.CanStart == true;
        if (Player.Role == RoleId.Penguin)
            available &= Player.GetAbility<PenguinAbility>()?.FindSHRTarget() != null;
        Logger.Debug($"Ability availability player={Player.PlayerId} role={Player.Role} configured={configured} ready={ready} target={target?.PlayerId} allowed={available}", "SHR.Controls");
        return available;
    }

    private ExPlayerControl FindSidekickTarget()
    {
        var sidekick = Player.Role == RoleId.SideKiller
            ? Player.GetAbility<SideKillerAbility>()?.SidekickAbility
            : Player.GetAbility<JackalAbility>()?.SidekickAbility;
        if (sidekick == null) return null;
        return TargetCustomButtonBase.SetTarget(targetingPlayer: Player.Player,
            isTargetable: sidekick.CanCreateSidekickFor);
    }

    private void Use()
    {
        switch (Player.Role)
        {
            case RoleId.SuicideWisher:
                SuicideWisherAbility.RpcSuicide(Player);
                break;
            case RoleId.Penguin:
                Player.GetAbility<PenguinAbility>()?.TryStartSHR();
                break;
            case RoleId.SideKiller:
                var madKillerTarget = FindSidekickTarget();
                if (madKillerTarget != null && Player.GetAbility<SideKillerAbility>().SidekickAbility.TryCreateSidekick(madKillerTarget))
                {
                    SidekickCompleted = true;
                    SetSelfRole(Player.Player, RoleTypes.Impostor);
                    SHRKillRequests.SetRemainingCooldown(Player.Player, SideKiller.SideKillerKillCooldown);
                }
                break;
            case RoleId.WaveCannon:
                Player.GetAbility<WaveCannonAbility>()?.SHRController?.TryStart();
                break;
            case RoleId.Samurai:
                if (Player.GetAbility<AreaKillButtonAbility>()?.TryUseAreaKill(invokeCallback: false) == true)
                {
                    Samurai.RpcSamuraiAnimation(Player);
                    SetSelfRole(Player.Player, RoleTypes.Impostor);
                }
                break;
            case RoleId.Tasker:
                if (!Tasker.TaskerCanKill) break;
                float taskerKillCooldown = SHRKillRequests.RemainingCooldown(Player.Player);
                TaskerImpostor = !TaskerImpostor;
                SetSelfRole(Player.Player, SHRRoleTypes.GetTaskerRole(Tasker.TaskerCanKill, TaskerImpostor));
                SHRPlayerOptions.Send(Player.Player, killCooldown: taskerKillCooldown, resetKill: true, resetAbility: !TaskerImpostor);
                Logger.Debug($"Tasker pet switch player={Player.PlayerId} impostor={TaskerImpostor} killCooldown={taskerKillCooldown}", "SHR.Controls");
                break;
            case RoleId.SelfBomber:
                Explode();
                break;
            case RoleId.Jackal:
                var target = FindSidekickTarget();
                if (target != null && Player.GetAbility<JackalAbility>().SidekickAbility.TryCreateSidekick(target))
                {
                    SendRoleToAll(target.Player, RoleTypes.Crewmate);
                    SidekickCompleted = true;
                    SetSelfRole(Player.Player, RoleTypes.Impostor);
                }
                break;
        }
    }

    private void Explode()
    {
        var victims = ExPlayerControl.ExPlayerControls.Where(p => p != Player && p.IsAlive()
            && (!SelfBomber.SelfBomberOnlyKillCrewmates || !p.IsImpostor())
            && Vector2.Distance(Player.Player.GetTruePosition(), p.Player.GetTruePosition()) <= SelfBomber.SelfBomberExplosionRadius).ToArray();
        foreach (var victim in victims) VanillaMurder(Player, victim);
        VanillaMurder(Player, Player);
    }

    private static void VanillaMurder(ExPlayerControl source, ExPlayerControl target)
        => SHRMurderSync.Murder(source, target, explosion: true);

    private void Configure()
    {
        if (configured || Player.Data?.Role == null || !Player.IsAlive() || !NeedsVanillaControls(Player)) return;
        Logger.Debug($"Configuring player={Player.PlayerId} role={Player.Role}", "SHR.Controls");
        originalRole = Player.Data.Role.Role;
        configured = true;
        SHRPlayerOptions.Send(Player.Player);
        switch (Player.Role)
        {
            case RoleId.WaveCannon:
                SendRoleView(Player.Player, Player.Player, RoleTypes.Phantom);
                ResetInitialCooldown(WaveCannon.WaveCannonCooldown);
                break;
            case RoleId.Samurai:
                SendRoleView(Player.Player, Player.Player, RoleTypes.Phantom);
                ResetInitialCooldown(Samurai.SamuraiKillCooldown);
                break;
            case RoleId.Tasker:
                SetSelfRole(Player.Player, SHRRoleTypes.GetTaskerRole(Tasker.TaskerCanKill, false));
                SHRPlayerOptions.Send(Player.Player, killCooldown: SHRKillRequests.RemainingCooldown(Player.Player),
                    resetKill: true, resetAbility: true);
                break;
            case RoleId.SelfBomber:
                SendRoleView(Player.Player, Player.Player, RoleTypes.Phantom);
                ResetInitialCooldown(SelfBomber.SelfBomberCooldown);
                break;
            case RoleId.Jackal:
                SetSelfRole(Player.Player, RoleTypes.Phantom);
                ResetInitialCooldown(Player.GetAbility<JackalAbility>().JackData.SidekickCooldown);
                break;
            case RoleId.SideKiller:
                SetSelfRole(Player.Player, RoleTypes.Phantom);
                ResetInitialCooldown(SideKiller.SideKillerKillCooldown);
                break;
            case RoleId.Penguin:
                SetSelfRole(Player.Player, RoleTypes.Phantom);
                ResetInitialCooldown(Penguin.PenguinCooldown);
                break;
            case RoleId.SuicideWisher:
                SetSelfRole(Player.Player, RoleTypes.Phantom);
                ResetInitialCooldown(SuicideWisher.SuicideWisherCoolTime);
                break;
        }
    }

    internal static void SetSelfRole(PlayerControl subject, RoleTypes role)
    {
        ExPlayerControl player = subject;
        if (player == null || !NeedsVanillaControls(player)) return;
        if (!subject.AmOwner && !player.IsImpostor() && role is RoleTypes.Phantom or RoleTypes.Impostor)
        {
            foreach (var other in PlayerControl.AllPlayerControls)
                if (other != subject && other.Data?.Role != null && other.Data.Role.IsImpostor)
                    SendRoleView(other, subject, RoleTypes.Scientist);
        }
        SendRoleView(subject, subject, role);
    }

    internal static void SendRoleView(PlayerControl subject, PlayerControl viewer, RoleTypes role, bool allowDuringTransition = false)
    {
        if (viewer.AmOwner) return;
        if (SHRAntiBlackout.Active && !allowDuringTransition && SHRPlayerOptions.IsVanillaGuest(viewer)) return;
        int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
        if (id < 0)
        {
            Logger.Debug($"Role view not sent: missing client ID for viewer={viewer.PlayerId}", "SHR.Controls");
            return;
        }
        var writer = AmongUsClient.Instance.StartRpcImmediately(subject.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, id);
        new RpcSetRoleMessage(subject.NetId, role, true).SerializeRpcValues(writer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        Logger.Debug($"Role view sent subject={subject.PlayerId} viewer={viewer.PlayerId} client={id} role={role}", "SHR.Controls");
    }

    internal static void SendRoleToAll(PlayerControl subject, RoleTypes role)
    {
        var writer = AmongUsClient.Instance.StartRpcImmediately(subject.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, -1);
        new RpcSetRoleMessage(subject.NetId, role, true).SerializeRpcValues(writer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        RoleManager.Instance.SetRole(subject, role);
    }

    private void Restore()
    {
        if (!IsHostSHR || Player?.Data == null || Player.Data.Disconnected) return;
        if (configured && Player.IsAlive()) SendRoleView(Player.Player, Player.Player, originalRole);
        configured = false;
    }

    public override void DetachToAlls() => Restore();

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    public static class IntroPatch
    {
        public static void Postfix()
        {
            if (!IsHostSHR) return;
            roundReady = true;
            foreach (var player in ExPlayerControl.ExPlayerControls.ToArray())
                player.GetAbility<SHRTestRoleAbility>()?.Configure();
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    public static class StartPatch
    {
        public static void Prefix()
        {
            roundReady = false;
            SHRKillRequests.Reset();
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class EndPatch
    {
        public static void Prefix()
        {
            if (!IsHostSHR) return;
            roundReady = false;
            foreach (var player in ExPlayerControl.ExPlayerControls.ToArray())
                player.GetAbility<SHRTestRoleAbility>()?.Restore();
        }
    }
}
