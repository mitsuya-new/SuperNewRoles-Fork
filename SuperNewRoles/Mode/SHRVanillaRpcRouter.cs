using System.Collections.Generic;
using System.Reflection;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRVanillaRpcRouter
{
    internal enum Operation { None, Kill, Suicide, Exile, SetTasks, Report, SnapTo, Shapeshift, MurderResult }

    internal static Operation Classify(MethodBase method, object[] args)
    {
        if (method.DeclaringType == typeof(CustomDeathExtensions) && method.Name == nameof(CustomDeathExtensions.RpcCustomDeath)
            && args.Length >= 2 && args[^1] is CustomDeathType death)
            return death switch
            {
                CustomDeathType.Kill => Operation.Kill,
                CustomDeathType.Suicide => Operation.Suicide,
                CustomDeathType.Exile => Operation.Exile,
                _ => Operation.None,
            };
        if (method.DeclaringType == typeof(CustomTaskAbility) && method.Name == nameof(CustomTaskAbility.RpcUncheckedSetTasks))
            return Operation.SetTasks;
        if (method.DeclaringType != typeof(PlayerControlRpcExtensions)) return Operation.None;
        return method.Name switch
        {
            nameof(PlayerControlRpcExtensions.RpcExiledCustom) => Operation.Exile,
            nameof(PlayerControlRpcExtensions.RpcCustomReportDeadBody) => Operation.Report,
            nameof(PlayerControlRpcExtensions.RpcCustomSnapTo) => Operation.SnapTo,
            nameof(PlayerControlRpcExtensions.RpcShapeshiftModded) => Operation.Shapeshift,
            nameof(PlayerControlRpcExtensions.RpcCustomMurderPlayer) => Operation.MurderResult,
            _ => Operation.None,
        };
    }

    internal static bool CanAssignTasks(ExPlayerControl player)
        => player?.Player != null && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
            && ModeManager.IsMode(ModeId.SuperHostRoles) && !player.AmOwner
            && (!Patches.SyncVersion.VersionData.VersionMap.TryGetValue(player.PlayerId, out string version)
                || string.IsNullOrEmpty(version));

    internal static bool TryRoute(MethodBase method, object[] args, bool received)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !ModeManager.IsMode(ModeId.SuperHostRoles)) return false;
        var operation = Classify(method, args);
        if (operation == Operation.None) return false;
        if (!client.AmHost)
        {
            if (!received && client.HostId >= 0)
                CustomRPCManager.SendRpcTo(method, null, args, client.HostId);
            return true;
        }

        switch (operation)
        {
            case Operation.Kill:
                SHRMurderSync.Murder((ExPlayerControl)args[0], (ExPlayerControl)args[args.Length == 3 ? 1 : 0]);
                break;
            case Operation.Suicide:
                SHRMurderSync.Murder((ExPlayerControl)args[args.Length == 3 ? 1 : 0],
                    (ExPlayerControl)args[args.Length == 3 ? 1 : 0], finalStatus: FinalStatus.Suicide);
                break;
            case Operation.Exile:
                Exile((ExPlayerControl)args[args.Length == 3 ? 1 : 0]);
                break;
            case Operation.SetTasks:
                var player = (PlayerControl)args[0];
                if (player?.Data != null)
                    player.Data.RpcSetTasks(new Il2CppStructArray<byte>(((List<byte>)args[1]).ToArray()));
                break;
            case Operation.Report:
                ((ExPlayerControl)args[0]).Player.ReportDeadBody((NetworkedPlayerInfo)args[1]);
                break;
            case Operation.SnapTo:
                ((ExPlayerControl)args[0]).NetTransform.RpcSnapTo((Vector2)args[1]);
                break;
            case Operation.Shapeshift:
                ((PlayerControl)args[0]).RpcShapeshift((PlayerControl)args[1], (bool)args[2]);
                break;
            case Operation.MurderResult:
                if ((bool)args[2]) SHRMurderSync.Murder((PlayerControl)args[0], (PlayerControl)args[1]);
                else ((PlayerControl)args[0]).RpcMurderPlayer((PlayerControl)args[1], false);
                break;
        }
        Logger.Debug($"Native operation={operation} method={method.Name}", "SHR.RpcRoute");
        return true;
    }

    private static void Exile(ExPlayerControl player)
    {
        if (player?.Data == null || player.Data.Disconnected || player.Data.IsDead) return;
        if (ExileEvent.Invoke(player).RefCanceled) return;
        var client = AmongUsClient.Instance;
        var writer = client.StartRpcImmediately(player.Player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, -1);
        client.FinishRpcImmediately(writer);
        player.Player.Exiled();
        FinalStatusManager.SetFinalStatus(player, FinalStatus.Exiled);
    }
}
