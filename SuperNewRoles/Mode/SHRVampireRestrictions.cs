using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRVampireRestrictions
{
    private static readonly Dictionary<byte, PlayerControl> FakeComms = new();
    private static readonly Dictionary<byte, (PlayerControl Player, float Light)> DependentLights = new();
    private static readonly Dictionary<(byte, byte), (ExPlayerControl Target, PlayerControl Viewer)> HiddenAdmin = new();
    private static float nextUpdate;
    private static bool IsHostSHR => AmongUsClient.Instance?.AmHost == true && ModeManager.IsMode(ModeId.SuperHostRoles);

    internal static bool IsVampire(RoleId role) => role is RoleId.Vampire or RoleId.VampireDependent;

    internal static float DependentVisionRadius(float light, float minimum, float maximum, float crewVision, float impostorVision, bool impostorInDark)
        => light >= 1f ? minimum : impostorInDark ? maximum * impostorVision
            : (minimum + (maximum - minimum) * light) * crewVision;

    internal static Vector2[] AdminPositions(int map) => map switch
    {
        0 => new[] { new Vector2(3.48f, -8.62f) },
        1 => new[] { new Vector2(21.02f, 19.09f) },
        2 => new[] { new Vector2(22.80f, -21.52f), new Vector2(24.66f, -21.52f) },
        3 => new[] { new Vector2(-3.48f, -8.62f) },
        4 => new[] { new Vector2(-22.32f, 0.91f), new Vector2(19.89f, 12.60f) },
        _ => System.Array.Empty<Vector2>(),
    };

    internal static Vector2[] OtherDevicePositions(int map) => map switch
    {
        0 => new[] { new Vector2(-13.06f, -2.45f) },
        1 => new[] { new Vector2(16.22f, 5.82f) },
        2 => new[] { new Vector2(2.96f, -12.74f), new Vector2(26.70f, -15.94f) },
        3 => new[] { new Vector2(13.06f, -2.45f) },
        4 => new[] { new Vector2(8.10f, -9.63f), new Vector2(25.24f, -7.94f) },
        5 => new[] { new Vector2(-2.765f, -9.819f) },
        _ => System.Array.Empty<Vector2>(),
    };

    private static bool CommsActive => ShipStatus.Instance != null
        && ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Comms, out var system)
        && system.TryCast<IActivatable>()?.IsActive == true;

    internal static void Update()
    {
        if (!IsHostSHR || ShipStatus.Instance == null || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 0.15f;
        int map = GameOptionsManager.Instance.CurrentGameOptions.MapId;
        var admins = AdminPositions(map);
        var devices = admins.Concat(OtherDevicePositions(map)).ToArray();
        float distance = map == 1 ? 2.4f : 1.8f;
        bool inGame = AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started
            && !AmongUsClient.Instance.IsGameOver && MeetingHud.Instance == null && ExileController.Instance == null
            && HudManager.Instance != null && !HudManager.Instance.IsIntroDisplayed;
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            ExPlayerControl player = viewer;
            if (player == null) continue;
            bool available = inGame && player.IsAlive() && !viewer.inVent;
            if (available && player.Role == RoleId.VampireDependent && Vampire.VampireDependentHasReverseVision)
            {
                var light = (viewer, SHRMadKillerSupport.LightFraction());
                if (!DependentLights.TryGetValue(viewer.PlayerId, out var previous) || previous != light)
                {
                    DependentLights[viewer.PlayerId] = light;
                    SHRPlayerOptions.Send(viewer);
                }
            }
            bool block = available && IsVampire(player.Role) && Vampire.VampireCannotUseDevice
                && devices.Any(pos => Vector2.Distance(pos, viewer.GetTruePosition()) <= distance);
            if (CommsActive) FakeComms.Remove(viewer.PlayerId);
            else if (block && !FakeComms.ContainsKey(viewer.PlayerId))
            {
                SendComms(viewer, true);
                FakeComms[viewer.PlayerId] = viewer;
            }
            else if (!block && FakeComms.Remove(viewer.PlayerId)) SendComms(viewer, false);

            bool nearAdmin = available && admins.Any(pos => Vector2.Distance(pos, viewer.GetTruePosition()) <= distance);
            foreach (var target in ExPlayerControl.ExPlayerControls)
            {
                if (target.Player == viewer || target.Data == null || target.Data.Disconnected) continue;
                var key = (target.PlayerId, viewer.PlayerId);
                bool hide = nearAdmin && target.IsAlive() && IsVampire(target.Role) && Vampire.VampireInvisibleOnAdmin;
                if (hide)
                {
                    HiddenAdmin[key] = (target, viewer);
                    SHRPrivatePlayerEffects.SnapFor(target, viewer, new Vector2(50f, 50f));
                }
                else if (HiddenAdmin.Remove(key)) SHRPrivatePlayerEffects.SnapFor(target, viewer, target.transform.position, restoring: true);
            }
        }
    }

    private static void SendComms(PlayerControl player, bool broken)
    {
        int id = AmongUsClient.Instance.GetClientIdFromCharacter(player);
        if (id < 0 || ShipStatus.Instance == null) return;
        Send(broken ? (byte)128 : (byte)16, player);
        int map = GameOptionsManager.Instance.CurrentGameOptions.MapId;
        if (!broken && map is 1 or 5) Send(17, PlayerControl.LocalPlayer);
        void Send(byte amount, PlayerControl actor)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId,
                (byte)RpcCalls.UpdateSystem, SendOption.Reliable, id);
            writer.Write((byte)SystemTypes.Comms);
            writer.WriteNetObject(actor);
            writer.Write(amount);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    internal static void Restore()
    {
        if (IsHostSHR)
        {
            if (!CommsActive)
                foreach (var player in FakeComms.Values)
                    if (SHRPlayerOptions.IsVanillaGuest(player)) SendComms(player, false);
            foreach (var pair in HiddenAdmin.Values)
                if (pair.Target?.Player != null && pair.Target.Data != null && !pair.Target.Data.Disconnected)
                    SHRPrivatePlayerEffects.SnapFor(pair.Target, pair.Viewer, pair.Target.transform.position, restoring: true);
        }
        FakeComms.Clear();
        DependentLights.Clear();
        HiddenAdmin.Clear();
    }

    [HarmonyPatch(typeof(SwitchSystem), nameof(SwitchSystem.UpdateSystem))]
    public static class RepairPatch
    {
        public static bool Prefix(SwitchSystem __instance, PlayerControl player, MessageReader msgReader)
        {
            if (!IsHostSHR || player?.Data == null || !IsVampire(((ExPlayerControl)player).Role) || !Vampire.VampireCannotFixSabotage) return true;
            var copy = MessageReader.Get(msgReader);
            try
            {
                if (copy.ReadByte() >= 5) return true;
                __instance.IsDirty = true;
                return false;
            }
            finally { copy.Recycle(); }
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class UpdatePatch { public static void Postfix() => Update(); }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    public static class MeetingPatch { public static void Prefix() { Restore(); SHRPrivatePlayerEffects.Restore(); } }
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class EndPatch { public static void Prefix() { Restore(); SHRPrivatePlayerEffects.Restore(); } }
}
