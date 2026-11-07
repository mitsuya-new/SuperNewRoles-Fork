using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRMadKillerSupport
{
    private static readonly Dictionary<(byte, byte), (PlayerControl Subject, PlayerControl Viewer, string Text)> Names = new();
    private static readonly Dictionary<byte, (PlayerControl Player, float Light, bool Awake, bool Vision)> Options = new();

    internal static float LightFraction()
        => ShipStatus.Instance != null && ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Electrical, out var system)
            && system.TryCast<SwitchSystem>() is { } lights ? lights.Value / 255f : 1f;

    internal static bool CanIdentify(bool awakened, bool hideBeforePromotion)
        => awakened || !hideBeforePromotion;

    internal static bool CanIdentifyImpostors(bool awakened, bool hideBeforePromotion, bool hideImpostors)
        => awakened || !hideBeforePromotion || !hideImpostors;

    internal static void OnCreated(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        SHRTestRoleAbility.SendRoleToAll(player.Player, AmongUs.GameOptions.RoleTypes.Crewmate);
        SHRTestRoleAbility.SetSelfRole(player.Player, SHRRoleTypes.GetMadKillerRole(SideKiller.MadKillerCanUseVent));
        SHRPlayerOptions.Send(player.Player, resetAbility: true);
    }

    internal static void SendAwakenedRole(ExPlayerControl player)
    {
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            var role = viewer == player.Player ? RoleTypes.Impostor
                : SHRRoleAssignment.GetViewRole(RoleId.MadKiller, RoleTypes.Impostor, false,
                    SHRPlayerOptions.IsVanillaGuest(player.Player), ((ExPlayerControl)viewer).Role, true, true);
            SHRTestRoleAbility.SendRoleView(player.Player, viewer, role);
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcVotingComplete))]
    public static class VotingPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Prefix(NetworkedPlayerInfo exiled, bool tie)
        {
            if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || tie || exiled == null) return;
            foreach (var player in ExPlayerControl.ExPlayerControls.ToArray())
                player.GetAbility<MadKillerAbility>()?.PrepareOwnerExile(exiled.PlayerId);
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Exiled))]
    public static class ExiledPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(PlayerControl __instance)
        {
            if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || __instance?.Data == null || !__instance.Data.IsDead) return;
            foreach (var player in ExPlayerControl.ExPlayerControls.ToArray())
                player.GetAbility<MadKillerAbility>()?.AwakenForOwnerExile(__instance.PlayerId);
        }
    }

    internal static void Update()
    {
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer) || viewer.Data.IsDead) continue;
            ExPlayerControl viewerEx = viewer;
            if (viewerEx.Role == RoleId.MadKiller && ShipStatus.Instance != null)
            {
                var state = (viewer, LightFraction(), viewerEx.GetAbility<MadKillerAbility>()?.IsAwakened == true, viewerEx.HasImpostorVision());
                if (!Options.TryGetValue(viewer.PlayerId, out var old) || old != state)
                {
                    Options[viewer.PlayerId] = state;
                    SHRPlayerOptions.Send(viewer);
                }
            }
            foreach (var subject in PlayerControl.AllPlayerControls)
            {
                if (subject == viewer || subject?.Data == null || subject.Data.Disconnected) continue;
                ExPlayerControl target = subject;
                bool recognized = target.Role == RoleId.MadKiller && viewerEx.IsImpostor()
                    && CanIdentify(target.GetAbility<MadKillerAbility>()?.IsAwakened == true, SideKiller.CannotSeeMadKillerBeforePromotion);
                bool showRole = recognized && viewerEx.Role == RoleId.SideKiller;
                recognized |= viewerEx.Role == RoleId.MadKiller && target.IsImpostor()
                    && CanIdentifyImpostors(viewerEx.GetAbility<MadKillerAbility>()?.IsAwakened == true,
                        SideKiller.CannotSeeMadKillerBeforePromotion, SideKiller.MadKillerCannotSeeImpostorBeforePromotion);
                var key = (subject.PlayerId, viewer.PlayerId);
                if (SHRWaveCannonController.IsDisplaying(subject.PlayerId) && MeetingHud.Instance == null)
                {
                    Names.Remove(key);
                    continue;
                }
                if (!recognized && !Names.ContainsKey(key)) continue;
                string display = recognized ? $"<color=#FF1919>{subject.Data.PlayerName}</color>" : subject.Data.PlayerName;
                if (showRole)
                    display = SHRRoleNameDisplay.FormatName(NameText.GetRoleInfoText(target), display, MeetingHud.Instance != null);
                if (Names.TryGetValue(key, out var previous) && previous.Subject == subject
                    && previous.Viewer == viewer && previous.Text == display) continue;
                subject.RpcSetNamePrivate(display, viewer);
                if (recognized) Names[key] = (subject, viewer, display);
                else Names.Remove(key);
            }
        }
    }

    internal static void Restore()
    {
        if (AmongUsClient.Instance?.AmHost == true)
            foreach (var entry in Names.Values)
                if (entry.Subject?.Data != null && !entry.Subject.Data.Disconnected && SHRPlayerOptions.IsVanillaGuest(entry.Viewer))
                    entry.Subject.RpcSetNamePrivate(entry.Subject.Data.PlayerName, entry.Viewer);
        Names.Clear();
        Options.Clear();
    }
}
