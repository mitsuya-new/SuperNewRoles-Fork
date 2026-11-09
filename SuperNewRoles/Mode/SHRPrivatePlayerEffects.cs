using System.Collections.Generic;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRPrivatePlayerEffects
{
    private sealed record HiddenViewer(PlayerControl Player, int VentId);
    private sealed record HiddenVent(ExPlayerControl Target, HiddenViewer[] Viewers);
    private static readonly Dictionary<byte, HiddenVent> Hidden = new();

    internal static bool IsHidden(ExPlayerControl target) => target != null && Hidden.ContainsKey(target.PlayerId);

    internal static bool IsHiddenFrom(ExPlayerControl target, PlayerControl viewer)
        => target != null && Hidden.TryGetValue(target.PlayerId, out var state)
            && state.Viewers.Any(v => v.Player == viewer);

    internal static bool ShouldHideFrom(bool isSelf, bool vanillaGuest, bool impostor)
        => !isSelf && vanillaGuest && !impostor;

    internal static void HideInVent(ExPlayerControl target, bool hidden)
    {
        if (AmongUsClient.Instance?.AmHost != true || target?.Player == null || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        if (!hidden)
        {
            if (!Hidden.Remove(target.PlayerId, out var state)) return;
            foreach (var viewer in state.Viewers)
                if (viewer.Player?.Data != null && !viewer.Player.Data.Disconnected && !target.Player.inVent)
                    SendVent(target, viewer.Player, viewer.VentId, false);
            if (target.IsAlive() && !target.Data.Disconnected)
                new LateTask(() =>
                {
                    if (target?.Player == null || target.Data.Disconnected || !target.IsAlive() || Hidden.ContainsKey(target.PlayerId)
                        || AmongUsClient.Instance?.GameState != InnerNet.InnerNetClient.GameStates.Started) return;
                    foreach (var viewer in state.Viewers)
                        if (viewer.Player?.Data != null && !viewer.Player.Data.Disconnected && !target.Player.inVent)
                            SnapFor(target, viewer.Player, target.transform.position, restoring: true);
                }, 0.65f, "SHR restore hidden player position");
            return;
        }
        if (Hidden.ContainsKey(target.PlayerId) || ShipStatus.Instance?.AllVents == null
            || target.MyPhysics == null || target.NetTransform == null) return;
        var vents = ShipStatus.Instance.AllVents.Where(v => v != null).ToArray();
        if (vents.Length == 0) return;
        var viewers = PlayerControl.AllPlayerControls.ToArray().Where(p => p?.Data != null && !p.Data.Disconnected
            && ShouldHideFrom(p == target.Player, SHRPlayerOptions.IsVanillaGuest(p), ((ExPlayerControl)p).IsImpostor()))
            .Select(p => new HiddenViewer(p, vents.OrderByDescending(v => Vector2.Distance(v.transform.position, p.GetTruePosition())).First().Id)).ToArray();
        Hidden[target.PlayerId] = new HiddenVent(target, viewers);
        foreach (var viewer in viewers) SendVent(target, viewer.Player, viewer.VentId, true);
        Logger.Debug($"Hidden target={target.PlayerId} viewers={string.Join(",", viewers.Select(v => $"{v.Player.PlayerId}:vent{v.VentId}"))}", "SHR.Jammer");
    }

    private static void SendVent(ExPlayerControl target, PlayerControl viewer, int ventId, bool enter)
    {
        int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
        if (id < 0 || target.MyPhysics == null || target.NetTransform == null) return;
        var vent = ShipStatus.Instance?.AllVents?.FirstOrDefault(v => v != null && v.Id == ventId);
        if (enter && vent == null) return;
        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            writer.StartMessage(Tags.GameDataTo);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(id);
            if (enter) WriteSnap(vent.transform.position, false);
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(target.MyPhysics.NetId);
            writer.Write((byte)(enter ? RpcCalls.EnterVent : RpcCalls.BootFromVent));
            writer.WritePacked(ventId);
            writer.EndMessage();
            if (!enter) WriteSnap(target.transform.position, true);
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }

        void WriteSnap(Vector2 position, bool restoring)
        {
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(target.NetTransform.NetId);
            writer.Write((byte)RpcCalls.SnapTo);
            WriteSnapValues(writer, target, position, restoring);
            writer.EndMessage();
        }
    }

    internal static void SnapFor(ExPlayerControl target, PlayerControl viewer, Vector2 position, bool restoring = false)
    {
        if (target?.NetTransform == null || viewer?.Data == null || viewer.Data.Disconnected) return;
        int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
        if (id < 0) return;
        var writer = AmongUsClient.Instance.StartRpcImmediately(target.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable, id);
        WriteSnapValues(writer, target, position, restoring);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private static void WriteSnapValues(MessageWriter writer, ExPlayerControl target, Vector2 position, bool restoring)
    {
        NetHelpers.WriteVector2(position, writer);
        writer.Write(unchecked((ushort)(target.NetTransform.lastSequenceId + (restoring ? 35 : 34))));
    }

    internal static void Restore()
    {
        foreach (var state in Hidden.Values.ToArray()) HideInVent(state.Target, false);
        Hidden.Clear();
    }
}
