using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Patches;
using SuperNewRoles.Roles;

namespace SuperNewRoles.Mode;

public static class SHRRoleAssignment
{
    private static readonly Dictionary<byte, RoleTypes> VanillaRoles = new();
    private static int assignmentGeneration;
    public static bool Capturing { get; private set; }

    public static void Begin()
    {
        assignmentGeneration++;
        VanillaRoles.Clear();
        Capturing = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    }

    public static void Abort()
    {
        assignmentGeneration++;
        Capturing = false;
        VanillaRoles.Clear();
    }

    private static bool IsUnmodded(PlayerControl player)
        => !SyncVersion.VersionData.VersionMap.TryGetValue(player.PlayerId, out string version)
            || string.IsNullOrEmpty(version);

    internal static RoleTypes GetViewRole(RoleId customRole, RoleTypes vanillaRole, bool isSelf,
        bool unmoddedSubject, RoleId viewerRole, bool unmoddedViewer, bool subjectIsImpostor)
    {
        if (!unmoddedViewer) return vanillaRole;
        if (isSelf && unmoddedSubject)
            return SHRRoleTypes.GetInitialRole(customRole) ?? vanillaRole;
        if (viewerRole is RoleId.Jackal or RoleId.Sheriff && subjectIsImpostor)
            return RoleTypes.Scientist;
        if (unmoddedSubject && customRole is RoleId.Jackal or RoleId.Sheriff)
            return RoleTypes.Scientist;
        return vanillaRole;
    }

    public static void PrepareModdedClients()
    {
        foreach (var player in PlayerControl.AllPlayerControls)
            if (player?.Data?.Role != null) VanillaRoles[player.PlayerId] = player.Data.Role.Role;
        SendTables(unmodded: false);
    }

    public static void Complete()
    {
        try
        {
            SendTables(unmodded: true);
            ScheduleIntroCompletion();
        }
        finally { Capturing = false; }
    }

    private static void ScheduleIntroCompletion()
    {
        var client = AmongUsClient.Instance;
        if (!Capturing || client == null || !client.AmHost) return;
        int generation = assignmentGeneration;
        int gameId = client.GameId;
        bool IsCurrent() => generation == assignmentGeneration && AmongUsClient.Instance != null
            && AmongUsClient.Instance.AmHost && AmongUsClient.Instance.GameId == gameId
            && ModeManager.IsMode(ModeId.SuperHostRoles);

        foreach (var viewer in PlayerControl.AllPlayerControls.ToArray())
        {
            if (viewer == null || viewer.AmOwner || viewer.Data == null || viewer.Data.Disconnected) continue;
            int viewerId = client.GetClientIdFromCharacter(viewer);
            if (viewerId < 0 || !VanillaRoles.TryGetValue(viewer.PlayerId, out var vanilla)) continue;
            var custom = ((ExPlayerControl)viewer)?.Role ?? RoleId.None;
            bool unmodded = IsUnmodded(viewer);
            var role = GetViewRole(custom, vanilla, true, unmodded, custom, unmodded,
                RoleManager.Instance.GetRole(vanilla)?.TeamType == RoleTeamTypes.Impostor);

            new LateTask(() =>
            {
                if (!IsCurrent() || viewer == null || viewer.Data == null || viewer.Data.Disconnected) return;
                new LateTask(() =>
                {
                    if (IsCurrent() && viewer != null) SendPlayerInformation(viewer, viewerId, temporarilyDisconnected: false);
                }, 0.5f, "SHR restore intro player information");
                SendPlayerInformation(viewer, viewerId, temporarilyDisconnected: true);
                new LateTask(() =>
                {
                    if (!IsCurrent() || viewer == null || viewer.Data == null || viewer.Data.Disconnected) return;
                    var writer = AmongUsClient.Instance.StartRpcImmediately(viewer.NetId,
                        (byte)RpcCalls.SetRole, SendOption.Reliable, viewerId);
                    new RpcSetRoleMessage(viewer.NetId, role, true).SerializeRpcValues(writer);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    Logger.Debug($"Intro completion sent viewer={viewer.PlayerId} role={role}", "SHR.Assign");
                }, 0.25f, "SHR complete native intro assignment");
            }, 0.25f, "SHR prepare native intro completion");
        }
    }

    private static void SendPlayerInformation(PlayerControl viewer, int viewerId, bool temporarilyDisconnected)
    {
        foreach (var player in GameData.Instance.AllPlayers)
        {
            if (player == null || player.Role == null) continue;
            var writer = MessageWriter.Get(SendOption.Reliable);
            bool disconnected = player.Disconnected;
            var originalRole = player.Role;
            try
            {
                writer.StartMessage(Tags.GameDataTo);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(viewerId);
                writer.StartMessage(1);
                writer.WritePacked(player.NetId);
                if (temporarilyDisconnected) player.Disconnected = true;
                var subject = player.Object;
                if (subject != null && VanillaRoles.TryGetValue(player.PlayerId, out var vanilla))
                {
                    var viewerRole = ((ExPlayerControl)viewer)?.Role ?? RoleId.None;
                    var role = GetViewRole(((ExPlayerControl)subject)?.Role ?? RoleId.None, vanilla,
                        subject.PlayerId == viewer.PlayerId, IsUnmodded(subject), viewerRole,
                        IsUnmodded(viewer), RoleManager.Instance.GetRole(vanilla)?.TeamType == RoleTeamTypes.Impostor);
                    player.Role = RoleManager.Instance.GetRole(role);
                }
                try { player.Serialize(writer, false); }
                finally
                {
                    player.Disconnected = disconnected;
                    player.Role = originalRole;
                }
                writer.EndMessage();
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
            }
            finally
            {
                player.Disconnected = disconnected;
                player.Role = originalRole;
                writer.Recycle();
            }
        }
        Logger.Debug($"Intro player information viewerClient={viewerId} temporary={temporarilyDisconnected}", "SHR.Assign");
    }

    private static void SendTables(bool unmodded)
    {
        var client = AmongUsClient.Instance;
        if (!Capturing || client == null || !client.AmHost) return;
        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p.Data != null && !p.Data.Disconnected).ToArray();
        foreach (var viewer in players)
        {
            if (viewer.AmOwner || IsUnmodded(viewer) != unmodded) continue;
            int viewerId = client.GetClientIdFromCharacter(viewer);
            if (viewerId < 0) continue;
            RoleId viewerRole = ((ExPlayerControl)viewer)?.Role ?? RoleId.None;
            MessageWriter writer = null;
            int count = 0;
            try
            {
                foreach (var subject in players)
                {
                    if (!VanillaRoles.TryGetValue(subject.PlayerId, out var vanilla))
                    {
                        if (subject.Data.Role == null) continue;
                        vanilla = subject.Data.Role.Role;
                    }
                    bool impostor = RoleManager.Instance.GetRole(vanilla)?.TeamType == RoleTeamTypes.Impostor;
                    var role = GetViewRole(((ExPlayerControl)subject)?.Role ?? RoleId.None, vanilla,
                        subject.PlayerId == viewer.PlayerId, IsUnmodded(subject), viewerRole, unmodded, impostor);
                    if (writer != null && writer.Length + 32 > 1200)
                        Flush();
                    if (writer == null)
                    {
                        writer = MessageWriter.Get(SendOption.Reliable);
                        writer.StartMessage(Tags.GameDataTo);
                        writer.Write(client.GameId);
                        writer.WritePacked(viewerId);
                    }
                    writer.StartMessage((byte)GameDataTypes.RpcFlag);
                    writer.WritePacked(subject.NetId);
                    writer.Write((byte)RpcCalls.SetRole);
                    new RpcSetRoleMessage(subject.NetId, role, true).SerializeRpcValues(writer);
                    writer.EndMessage();
                    count++;
                    Logger.Debug($"Initial role subject={subject.PlayerId} viewer={viewer.PlayerId} role={role}", "SHR.Assign");
                }
                Flush();
            }
            finally { writer?.Recycle(); }

            void Flush()
            {
                if (writer == null) return;
                writer.EndMessage();
                if (writer.Length > 1200) throw new InvalidOperationException("SHR role packet exceeds 1200 bytes.");
                client.SendOrDisconnect(writer);
                writer.Recycle();
                writer = null;
                Logger.Debug($"Initial roles sent viewer={viewer.PlayerId} count={count}", "SHR.Assign");
                count = 0;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    public static class CaptureRolePatch
    {
        public static bool Prefix(PlayerControl __instance, RoleTypes roleType, bool canOverrideRole)
        {
            if (!Capturing || !ModeManager.IsMode(ModeId.SuperHostRoles) || !AmongUsClient.Instance.AmHost) return true;
            VanillaRoles[__instance.PlayerId] = roleType;
            __instance.StartCoroutine(__instance.CoSetRole(roleType, canOverrideRole));
            return false;
        }
    }
}
