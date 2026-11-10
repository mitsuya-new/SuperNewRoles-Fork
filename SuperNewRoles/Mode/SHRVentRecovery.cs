using System;
using System.Collections.Generic;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;

namespace SuperNewRoles.Mode;

public static class SHRVentRecovery
{
    private sealed record Recovery(PlayerControl Player, PlayerPhysics Physics, int GameId, int VentId);
    private static readonly Dictionary<byte, Recovery> Pending = new();

    internal static bool UsesOwnerReplay(RoleId role) => role is RoleId.ToiletFan or RoleId.Technician
        || SHRCrewBoostAbility.UsesEngineer(role) || SHRCrewActionAbility.UsesEngineer(role);

    internal static void WriteOwnerReplay(Action<RpcCalls> write)
    {
        write(RpcCalls.BootFromVent);
        write(RpcCalls.EnterVent);
        write(RpcCalls.ProtectPlayer);
    }

    internal static void Begin(ExPlayerControl player, int ventId)
    {
        if (player?.Data == null || player.MyPhysics == null || !SHRPlayerOptions.IsVanillaGuest(player.Player)) return;
        if (Pending.TryGetValue(player.PlayerId, out var existing) && existing.Player == player.Player && CanFinish(existing)) return;
        var recovery = new Recovery(player.Player, player.MyPhysics, AmongUsClient.Instance.GameId, ventId);
        Pending[player.PlayerId] = recovery;
        SHRCrewRoleAbility.HandleVentAbility(player);
        SHRCrewRoleAbility.RefreshVentCooldown(player);
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (viewer == player.Player || viewer.AmOwner || viewer.Data == null || viewer.Data.Disconnected) continue;
            Send(recovery, viewer, false);
        }
        Send(recovery, player.Player, true);
        RestoreHostState(recovery);
        new LateTask(() =>
        {
            if (!CanFinish(recovery)) { Remove(recovery); return; }
            Send(recovery, recovery.Player, false);
            RestoreHostState(recovery);
            SHRCrewRoleAbility.RefreshVentCooldown(recovery.Player);
        }, 0.8f, "SHR vent recovery exit");
        new LateTask(() =>
        {
            if (CanFinish(recovery)) RestoreHostState(recovery);
            Remove(recovery);
        }, 1.5f, "SHR vent recovery movement");
    }

    private static bool IsCurrent(Recovery recovery)
        => Pending.TryGetValue(recovery.Player.PlayerId, out var current) && ReferenceEquals(current, recovery)
            && AmongUsClient.Instance?.GameId == recovery.GameId;

    private static bool CanFinish(Recovery recovery)
        => IsCurrent(recovery) && AmongUsClient.Instance.AmHost
            && ModeManager.IsMode(ModeId.SuperHostRoles) && !AmongUsClient.Instance.IsGameOver
            && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started
            && recovery.Player?.Data != null && !recovery.Player.Data.Disconnected && !recovery.Player.Data.IsDead
            && MeetingHud.Instance == null && ExileController.Instance == null;

    private static void Remove(Recovery recovery)
    {
        if (IsCurrent(recovery)) Pending.Remove(recovery.Player.PlayerId);
    }

    internal static void Cancel(ExPlayerControl player)
    {
        if (player != null) Pending.Remove(player.PlayerId);
    }

    internal static void Clear() => Pending.Clear();

    private static void RestoreHostState(Recovery recovery)
    {
        recovery.Player.inVent = false;
        recovery.Player.walkingToVent = false;
        recovery.Player.moveable = true;
        recovery.Player.Visible = true;
        if (ShipStatus.Instance != null && ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out var system)
            && system.TryCast<VentilationSystem>() is { } ventilation)
        {
            ventilation.PlayersInsideVents.Remove(recovery.Player.PlayerId);
            ventilation.IsDirty = true;
        }
    }

    private static void Send(Recovery recovery, PlayerControl viewer, bool replay)
    {
        var client = AmongUsClient.Instance;
        int id = client.GetClientIdFromCharacter(viewer);
        if (id < 0) return;
        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            writer.StartMessage(Tags.GameDataTo);
            writer.Write(client.GameId);
            writer.WritePacked(id);
            if (replay) WriteOwnerReplay(Write);
            else Write(RpcCalls.BootFromVent);
            writer.EndMessage();
            client.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }

        void Write(RpcCalls call)
        {
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(call == RpcCalls.ProtectPlayer ? recovery.Player.NetId : recovery.Physics.NetId);
            writer.Write((byte)call);
            switch (call)
            {
                case RpcCalls.BootFromVent:
                    new RpcBootFromVentMessage(recovery.Physics.NetId, recovery.VentId).SerializeRpcValues(writer);
                    break;
                case RpcCalls.EnterVent:
                    new RpcEnterVentMessage(recovery.Physics.NetId, recovery.VentId).SerializeRpcValues(writer);
                    break;
                case RpcCalls.ProtectPlayer:
                    writer.WriteNetObject(recovery.Player);
                    writer.Write(0);
                    break;
            }
            writer.EndMessage();
        }
    }
}
