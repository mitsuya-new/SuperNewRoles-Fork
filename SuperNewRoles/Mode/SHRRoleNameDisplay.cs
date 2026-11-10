using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Patches;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRRoleNameDisplay
{
    private sealed record SentName(PlayerControl Player, string Original, string Display);
    private static readonly Dictionary<byte, SentName> Sent = new();
    private static readonly List<byte> Remove = new();
    private static float nextUpdate;
    private static bool meetingPending;

    internal static string GetSelfDisplayName(PlayerControl player)
        => Sent.TryGetValue(player.PlayerId, out var entry) && entry.Player == player
            ? entry.Display : player.Data.PlayerName;

    internal static string FormatName(string role, string name, bool inMeeting)
        => string.IsNullOrEmpty(name) ? $"<size=75%>{role}</size>" : inMeeting
            ? $"{name}\n<size=75%>{role}</size>"
            : $"<size=75%>{role}</size>\n{name}";

    internal static void Refresh() => nextUpdate = 0f;

    private static bool IsUnmodded(PlayerControl player)
        => !SyncVersion.VersionData.VersionMap.TryGetValue(player.PlayerId, out string version)
            || string.IsNullOrEmpty(version);

    public static void Restore()
    {
        SHRDatahackerInformation.Clear();
        SHRMadKillerSupport.Restore();
        var client = AmongUsClient.Instance;
        if (client != null && client.AmHost)
        {
            foreach (var entry in Sent.Values)
            {
                var player = entry.Player;
                if (player == null || player.Data == null || player.Data.Disconnected || player.AmOwner) continue;
                if (client.GetClientIdFromCharacter(player) < 0) continue;
                player.RpcSetNamePrivate(player.Data.PlayerName ?? entry.Original, player);
            }
        }
        Sent.Clear();
        nextUpdate = 0f;
    }

    private static void Update()
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost)
        {
            Sent.Clear();
            return;
        }
        if (!ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNetClient.GameStates.Started)
        {
            if (Sent.Count > 0) Restore();
            return;
        }
        if (SHRAntiBlackout.Active || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 0.5f;
        SHRMadKillerSupport.Update();

        Remove.Clear();
        foreach (var pair in Sent)
            if (pair.Value.Player == null || pair.Value.Player.Data == null || pair.Value.Player.Data.Disconnected)
                Remove.Add(pair.Key);
        foreach (byte id in Remove) Sent.Remove(id);

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player.AmOwner || player.Data == null || player.Data.Disconnected
                || player.Data.Role == null || client.GetClientIdFromCharacter(player) < 0) continue;
            ExPlayerControl exPlayer = player;
            if (exPlayer == null) continue;
            if (SHRWaveCannonController.IsDisplaying(player.PlayerId) && MeetingHud.Instance == null) continue;
            string roleInfo = NameText.GetRoleInfoText(exPlayer);
            foreach (var hide in exPlayer.GetAbilities<HideMyRoleWhenAliveAbility>())
                hide.DisplayFalseRoleNameWhileAlive(exPlayer, ref roleInfo);
            string original = SHRCamouflagerController.OriginalName(player);
            bool masked = SHRCamouflagerController.IsMasked(player) && !meetingPending && MeetingHud.Instance == null;
            string display = IsUnmodded(player)
                ? FormatName(roleInfo + (masked ? "" : (exPlayer.GetAbility<SHRBatchRoleAbility>()?.Status ?? "")
                    + (exPlayer.GetAbility<SHRCrewRoleAbility>()?.Status ?? ""))
                    + (masked ? "" : exPlayer.GetAbility<SHRCrewActionAbility>()?.Status ?? "")
                    + (masked ? "" : exPlayer.GetAbility<SHRConjurerAbility>()?.Status ?? "")
                    + (masked ? "" : exPlayer.GetAbility<SHREvilSeerAbility>()?.Status ?? "")
                    + (masked ? "" : exPlayer.GetAbility<SHRBansheeAbility>()?.Status ?? "")
                    + (masked ? "" : exPlayer.GetAbility<SHRRocketAbility>()?.Status ?? "")
                    + NameText.GetTaskProgressText(exPlayer)
                    + (masked ? "" : exPlayer.GetAbility<SHREvilHackerAbility>()?.Status ?? "")
                    + (masked ? "" : SHRDatahackerInformation.SelfArrows(exPlayer)), masked ? "" : original, meetingPending || MeetingHud.Instance != null)
                : original;
            if (Sent.TryGetValue(player.PlayerId, out var previous)
                && previous.Player == player && previous.Display == display) continue;
            if (display == original && !Sent.ContainsKey(player.PlayerId)) continue;
            player.RpcSetNamePrivate(display, player);
            Sent[player.PlayerId] = new SentName(player, original, display);
        }
        SHRDatahackerInformation.Update();
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class HudUpdatePatch
    {
        public static void Postfix() => Update();
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    public static class MeetingPatch
    {
        public static void Prefix()
        {
            meetingPending = true;
            nextUpdate = 0f;
            Update();
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    public static class MeetingClosePatch
    {
        public static void Postfix()
        {
            meetingPending = false;
            nextUpdate = 0f;
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class GameEndPatch
    {
        public static void Prefix() => Restore();
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    public static class LobbyPatch
    {
        public static void Prefix()
        {
            Restore();
            meetingPending = false;
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleDisconnect))]
    public static class DisconnectPatch
    {
        public static void Postfix()
        {
            SHRDatahackerInformation.Clear();
            Sent.Clear();
            nextUpdate = 0f;
            meetingPending = false;
        }
    }
}
