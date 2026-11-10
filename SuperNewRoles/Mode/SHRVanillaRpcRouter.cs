using System.Collections.Generic;
using System.Reflection;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;
using InnerNet;
using AmongUs.InnerNet.GameDataMessages;

namespace SuperNewRoles.Mode;

public static class SHRVanillaRpcRouter
{
    internal enum Operation { None, Kill, Suicide, Exile, SetTasks, Report, SnapTo, Shapeshift, MurderResult, Revenge, VampireLinkedDeath, VampireLinkedExile, Camouflage, FalseCharge }

    internal static Operation Classify(MethodBase method, object[] args)
    {
        if (method.DeclaringType == typeof(CamouflagerAbility) && method.Name == nameof(CamouflagerAbility.RpcRequestSHRStart))
            return Operation.Camouflage;
        if (method.DeclaringType == typeof(CustomDeathExtensions) && method.Name == nameof(CustomDeathExtensions.RpcCustomDeath)
            && args.Length >= 2 && args[^1] is CustomDeathType death)
            return death switch
            {
                CustomDeathType.Kill => Operation.Kill,
                CustomDeathType.Suicide => Operation.Suicide,
                CustomDeathType.Exile => Operation.Exile,
                CustomDeathType.FalseCharge => Operation.FalseCharge,
                CustomDeathType.Revange => Operation.Revenge,
                CustomDeathType.VampireWithDead => Operation.VampireLinkedDeath,
                CustomDeathType.VampireWithDeadNonDeadbody => Operation.VampireLinkedExile,
                _ => Operation.None,
            };
        if (method.DeclaringType == typeof(CustomTaskAbility) && method.Name == nameof(CustomTaskAbility.RpcUncheckedSetTasks))
            return Operation.SetTasks;
        if (method.DeclaringType == typeof(TryShapeshiftPatch) && method.Name == nameof(TryShapeshiftPatch.RpcCustomShapeshift))
            return Operation.Shapeshift;
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

    internal static bool Teleport(ExPlayerControl player, Vector2 position, bool force = false)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player?.Data == null || player.Data.Disconnected || !player.IsAlive() || player.NetTransform == null) return false;
        var net = player.NetTransform;
        if (!force && ((Vector2)player.transform.position - position).sqrMagnitude < 0.0001f) return true;
        net.SnapTo(position, unchecked((ushort)(net.lastSequenceId + 128)));
        var writer = client.StartRpcImmediately(net.NetId, (byte)RpcCalls.SnapTo, force ? SendOption.Reliable : SendOption.None);
        NetHelpers.WriteVector2(position, writer);
        writer.Write(unchecked((ushort)(net.lastSequenceId + 2)));
        client.FinishRpcImmediately(writer);
        return true;
    }

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
            case Operation.Camouflage:
                ((ExPlayerControl)args[0])?.GetAbility<CamouflagerAbility>()?.SHRController?.Start();
                break;
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
            case Operation.FalseCharge:
                var falselyCharged = (ExPlayerControl)args[args.Length == 3 ? 1 : 0];
                if (Exile(falselyCharged)) FinalStatusManager.SetFinalStatus(falselyCharged, FinalStatus.FalseCharge);
                break;
            case Operation.Revenge:
                var revengeTarget = (ExPlayerControl)args[args.Length == 3 ? 1 : 0];
                if (Exile(revengeTarget)) FinalStatusManager.SetFinalStatus(revengeTarget, FinalStatus.Revange);
                break;
            case Operation.VampireLinkedDeath:
                var dependent = (ExPlayerControl)args[args.Length == 3 ? 1 : 0];
                SHRMurderSync.Murder(dependent, dependent, finalStatus: FinalStatus.VampireWithDead);
                break;
            case Operation.VampireLinkedExile:
                var exiledDependent = (ExPlayerControl)args[args.Length == 3 ? 1 : 0];
                if (Exile(exiledDependent)) FinalStatusManager.SetFinalStatus(exiledDependent, FinalStatus.VampireWithDead);
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
                if (SHRCamouflagerController.BlocksShape((PlayerControl)args[0], (PlayerControl)args[1])) break;
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

    internal static bool Exile(ExPlayerControl player)
    {
        if (player?.Data == null || player.Data.Disconnected || player.Data.IsDead) return false;
        if (ExileEvent.Invoke(player).RefCanceled) return false;
        if (player.Data.IsDead) return false;
        var client = AmongUsClient.Instance;
        if (!SHRAntiBlackout.DeferExile(player.Player))
        {
            var writer = client.StartRpcImmediately(player.Player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, -1);
            client.FinishRpcImmediately(writer);
        }
        player.Player.Exiled();
        FinalStatusManager.SetFinalStatus(player, FinalStatus.Exiled);
        return true;
    }

    internal static bool RocketDeath(ExPlayerControl source, ExPlayerControl target)
        => AbilityExile(source, target, CustomDeathType.LaunchByRocket, FinalStatus.LaunchByRocket, null);

    internal static bool PushDeath(ExPlayerControl source, ExPlayerControl target, Vector2? corpsePosition)
        => AbilityExile(source, target, CustomDeathType.Push, FinalStatus.Push, corpsePosition);

    private static bool AbilityExile(ExPlayerControl source, ExPlayerControl target, CustomDeathType death, FinalStatus status, Vector2? corpsePosition)
    {
        var requested = target;
        var client = AmongUsClient.Instance;
        if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNet.InnerNetClient.GameStates.Started || client.IsGameOver
            || source?.Data == null || source.Data.Disconnected || target?.Data == null || target.Data.IsDead || target.Data.Disconnected) return false;
        if (SHRWaveCannonController.BlocksAttack(SHRWaveCannonController.IsProtected(target), source == target, death)) return false;
        if (!TryKillEvent.Invoke(source, ref target).RefSuccess || target?.Data == null || target.Data.IsDead || target.Data.Disconnected) return false;
        if (SHRWaveCannonController.BlocksAttack(SHRWaveCannonController.IsProtected(target), source == target, death)) return false;
        if (SHRPrivatePlayerEffects.IsHidden(target)) SHRPrivatePlayerEffects.HideInVent(target, false);
        if (corpsePosition.HasValue && target == requested)
        {
            Vector2 original = target.transform.position;
            ushort sequence = unchecked((ushort)(target.NetTransform.lastSequenceId + 128));
            target.NetTransform.SnapTo(original, sequence);
            foreach (var viewer in PlayerControl.AllPlayerControls)
            {
                if (viewer?.Data == null || viewer.AmOwner || viewer.Data.Disconnected) continue;
                int viewerId = client.GetClientIdFromCharacter(viewer);
                if (viewerId < 0) continue;
                bool vanilla = SHRPlayerOptions.IsVanillaGuest(viewer);
                SendPushDeath(target, viewerId, vanilla ? corpsePosition.Value : original, sequence, vanilla);
            }
        }
        else if (!SHRAntiBlackout.DeferExile(target.Player))
        {
            var writer = client.StartRpcImmediately(target.Player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, -1);
            client.FinishRpcImmediately(writer);
        }
        target.Player.Exiled();
        FinalStatusManager.SetFinalStatus(target, status);
        MurderDataManager.AddMurderData(source, target);
        return requested.Data.IsDead;
    }

    private static void SendPushDeath(ExPlayerControl target, int viewerId, Vector2 position, ushort sequence, bool vanilla)
    {
        var client = AmongUsClient.Instance;
        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            writer.StartMessage(Tags.GameDataTo);
            writer.Write(client.GameId);
            writer.WritePacked(viewerId);
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(target.NetTransform.NetId);
            writer.Write((byte)RpcCalls.SnapTo);
            NetHelpers.WriteVector2(position, writer);
            writer.Write(sequence);
            writer.EndMessage();
            writer.StartMessage((byte)GameDataTypes.RpcFlag);
            writer.WritePacked(target.Player.NetId);
            writer.Write((byte)(vanilla ? RpcCalls.MurderPlayer : RpcCalls.Exiled));
            if (vanilla)
            {
                writer.WriteNetObject(target.Player);
                writer.Write((int)(MurderResultFlags.DecisionByHost | MurderResultFlags.Succeeded));
            }
            writer.EndMessage();
            writer.EndMessage();
            client.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }
    }
}
