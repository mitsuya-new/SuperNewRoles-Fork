using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Crewmate;

namespace SuperNewRoles.Mode;

internal sealed class SHRMeetingSheriffVoteState
{
    internal enum Action { Vote, Arm, Cancel, Shoot }
    internal bool Armed { get; private set; }
    internal Action Receive(byte source, byte target)
    {
        if (source == target) { Armed = true; return Action.Arm; }
        if (!Armed) return Action.Vote;
        Armed = false;
        return target >= 252 ? Action.Cancel : Action.Shoot;
    }
}

public static class SHRMeetingSheriffVotes
{
    private static readonly Dictionary<byte, SHRMeetingSheriffVoteState> States = new();

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
    public static class VotePatch
    {
        public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] PlayerId source, [HarmonyArgument(1)] PlayerId target)
        {
            if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || BalancerAbility.BalancingAbility != null) return true;
            var player = ExPlayerControl.ById(source);
            if (player?.Role != RoleId.MeetingSheriff || !player.IsAlive() || player.Data.Disconnected) return true;
            var ability = player.GetAbility<MeetingSheriffAbility>();
            if (ability == null) return true;
            if (!States.TryGetValue(source, out var state)) States[source] = state = new();
            if (!state.Armed && !ability.CanShootSHR) return true;
            var action = state.Receive(source, target);
            if (action == SHRMeetingSheriffVoteState.Action.Vote) return true;
            __instance.RpcClearVote(source);
            if (action == SHRMeetingSheriffVoteState.Action.Shoot)
                ability.TryShootSHR(ExPlayerControl.ById(target));
            return false;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    public static class ResetPatch
    {
        public static void Prefix() => States.Clear();
    }
}
