using System;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRNunPlatform
{
    private static MovingPlatformBehaviour platform;
    private static PlayerControl passenger;
    private static bool active;
    private static int gameId;
    private static bool IsHostSHR => AmongUsClient.Instance?.AmHost == true && ModeManager.IsMode(ModeId.SuperHostRoles);
    internal static bool IsPassenger(ExPlayerControl player) => active && passenger != null && player?.Player == passenger;
    internal static byte PassengerId(MovingPlatformBehaviour current) => active && platform == current
        ? passenger?.PlayerId ?? byte.MaxValue : current.Target?.PlayerId ?? byte.MaxValue;
    internal static bool IsOnPlatform(float distance) => distance <= 1.25f;
    internal static byte NextSequence(byte current) => unchecked((byte)(current + 1));
    internal static byte[] EncodeState(byte sequence, bool left) => new byte[] { sequence, 255, 255, 255, 255, left ? (byte)1 : (byte)0 };

    internal static void Begin(MovingPlatformBehaviour current, PlayerControl target, PlayerControl interrupted)
    {
        platform = current;
        passenger = target;
        active = true;
        gameId = AmongUsClient.Instance.GameId;
        if (!IsHostSHR || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver) return;
        current.IsDirty = false;
        SendState(current);
        if (target != null) SHRPlayerOptions.Send(target);
        else if (interrupted != null) RestoreRejectedUse(interrupted, interrupted.transform.position);
    }

    internal static void Finish(MovingPlatformBehaviour current)
    {
        if (!active || platform != current) return;
        var released = passenger;
        active = false;
        passenger = null;
        platform = null;
        if (!IsHostSHR || AmongUsClient.Instance.GameId != gameId
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver) return;
        current.IsDirty = false;
        SendState(current);
        if (released?.Data != null && !released.Data.Disconnected)
        {
            if (!released.Data.IsDead) SHRVanillaRpcRouter.Teleport(released, released.transform.position, force: true);
            SHRPlayerOptions.Send(released);
        }
    }

    internal static void Cancel()
    {
        if (!active) return;
        var current = platform;
        if (current != null) current.StopAllCoroutines();
        if (passenger != null) NunAbility.RestoreInterruptedTarget(passenger, true);
        if (current != null)
        {
            current.Target = null;
            Finish(current);
        }
        else { active = false; passenger = null; platform = null; }
    }

    private static void SendState(MovingPlatformBehaviour current)
    {
        if (!IsHostSHR || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started
            || AmongUsClient.Instance.IsGameOver || ShipStatus.Instance?.Systems == null) return;
        byte? systemId = null;
        foreach (var pair in ShipStatus.Instance.Systems)
            if (pair.Value?.Pointer == current.Pointer) { systemId = (byte)pair.Key; break; }
        if (!systemId.HasValue) { Logger.Error("Moving platform system was not found", "SHR.Nun"); return; }
        current.useId = NextSequence(current.useId);
        byte[] state = EncodeState(current.useId, current.IsLeft);
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            int id = AmongUsClient.Instance.GetClientIdFromCharacter(viewer);
            if (id < 0) continue;
            var writer = MessageWriter.Get(SendOption.Reliable);
            try
            {
                writer.StartMessage(Tags.GameDataTo);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(id);
                writer.StartMessage((byte)GameDataTypes.DataFlag);
                writer.WritePacked(ShipStatus.Instance.NetId);
                writer.StartMessage(systemId.Value);
                foreach (byte value in state) writer.Write(value);
                writer.EndMessage();
                writer.EndMessage();
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
            }
            finally { writer.Recycle(); }
        }
    }

    [HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.Use), typeof(PlayerControl))]
    public static class UsePatch
    {
        public static bool Prefix(MovingPlatformBehaviour __instance, PlayerControl player)
        {
            if (!IsHostSHR || !active || platform != __instance) return true;
            SendState(__instance);
            if (SHRPlayerOptions.IsVanillaGuest(player))
                RestoreRejectedUse(player, player.transform.position);
            return false;
        }
    }

    private static void RestoreRejectedUse(PlayerControl player, Vector2 original)
    {
        if (!SHRPlayerOptions.IsVanillaGuest(player)) return;
        int currentGame = gameId;
        new LateTask(() =>
        {
            if (!IsHostSHR || AmongUsClient.Instance.GameId != currentGame || player?.Data == null
                || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
                || player.Data.Disconnected || player.Data.IsDead || MeetingHud.Instance != null || IsPassenger(player)
                || ShipStatus.Instance?.TryCast<AirshipStatus>()?.GapPlatform?.Target == player) return;
            SHRVanillaRpcRouter.Teleport(player, original, force: true);
        }, 0.1f, "SHR restore rejected platform use");
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class UpdatePatch
    {
        public static void Postfix()
        {
            if (!active) return;
            if (AmongUsClient.Instance == null || !ModeManager.IsMode(ModeId.SuperHostRoles) || AmongUsClient.Instance.GameId != gameId
                || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
                || MeetingHud.Instance != null || ExileController.Instance != null || platform == null
                || passenger?.Data?.IsDead == true || passenger?.Data?.Disconnected == true) Cancel();
        }
    }
}
