using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Crewmate;

namespace SuperNewRoles.Mode;

public static class SHRBakeryExile
{
    private static PlayerControl carrier;
    private static string originalName;
    private static string displayName;
    private static string breadMessage;
    private static bool fakeExile;
    private static int generation;
    internal static byte? CarrierId => carrier?.PlayerId;

    internal static byte? SelectCarrier(bool bakeryAlive, int connectedPlayers, byte? exiled, byte host)
        => !bakeryAlive || connectedPlayers < 4 ? null : exiled ?? host;

    internal static string FormatName(string exileLine, string bread, bool fake, string notice, float baseFontSize = 3f)
        => "<size=" + (baseFontSize * (fake ? 1.25f : 1.15f)).ToString("0.###", CultureInfo.InvariantCulture) + ">" + exileLine + "<br>" + bread
            + (fake ? "<br><sub>" + notice + "</sub>" : "") + "</size><size=0>";

    internal static bool IsTieSkip(bool tie, IEnumerable<byte> votes)
    {
        var counts = votes.Where(vote => vote < 252 || vote == 253).GroupBy(vote => vote)
            .ToDictionary(group => group.Key, group => group.Count());
        if (!tie || counts.Count == 0) return false;
        return !counts.TryGetValue(253, out int skipped) || counts.Any(pair => pair.Key != 253 && pair.Value >= skipped);
    }

    internal static void Prepare(NetworkedPlayerInfo exiled, bool tie, int playerCount, IEnumerable<byte> votes)
    {
        Clear();
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        bool alive = ExPlayerControl.ExPlayerControls.Any(player => player.Role == RoleId.Bakery && player.IsAlive() && !player.Data.Disconnected);
        if (!alive) return;
        breadMessage = ModTranslation.GetString(new Random().Next(1, 11) == 1 ? "BakeryExileText2" : "BakeryExileText");
        BakeryAbility.RpcSetSHRBreadMessage(breadMessage);
        var host = PlayerControl.LocalPlayer;
        byte? actual = tie ? null : exiled?.PlayerId;
        byte? selected = SelectCarrier(alive, playerCount, actual, host.PlayerId);
        if (!selected.HasValue) return;
        carrier = ExPlayerControl.ById(selected.Value)?.Player;
        if (carrier?.Data == null || carrier.Data.Disconnected) { carrier = null; return; }
        originalName = SHRCamouflagerController.OriginalName(carrier);
        fakeExile = !actual.HasValue;
        string exileLine = fakeExile
            ? ModTranslation.GetString(IsTieSkip(tie, votes) ? "SHR.Bakery.SkipTie" : "SHR.Bakery.SkipVote")
            : string.Format(FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.ExileTextNonConfirm), originalName);
        float baseFontSize = ShipStatus.Instance?.ExileCutscenePrefab?.Text?.fontSize ?? 3f;
        if (baseFontSize <= 0f || !float.IsFinite(baseFontSize)) baseFontSize = 3f;
        displayName = FormatName(exileLine, breadMessage, fakeExile, ModTranslation.GetString("SHR.Bakery.DisplayOnlyNotice"), baseFontSize);
        Logger.Debug($"Exile message baseSize={baseFontSize} fake={fakeExile}", "SHR.Bakery");
    }

    internal static bool SendVotingComplete(MeetingHud meeting, PlayerControl viewer, Il2CppStructArray<MeetingHud.VoterState> states,
        bool wasOverruled, ushort overruleNonce)
    {
        if (!SHRPlayerOptions.IsVanillaGuest(viewer)) return false;
        if (carrier?.Data == null || carrier.Data.Disconnected || displayName == null)
        {
            if (breadMessage != null) SHRMeetingChat.Reply(viewer, breadMessage, ModTranslation.GetString("Bakery"));
            return false;
        }
        var client = AmongUsClient.Instance;
        int id = client.GetClientIdFromCharacter(viewer);
        if (id < 0) return false;
        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            writer.StartMessage(Tags.GameDataTo);
            writer.Write(client.GameId);
            writer.WritePacked(id);
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(carrier.NetId);
            writer.Write((byte)RpcCalls.SetName);
            new RpcSetNameMessage(carrier.NetId, carrier.Data.NetId, displayName).SerializeRpcValues(writer);
            writer.EndMessage();
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(meeting.NetId);
            writer.Write((byte)RpcCalls.VotingComplete);
            new RpcVotingCompleteMessage(meeting.NetId, states, carrier.PlayerId, false, wasOverruled, overruleNonce).SerializeRpcValues(writer);
            writer.EndMessage();
            writer.EndMessage();
            client.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }
        return true;
    }

    internal static void Restore()
    {
        var subject = carrier;
        string name = originalName;
        bool repair = fakeExile;
        Clear();
        if (subject?.Data == null || AmongUsClient.Instance?.AmHost != true) return;
        SHRDatahackerInformation.Clear();
        SHRMadKillerSupport.Restore();
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            subject.RpcSetNamePrivate(subject == viewer ? SHRRoleNameDisplay.GetSelfDisplayName(subject) : name, viewer);
        }
        SHRRoleNameDisplay.Refresh();
        if (!repair) return;
        int current = generation;
        int game = AmongUsClient.Instance.GameId;
        foreach (float delay in new[] { 2f, 5f, 8f })
            new LateTask(() =>
            {
                var client = AmongUsClient.Instance;
                if (current != generation || client?.AmHost != true || client.GameId != game
                    || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver
                    || !ModeManager.IsMode(ModeId.SuperHostRoles) || MeetingHud.Instance != null
                    || subject?.Data?.Role == null || subject.Data.Disconnected) return;
                foreach (var viewer in PlayerControl.AllPlayerControls)
                {
                    if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
                    SHRTestRoleAbility.SendRoleView(subject, viewer, SHRMeetingChat.GetCurrentViewRole(viewer, subject));
                    SHRMeetingChat.SendState(viewer, subject, hide: false);
                }
            }, delay, "SHR restore bakery display carrier");
    }

    internal static void Clear()
    {
        generation++;
        carrier = null;
        originalName = displayName = breadMessage = null;
        fakeExile = false;
        BakeryAbility.ClearSHRBreadMessage();
    }
}
