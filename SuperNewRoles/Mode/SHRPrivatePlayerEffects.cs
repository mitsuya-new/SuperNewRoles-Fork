using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal sealed class SHRHiddenSources
{
    private readonly Dictionary<object, Func<PlayerControl, bool>> sources = new();
    internal int Count => sources.Count;
    internal void Set(object source, Func<PlayerControl, bool> predicate) => sources[source] = predicate;
    internal void Remove(object source) => sources.Remove(source);
    internal bool Matches(PlayerControl viewer) => sources.Values.Any(predicate => predicate(viewer));
}

public static class SHRPrivatePlayerEffects
{
    private sealed record HiddenViewer(PlayerControl Player, int VentId);
    private sealed record HiddenVent(ExPlayerControl Target, HiddenViewer[] Viewers);
    private static readonly Dictionary<byte, HiddenVent> Hidden = new();
    private static readonly Dictionary<byte, SHRHiddenSources> Requests = new();

    internal static bool ScientistShouldHide(bool self, bool vanilla, bool impostor, bool dead, bool lighterCanSee, bool lighterActive)
        => ShouldHideFrom(self, vanilla, impostor) && !dead && !(lighterCanSee && lighterActive);

    internal static void SetHidden(ExPlayerControl target, object source, bool hidden, Func<PlayerControl, bool> predicate)
    {
        if (AmongUsClient.Instance?.AmHost != true || target?.Data == null || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        if (hidden)
        {
            if (!Requests.TryGetValue(target.PlayerId, out var requests)) Requests[target.PlayerId] = requests = new();
            requests.Set(source, predicate);
        }
        else if (Requests.TryGetValue(target.PlayerId, out var requests))
        {
            requests.Remove(source);
            if (requests.Count == 0) Requests.Remove(target.PlayerId);
        }
        RefreshHidden(target);
    }

    internal static void RefreshHidden(ExPlayerControl target)
    {
        if (AmongUsClient.Instance?.AmHost != true || target?.Data == null || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        if (!Requests.TryGetValue(target.PlayerId, out var requests) || requests.Count == 0 || target.IsDead() || target.Data.Disconnected)
        {
            HideInVent(target, false);
            return;
        }
        if (ShipStatus.Instance?.AllVents == null || target.MyPhysics == null || target.NetTransform == null) return;
        var vents = ShipStatus.Instance.AllVents.Where(v => v != null).ToArray();
        if (vents.Length == 0) return;
        var wanted = PlayerControl.AllPlayerControls.ToArray().Where(p => p?.Data != null && !p.Data.Disconnected
            && requests.Matches(p)).ToArray();
        Hidden.TryGetValue(target.PlayerId, out var previous);
        var old = previous?.Viewers ?? Array.Empty<HiddenViewer>();
        var current = wanted.Select(p => old.FirstOrDefault(v => v.Player == p)
            ?? new HiddenViewer(p, vents.OrderByDescending(v => Vector2.Distance(v.transform.position, p.GetTruePosition())).First().Id)).ToArray();
        Hidden[target.PlayerId] = new HiddenVent(target, current);
        foreach (var viewer in old.Where(v => !wanted.Contains(v.Player)))
            if (viewer.Player?.Data != null && !viewer.Player.Data.Disconnected && !target.Player.inVent)
            {
                SendVent(target, viewer.Player, viewer.VentId, false);
                RestorePosition(target, viewer.Player);
            }
        foreach (var viewer in current.Where(v => !old.Any(o => o.Player == v.Player)))
            SendVent(target, viewer.Player, viewer.VentId, true);
    }

    internal static bool IsHidden(ExPlayerControl target) => target != null && Hidden.ContainsKey(target.PlayerId);

    internal static bool IsHiddenFrom(ExPlayerControl target, PlayerControl viewer)
        => target != null && Hidden.TryGetValue(target.PlayerId, out var state)
            && state.Viewers.Any(v => v.Player == viewer);

    internal static bool ShouldHideFrom(bool isSelf, bool vanillaGuest, bool impostor)
        => !isSelf && vanillaGuest && !impostor;

    internal static void HideInVent(ExPlayerControl target, bool hidden)
    {
        if (AmongUsClient.Instance?.AmHost != true || target?.Player == null || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        if (hidden)
        {
            SetHidden(target, target, true, viewer => ShouldHideFrom(viewer == target.Player,
                SHRPlayerOptions.IsVanillaGuest(viewer), ((ExPlayerControl)viewer).IsImpostor()));
            return;
        }
        if (!hidden)
        {
            Requests.Remove(target.PlayerId);
            if (!Hidden.Remove(target.PlayerId, out var state)) return;
            foreach (var viewer in state.Viewers)
                if (viewer.Player?.Data != null && !viewer.Player.Data.Disconnected && !target.Player.inVent)
                    SendVent(target, viewer.Player, viewer.VentId, false);
            foreach (var viewer in state.Viewers) RestorePosition(target, viewer.Player);
            return;
        }
    }

    private static void RestorePosition(ExPlayerControl target, PlayerControl viewer)
    {
        int gameId = AmongUsClient.Instance.GameId;
        new LateTask(() =>
        {
            if (target?.Player == null || target.Data.Disconnected || !target.IsAlive() || IsHiddenFrom(target, viewer)
                || AmongUsClient.Instance?.AmHost != true || AmongUsClient.Instance.GameId != gameId
                || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;
            if (viewer?.Data != null && !viewer.Data.Disconnected && !target.Player.inVent)
                SnapFor(target, viewer, target.transform.position, restoring: true);
        }, 0.65f, "SHR restore hidden player position");
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
        Requests.Clear();
    }
}
