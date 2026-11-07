using HarmonyLib;
using Hazel;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using SuperNewRoles.Roles.Ability;

namespace SuperNewRoles.Mode;

public static class SHRSamuraiRestrictions
{
    internal static bool RejectVent(RoleId role, bool canUseVent, VentilationSystem.Operation operation)
        => role == RoleId.Samurai && !canUseVent
            && operation is VentilationSystem.Operation.Enter or VentilationSystem.Operation.Move;

    internal static bool RejectSabotage(RoleId role, bool canSabotage)
        => role == RoleId.Samurai && !canSabotage;

    private static bool IsHostSHR => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
        && ModeManager.IsMode(ModeId.SuperHostRoles);

    [HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.UpdateSystem))]
    public static class VentilationPatch
    {
        public static bool Prefix(PlayerControl player, MessageReader msgReader)
        {
            if (!IsHostSHR || player?.Data == null) return true;
            ExPlayerControl ex = player;
            bool waveBusy = ex.GetAbility<WaveCannonAbility>()?.SHRController?.Busy == true;
            if (!waveBusy && (ex.Role != RoleId.Samurai || Samurai.SamuraiCanUseVent)) return true;
            var reader = MessageReader.Get(msgReader);
            VentilationSystem.Operation operation;
            byte vent;
            try
            {
                reader.ReadUInt16();
                operation = (VentilationSystem.Operation)reader.ReadByte();
                vent = reader.ReadByte();
            }
            finally { reader.Recycle(); }
            if (!(waveBusy && operation is VentilationSystem.Operation.Enter or VentilationSystem.Operation.Move)
                && !RejectVent(ex.Role, Samurai.SamuraiCanUseVent, operation)) return true;
            player.MyPhysics.RpcBootFromVent(vent);
            Logger.Debug($"Vent rejected player={player.PlayerId} vent={vent} operation={operation}", "SHR.Samurai");
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
    public static class VentRpcPatch
    {
        public static bool Prefix(PlayerPhysics __instance, byte callId, MessageReader reader)
        {
            if (!IsHostSHR || callId != (byte)RpcCalls.EnterVent || __instance.myPlayer?.Data == null) return true;
            ExPlayerControl player = __instance.myPlayer;
            if (player.GetAbility<WaveCannonAbility>()?.SHRController?.Busy != true
                && !RejectVent(player.Role, Samurai.SamuraiCanUseVent, VentilationSystem.Operation.Enter)) return true;
            var copy = MessageReader.Get(reader);
            int vent;
            try { vent = copy.ReadPackedInt32(); }
            finally { copy.Recycle(); }
            __instance.RpcBootFromVent(vent);
            return false;
        }
    }

    [HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
    public static class SabotagePatch
    {
        public static bool Prefix(PlayerControl player)
        {
            if (!IsHostSHR || player?.Data == null) return true;
            bool reject = RejectSabotage(((ExPlayerControl)player).Role, Samurai.SamuraiCanSabotage);
            if (reject) Logger.Debug($"Sabotage rejected player={player.PlayerId}", "SHR.Samurai");
            return !reject;
        }
    }
}
