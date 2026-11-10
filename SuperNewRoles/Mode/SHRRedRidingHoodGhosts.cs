using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal static class SHRRedRidingHoodGhosts
{
    internal readonly record struct Step(RoleTypes Role, bool? Dead);
    internal static IEnumerable<Step> WaitingSteps()
    {
        yield return new(RoleTypes.CrewmateGhost, false);
        yield return new(RoleTypes.CrewmateGhost, null);
        yield return new(RoleTypes.Crewmate, false);
        yield return new(RoleTypes.Crewmate, null);
        yield return new(RoleTypes.Crewmate, true);
    }

    private sealed class View
    {
        internal PlayerControl Player;
        internal bool Locked;
        internal readonly Dictionary<byte, RoleTypes> Roles = new();
    }

    private static readonly Dictionary<byte, View> Views = new();
    private static float nextUpdate;
    private static int gameId;
    internal static bool ShouldHide(bool dead, bool revivable, int remaining)
        => dead && revivable && remaining > 0;
    internal static bool HidesInformation(PlayerControl viewer)
    {
        if (!SHRPlayerOptions.IsVanillaGuest(viewer)) return false;
        ExPlayerControl ex = viewer;
        var ability = ex?.GetAbility<NiceRedRidingHoodReviveAbility>();
        return ability != null && ShouldHide(viewer.Data.IsDead, ability.IsRevivable, ability.RemainingReviveCount);
    }

    internal static RoleTypes HiddenRole(bool self, bool dead)
        => self || !dead ? RoleTypes.Crewmate : RoleTypes.CrewmateGhost;

    internal static void Refresh(PlayerControl viewer, bool force = false)
    {
        var client = AmongUsClient.Instance;
        if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver
            || !SHRPlayerOptions.IsVanillaGuest(viewer) || SHRAntiBlackout.Active && !force) return;
        if (gameId != client.GameId) { Views.Clear(); gameId = client.GameId; }
        bool hidden = HidesInformation(viewer);
        if (!Views.TryGetValue(viewer.PlayerId, out var state) || state.Player != viewer)
        {
            if (!hidden) return;
            state = new View { Player = viewer };
            Views[viewer.PlayerId] = state;
        }
        if (!hidden)
        {
            foreach (var subject in PlayerControl.AllPlayerControls)
            {
                if (subject?.Data?.Role == null || subject.Data.Disconnected) continue;
                SHRTestRoleAbility.SendRoleView(subject, viewer, SHRMeetingChat.GetCurrentViewRole(viewer, subject), allowDuringTransition: force);
                SHRMeetingChat.SendState(viewer, subject, hide: false);
            }
            Views.Remove(viewer.PlayerId);
            return;
        }
        if (!state.Locked || force)
        {
            foreach (var step in WaitingSteps())
                if (step.Dead.HasValue)
                    SHRMeetingChat.SendState(viewer, viewer, hide: false, overrideRole: step.Role, overrideDead: step.Dead);
                else SHRTestRoleAbility.SendRoleView(viewer, viewer, step.Role, allowDuringTransition: force);
            state.Locked = true;
            state.Roles.Clear();
        }
        foreach (var subject in PlayerControl.AllPlayerControls)
        {
            if (subject == viewer || subject?.Data?.Role == null || subject.Data.Disconnected) continue;
            RoleTypes role = HiddenRole(false, subject.Data.IsDead);
            if (!force && state.Roles.TryGetValue(subject.PlayerId, out var previous) && previous == role) continue;
            SHRMeetingChat.SendState(viewer, subject, hide: false, overrideRole: role);
            SHRTestRoleAbility.SendRoleView(subject, viewer, role, allowDuringTransition: force);
            state.Roles[subject.PlayerId] = role;
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class UpdatePatch
    {
        public static void Postfix()
        {
            var client = AmongUsClient.Instance;
            if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver)
            {
                Views.Clear();
                return;
            }
            if (SHRAntiBlackout.Active)
            {
                foreach (var state in Views.Values) state.Locked = false;
                return;
            }
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.5f;
            foreach (var viewer in PlayerControl.AllPlayerControls) Refresh(viewer);
        }
    }
}
