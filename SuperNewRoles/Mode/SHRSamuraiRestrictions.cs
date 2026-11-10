using HarmonyLib;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using SuperNewRoles.Roles.Ability;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace SuperNewRoles.Mode;

public static class SHRSamuraiRestrictions
{
    internal static bool RejectVent(RoleId role, bool canUseVent, VentilationSystem.Operation operation)
        => (role == RoleId.ToiletFan || SHRCrewBoostAbility.UsesEngineer(role) || SHRCrewActionAbility.UsesEngineer(role) || SHRCrewRoleAbility.HasCrewImpostorControls(role)
            || role is RoleId.Samurai or RoleId.MadKiller or RoleId.DarkKiller or RoleId.DoubleKiller or RoleId.VampireDependent
                or RoleId.Technician or RoleId.SilverBullet && !canUseVent)
            && operation is VentilationSystem.Operation.Enter or VentilationSystem.Operation.Move;

    internal static bool RejectSabotage(RoleId role, bool canSabotage)
        => SHRCrewRoleAbility.HasCrewImpostorControls(role) || role is RoleId.Samurai or RoleId.DoubleKiller && !canSabotage;

    private static bool IsHostSHR => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
        && ModeManager.IsMode(ModeId.SuperHostRoles);

    private static bool CanUseVent(ExPlayerControl player)
        => player.CanUseVent();

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    public static class RecoveryStartPatch { public static void Prefix() => SHRVentRecovery.Clear(); }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class RecoveryEndPatch { public static void Prefix() => SHRVentRecovery.Clear(); }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
    public static class RecoveryDisconnectPatch { public static void Prefix() => SHRVentRecovery.Clear(); }

    internal static VentilationSystem.Operation ResolveVentOperation(VentilationSystem.Operation operation, bool rejected, bool replay = false)
        => rejected && !replay ? VentilationSystem.Operation.Exit : operation;

    private static void BootRejectedVent(ExPlayerControl player, int ventId)
    {
        if (player?.MyPhysics == null) return;
        if (SHRVentRecovery.UsesOwnerReplay(player.Role) && SHRPlayerOptions.IsVanillaGuest(player.Player))
        {
            SHRVentRecovery.Begin(player, ventId);
            return;
        }
        player.MyPhysics.RpcBootFromVent(ventId);
    }

    [HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.PerformVentOp))]
    public static class VentilationPatch
    {
        public static void Prefix(byte playerId, ref VentilationSystem.Operation op, byte ventId, out bool __state)
        {
            __state = false;
            if (!IsHostSHR) return;
            var ex = ExPlayerControl.ById(playerId);
            if (ex?.Data == null) return;
            bool waveBusy = ex.GetAbility<WaveCannonAbility>()?.SHRController?.Busy == true || PenguinAbility.IsDragged(ex) || SHRNunPlatform.IsPassenger(ex) || SHRFlightSync.IsFlying(ex)
                || ex.GetAbility<SHREvilHackerAbility>()?.FreezesMovement == true;
            __state = (waveBusy && op is VentilationSystem.Operation.Enter or VentilationSystem.Operation.Move)
                || RejectVent(ex.Role, CanUseVent(ex), op)
                || (ex.GetAbility<SHRHackerMadmateAbility>() != null && !CanUseVent(ex)
                    && op is VentilationSystem.Operation.Enter or VentilationSystem.Operation.Move);
            if (!__state && op == VentilationSystem.Operation.Enter) SHRVentRecovery.Cancel(ex);
            if (__state) Logger.Debug($"Vent rejected player={playerId} vent={ventId} operation={op}", "SHR.Samurai");
            op = ResolveVentOperation(op, __state, SHRVentRecovery.UsesOwnerReplay(ex.Role));
        }

        public static void Postfix(byte playerId, byte ventId, bool __state)
        {
            if (!__state) return;
            var player = ExPlayerControl.ById(playerId);
            BootRejectedVent(player, ventId);
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
    public static class VentRpcPatch
    {
        public static bool Prefix(PlayerPhysics __instance, int id, ref Il2CppSystem.Collections.IEnumerator __result)
        {
            if (!IsHostSHR || __instance.myPlayer?.Data == null) return true;
            ExPlayerControl player = __instance.myPlayer;
            if (player.GetAbility<WaveCannonAbility>()?.SHRController?.Busy != true && !PenguinAbility.IsDragged(player) && !SHRNunPlatform.IsPassenger(player) && !SHRFlightSync.IsFlying(player)
                && player.GetAbility<SHREvilHackerAbility>()?.FreezesMovement != true
                && (player.GetAbility<SHRHackerMadmateAbility>() == null || CanUseVent(player))
                && !RejectVent(player.Role, CanUseVent(player), VentilationSystem.Operation.Enter))
            {
                SHRVentRecovery.Cancel(player);
                return true;
            }
            BootRejectedVent(player, id);
            __result = EmptyEntry().WrapToIl2Cpp();
            return false;
        }

        private static System.Collections.IEnumerator EmptyEntry() { yield break; }
    }

    [HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
    public static class SabotagePatch
    {
        public static bool Prefix(PlayerControl player)
        {
            if (!IsHostSHR || player?.Data == null) return true;
            ExPlayerControl ex = player;
            bool reject = RejectSabotage(ex.Role, ex.CanSabotage());
            if (reject) Logger.Debug($"Sabotage rejected player={player.PlayerId}", "SHR.Samurai");
            return !reject;
        }
    }
}
