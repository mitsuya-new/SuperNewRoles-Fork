using System.Linq;
using System.Collections.Generic;
using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using SuperNewRoles.Modules;

namespace SuperNewRoles.Mode;

public static class SHRAntiBlackout
{
    public static bool Active { get; private set; }
    private static int generation;
    private static int gameId;
    private static bool completing;
    private static bool deferredExile;
    private static NetworkedPlayerInfo realExiled;
    private static readonly HashSet<byte> DeferredDeaths = new();
    private static readonly HashSet<byte> DeferredRevives = new();

    private static bool IsHostSHR => AmongUsClient.Instance?.AmHost == true
        && ModeManager.IsMode(ModeId.SuperHostRoles);

    internal static bool ShouldDeferExile(int playerCount, bool hasExile)
        => hasExile && playerCount < 4;

    internal static RoleTypes TemporaryRole(byte subject, byte impostor)
        => subject == impostor ? RoleTypes.Impostor : RoleTypes.Crewmate;

    internal static bool ShouldRouteForcedClose(bool isHostSHR, bool transitionActive, bool hasVanillaGuests,
        MeetingHud.MeetingStates state)
        => isHostSHR && !transitionActive && hasVanillaGuests
            && state is not (MeetingHud.MeetingStates.Results or MeetingHud.MeetingStates.Proceeding);

    internal static byte ForcedVoteTarget(bool dead) => dead ? (byte)252 : (byte)253;

    private static bool Begin(NetworkedPlayerInfo exiled, bool tie, Il2CppStructArray<MeetingHud.VoterState> states)
    {
        if (!IsHostSHR || Active) return false;
        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p?.Data?.Role != null && !p.Data.Disconnected).ToArray();
        if (!players.Any(SHRPlayerOptions.IsVanillaGuest)) return false;
        SHRMeetingChat.Restore();
        realExiled = tie ? null : exiled;
        deferredExile = ShouldDeferExile(players.Length, realExiled != null);
        SHRBakeryExile.Prepare(exiled, tie, players.Length, states.Select(state => state.VotedForId));
        var dummy = players.FirstOrDefault(p => !p.Data.IsDead && p.PlayerId != realExiled?.PlayerId && p.PlayerId != SHRBakeryExile.CarrierId)
            ?? players.FirstOrDefault(p => p.PlayerId != realExiled?.PlayerId && p.PlayerId != SHRBakeryExile.CarrierId);
        if (dummy == null) return false;
        Active = true;
        completing = false;
        generation++;
        gameId = AmongUsClient.Instance.GameId;
        foreach (var viewer in players.Where(SHRPlayerOptions.IsVanillaGuest))
            foreach (var subject in players)
            {
                var role = TemporaryRole(subject.PlayerId, dummy.PlayerId);
                SHRTestRoleAbility.SendRoleView(subject, viewer, role, allowDuringTransition: true);
                SHRMeetingChat.SendState(viewer, subject, hide: false, overrideRole: role, overrideDead: false);
            }
        Logger.Debug($"Protected meeting transition map={GameOptionsManager.Instance.CurrentGameOptions.MapId} players={players.Length} exile={realExiled?.PlayerId} deferred={deferredExile} roles={string.Join(",", players.Select(p => $"{p.PlayerId}:{((ExPlayerControl)p).Role}"))}", "SHR.AntiBlackout");
        return true;
    }

    internal static void Complete()
    {
        if (!Active || !IsHostSHR || completing) return;
        completing = true;
        Logger.Debug("Host wrap-up received; scheduling role and death restoration", "SHR.AntiBlackout");
        int current = generation;
        new LateTask(() =>
        {
            if (current != generation || !Active) return;
            if (!IsHostSHR || AmongUsClient.Instance.GameId != gameId
                || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
            {
                Clear();
                return;
            }
            Restore();
        }, 0.7f, "SHR restore meeting transition");
    }

    internal static bool DeferExile(PlayerControl player)
    {
        if (!Active || !IsHostSHR) return false;
        DeferredDeaths.Add(player.PlayerId);
        var client = AmongUsClient.Instance;
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (viewer == null || viewer.AmOwner || viewer.Data == null || viewer.Data.Disconnected
                || SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            int id = client.GetClientIdFromCharacter(viewer);
            if (id < 0) continue;
            var writer = client.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, id);
            client.FinishRpcImmediately(writer);
        }
        return true;
    }

    internal static void RegisterNativeExile(PlayerControl player)
    {
        if (Active && IsHostSHR && player?.Data?.IsDead == true) DeferredDeaths.Add(player.PlayerId);
    }

    internal static bool DeferRevive(PlayerControl player)
    {
        if (!Active || !IsHostSHR || player?.Data == null || player.Data.IsDead || player.Data.Disconnected) return false;
        DeferredRevives.Add(player.PlayerId);
        return true;
    }

    internal static bool RouteRoleUpdate(PlayerControl subject, RoleTypes role, bool canOverrideRole)
    {
        if (!Active || !IsHostSHR) return false;
        foreach (var viewer in PlayerControl.AllPlayerControls)
            if (viewer?.Data != null && !viewer.Data.Disconnected && !viewer.AmOwner
                && !SHRPlayerOptions.IsVanillaGuest(viewer))
                SHRTestRoleAbility.SendRoleView(subject, viewer, role);
        subject.StartCoroutine(subject.CoSetRole(role, canOverrideRole));
        return true;
    }

    private static void Restore()
    {
        var client = AmongUsClient.Instance;
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            if (deferredExile && realExiled != null) DeferredDeaths.Add(realExiled.PlayerId);
            foreach (var subject in PlayerControl.AllPlayerControls)
            {
                if (subject?.Data == null || !subject.Data.IsDead || !DeferredDeaths.Contains(subject.PlayerId)) continue;
                int id = client.GetClientIdFromCharacter(viewer);
                if (id >= 0)
                {
                    var writer = client.StartRpcImmediately(subject.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, id);
                    client.FinishRpcImmediately(writer);
                }
            }
            foreach (var subject in PlayerControl.AllPlayerControls)
            {
                if (subject?.Data?.Role == null || subject.Data.Disconnected) continue;
                if (DeferredRevives.Contains(subject.PlayerId) && !subject.Data.IsDead)
                {
                    SHRReviveSync.RestoreViewer(subject, viewer);
                    continue;
                }
                SHRTestRoleAbility.SendRoleView(subject, viewer, SHRMeetingChat.GetCurrentViewRole(viewer, subject), allowDuringTransition: true);
                SHRMeetingChat.SendState(viewer, subject, hide: false);
            }
            if (GameData.Instance != null)
                foreach (var data in GameData.Instance.AllPlayers)
                    if (data?.Disconnected == true) SHRMeetingChat.SendDisconnectedState(viewer, data);
            SHRPlayerOptions.Send(viewer, killCooldown: SHRKillRequests.RemainingCooldown(viewer),
                resetKill: true, resetAbility: !viewer.Data.IsDead);
            SHRRedRidingHoodGhosts.Refresh(viewer, force: true);
        }
        SHRBakeryExile.Restore();
        Clear();
        Logger.Debug("Restored meeting roles and death states", "SHR.AntiBlackout");
    }

    internal static void Clear()
    {
        generation++;
        Active = completing = deferredExile = false;
        realExiled = null;
        DeferredDeaths.Clear();
        DeferredRevives.Clear();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcVotingComplete))]
    public static class VotingPatch
    {
        [HarmonyPriority(Priority.VeryLow)]
        public static bool Prefix(MeetingHud __instance, Il2CppStructArray<MeetingHud.VoterState> states,
            NetworkedPlayerInfo exiled, bool tie, bool wasOverruled, ushort overruleNonce)
        {
            if (!Begin(exiled, tie, states)) return true;
            var client = AmongUsClient.Instance;
            foreach (var viewer in PlayerControl.AllPlayerControls)
            {
                if (viewer == null || viewer.AmOwner || viewer.Data == null || viewer.Data.Disconnected) continue;
                int id = client.GetClientIdFromCharacter(viewer);
                if (id < 0) continue;
                if (SHRBakeryExile.SendVotingComplete(__instance, viewer, states, wasOverruled, overruleNonce)) continue;
                bool defer = deferredExile && SHRPlayerOptions.IsVanillaGuest(viewer);
                var writer = client.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.VotingComplete, SendOption.Reliable, id);
                new RpcVotingCompleteMessage(__instance.NetId, states,
                    defer || exiled == null ? byte.MaxValue : exiled.PlayerId,
                    tie || defer, wasOverruled, overruleNonce).SerializeRpcValues(writer);
                client.FinishRpcImmediately(writer);
            }
            __instance.VotingComplete(states, exiled, tie, wasOverruled, overruleNonce);
            return false;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcClose))]
    public static class ForcedClosePatch
    {
        public static bool Prefix(MeetingHud __instance)
        {
            if (!ShouldRouteForcedClose(IsHostSHR, Active,
                PlayerControl.AllPlayerControls.ToArray().Any(SHRPlayerOptions.IsVanillaGuest), __instance.state)) return true;
            var states = __instance.playerStates.Select(area => new MeetingHud.VoterState
            {
                VoterId = area.PlayerId,
                VotedForId = ForcedVoteTarget(GameData.Instance.GetPlayerById(area.PlayerId)?.IsDead != false),
            }).ToArray();
            Logger.Debug($"Routing forced close through voting completion state={__instance.state}", "SHR.AntiBlackout");
            __instance.RpcVotingComplete(states, null, true, false, 0);
            return false;
        }
    }

    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.IsGameOverDueToDeath))]
    public static class DeathCheckPatch
    {
        public static bool Prefix(ref bool __result)
        {
            if (!ModeManager.IsMode(ModeId.SuperHostRoles)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    public static class StartPatch { public static void Prefix() { SHRBakeryExile.Clear(); Clear(); } }
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class EndPatch { public static void Prefix() { SHRBakeryExile.Restore(); SHRBakeryExile.Clear(); Clear(); } }
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
    public static class DisconnectPatch { public static void Prefix() { SHRBakeryExile.Clear(); Clear(); } }
}
