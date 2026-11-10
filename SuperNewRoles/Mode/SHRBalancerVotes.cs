using System.Collections.Generic;
using System;
using System.Linq;
using HarmonyLib;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Crewmate;
using UnityEngine;
using AmongUs.GameOptions;

namespace SuperNewRoles.Mode;

internal sealed class SHRBalancerSelection
{
    internal enum Action { Vote, Arm, First, Duplicate, Cancel, Start }
    internal bool Armed { get; private set; }
    internal byte? First { get; private set; }
    internal Action Receive(byte source, byte target)
    {
        if (source == target) { Armed = true; First = null; return Action.Arm; }
        if (!Armed) return Action.Vote;
        if (target >= 252) { Armed = false; First = null; return Action.Cancel; }
        if (!First.HasValue) { First = target; return Action.First; }
        if (First.Value == target) return Action.Duplicate;
        Armed = false;
        return Action.Start;
    }
}

public static class SHRBalancerVotes
{
    private static readonly Dictionary<byte, SHRBalancerSelection> Selections = new();
    private static float meetingStartedAt;
    private static int? nativeVotingTime;
    internal static int? NativeVotingTime => MeetingHud.Instance != null && BalancerAbility.BalancingAbility != null ? nativeVotingTime : null;

    internal static int VotingTimeForElapsed(float elapsed, int discussion, float remaining)
        => Math.Max(1, (int)Math.Ceiling(Math.Max(0f, elapsed) - discussion + Math.Max(0f, remaining)));

    internal static void SyncNativeTime(float remaining)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        int discussion = GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.DiscussionTime);
        nativeVotingTime = VotingTimeForElapsed(Time.unscaledTime - meetingStartedAt, discussion, remaining);
        foreach (var viewer in PlayerControl.AllPlayerControls)
            if (SHRPlayerOptions.IsVanillaGuest(viewer)) SHRPlayerOptions.Send(viewer);
    }
    internal static bool AcceptVote(bool waiting, byte target, byte left, byte right)
        => waiting && (target == left || target == right);

    internal static float Remaining(float now, float deadline) => Math.Max(0f, deadline - now);

    internal static void Announce(PlayerControl left, PlayerControl right, float? seconds = null)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        string text = left.Data.DefaultOutfit.PlayerName + " / " + right.Data.DefaultOutfit.PlayerName
            + "\n" + ModTranslation.GetString(seconds.HasValue ? "BalancerVoteText" : "BalancerAbilityUseText");
        if (seconds.HasValue) text += "\n" + ModTranslation.GetString("BalancerVoteTime") + ": " + Mathf.CeilToInt(seconds.Value);
        foreach (var viewer in PlayerControl.AllPlayerControls)
            if (SHRPlayerOptions.IsVanillaGuest(viewer))
                SHRChatCommands.Reply(viewer, ModTranslation.GetString("Balancer"), text);
    }

    internal static void ClearNativeVotes()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles) || MeetingHud.Instance == null) return;
        Selections.Clear();
        foreach (var viewer in PlayerControl.AllPlayerControls.ToArray())
            if (SHRPlayerOptions.IsVanillaGuest(viewer)) MeetingHud.Instance.RpcClearVote(viewer.PlayerId);
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
    public static class VotePatch
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] PlayerId source, [HarmonyArgument(1)] PlayerId target)
        {
            if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || __instance.state is MeetingHud.MeetingStates.Results or MeetingHud.MeetingStates.Proceeding) return true;
            if (BalancerAbility.BalancingAbility is { } balancing)
            {
                if (balancing.AcceptVoteSHR(target)) return true;
                __instance.RpcClearVote(source);
                return false;
            }
            var player = ExPlayerControl.ById(source);
            if (player?.Role != RoleId.Balancer || !SHRPlayerOptions.IsVanillaGuest(player.Player) || !player.IsAlive()) return true;
            var ability = player.GetAbility<BalancerAbility>();
            if (ability == null || !ability.HasCount || ability.isAbilityUsed) return true;
            if (!Selections.TryGetValue(source, out var selection)) Selections[source] = selection = new();
            var candidate = ExPlayerControl.ById(target);
            if (target < 252 && target != source && (candidate == null || !candidate.IsAlive() || candidate.Data.Disconnected))
            {
                if (!selection.Armed) return true;
                __instance.RpcClearVote(source);
                return false;
            }
            var action = selection.Receive(source, target);
            if (action == SHRBalancerSelection.Action.Vote) return true;
            __instance.RpcClearVote(source);
            if (action == SHRBalancerSelection.Action.Start)
            {
                ability.TryStartSHR(ExPlayerControl.ById(selection.First.Value), candidate);
                Selections.Remove(source);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class ResetPatch
    {
        public static void Prefix()
        {
            Selections.Clear();
            nativeVotingTime = null;
            meetingStartedAt = Time.unscaledTime;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    public static class ClosePatch
    {
        public static void Prefix() { Selections.Clear(); nativeVotingTime = null; }
    }
}
