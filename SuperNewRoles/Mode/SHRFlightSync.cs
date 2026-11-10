using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.CrewMate;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal sealed class SHRFlightFrames
{
    private int sent;
    internal const int MaximumFrames = 20;
    internal void Reset() => sent = 0;
    internal bool Advance(float elapsed)
    {
        int frame = System.Math.Min(MaximumFrames, (int)(System.Math.Max(0f, elapsed) * 10f));
        if (frame <= sent) return false;
        sent = frame;
        return true;
    }
}

internal static class SHRFlightSync
{
    internal static bool IsFlying(ExPlayerControl player)
        => player?.GetAbility<PteranodonAbility>()?.IsFlyingSHR == true;

    internal static void Send(ExPlayerControl player, Vector2 position)
    {
        var client = AmongUsClient.Instance;
        if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver
            || player?.Data == null || player.Data.Disconnected || player.Data.IsDead) return;
        var net = player.NetTransform;
        net.SnapTo(position, unchecked((ushort)(net.lastSequenceId + 128)));
        ushort sequence = unchecked((ushort)(net.lastSequenceId + 2));
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            int clientId = client.GetClientIdFromCharacter(viewer);
            if (clientId < 0) continue;
            var writer = client.StartRpcImmediately(net.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable, clientId);
            NetHelpers.WriteVector2(position, writer);
            writer.Write(sequence);
            client.FinishRpcImmediately(writer);
        }
    }
}
