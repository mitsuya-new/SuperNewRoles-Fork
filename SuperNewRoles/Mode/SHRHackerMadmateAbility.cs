using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRHackerMadmateAbility : AbilityBase
{
    private float nextUpdate;
    private MadmateAbility Madmate => Player.GetAbility<MadmateAbility>();
    internal RoleTypes ControlRole => VentRole(Madmate?.MadData.CouldUseVent == true);
    internal bool KnowsImpostors { get; private set; }
    internal static RoleTypes VentRole(bool vent) => vent ? RoleTypes.Engineer : RoleTypes.Crewmate;
    internal static bool IsCreation(RoleId source, RoleId target) => source == RoleId.EvilHacker && target == RoleId.Madmate;
    internal static float CrewVision(bool impostorVision, float crew, float impostor, float minimum, float maximum, float light)
        => impostorVision ? impostor * maximum / System.Math.Max(0.001f, minimum + (maximum - minimum) * light) : crew;

    internal static void OnCreated(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player?.Role != RoleId.Madmate || !SHRPlayerOptions.IsVanillaGuest(player.Player)) return;
        if (player.GetAbility<SHRHackerMadmateAbility>() == null)
            player.AttachAbility(new SHRHackerMadmateAbility(), new AbilityParentRole(player, player.roleBase));
        SHRTestRoleAbility.SendRoleToAll(player.Player, RoleTypes.Crewmate);
        SHRTestRoleAbility.SetSelfRole(player.Player, player.GetAbility<SHRHackerMadmateAbility>().ControlRole);
        SHRPlayerOptions.Send(player.Player, resetAbility: true);
        SHRRoleNameDisplay.Refresh();
    }

    public override void AttachToAlls() => SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);

    private void Tick()
    {
        var client = AmongUsClient.Instance;
        if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || Player.IsDead()
            || Time.unscaledTime < nextUpdate || Madmate == null) return;
        nextUpdate = Time.unscaledTime + 0.25f;
        var (complete, all) = ModHelpers.TaskCompletedData(Player.Data);
        bool known = Madmate.MadData.CouldKnowImpostors(complete, all);
        if (known == KnowsImpostors) return;
        KnowsImpostors = known;
        SHRRoleNameDisplay.Refresh();
    }

    internal static bool RejectRepair(SystemTypes system, byte amount, bool lights, bool comms, bool reactor)
    {
        if ((amount & 128) != 0) return false;
        int operation = amount & 240;
        return system switch
        {
            SystemTypes.Electrical => lights && amount < 5,
            SystemTypes.Comms => comms && (amount == 0 || operation is 16 or 64),
            SystemTypes.Reactor or SystemTypes.Laboratory or SystemTypes.LifeSupp or SystemTypes.HeliSabotage
                => reactor && operation is 16 or 64,
            _ => false,
        };
    }

    private static bool Reject(ShipStatus ship, SystemTypes type, PlayerControl player, byte amount)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player?.Data == null || player.Data.IsDead || player.Data.Disconnected
            || ((ExPlayerControl)player).GetAbility<SHRHackerMadmateAbility>() == null
            || !RejectRepair(type, amount, MadmateOptions.MadmateCannotFixElectrical,
                MadmateOptions.MadmateCannotFixComms, MadmateOptions.MadmateCannotFixReactor)) return false;
        if (ship.Systems.TryGetValue(type, out var system))
        {
            if (system.TryCast<SwitchSystem>() is { } lights) lights.IsDirty = true;
            if (system.TryCast<ReactorSystemType>() is { } reactor) reactor.IsDirty = true;
            if (system.TryCast<LifeSuppSystemType>() is { } oxygen) oxygen.IsDirty = true;
            if (system.TryCast<HeliSabotageSystem>() is { } heli) heli.IsDirty = true;
            if (system.TryCast<HqHudSystemType>() is { } hq) hq.IsDirty = true;
            if (system.TryCast<HudOverrideSystemType>() is { } comms) comms.IsDirty = true;
        }
        return true;
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), new[] { typeof(SystemTypes), typeof(PlayerControl), typeof(byte) })]
    public static class ByteRepairPatch
    {
        public static bool Prefix(ShipStatus __instance, SystemTypes systemType, PlayerControl player, byte amount)
            => !Reject(__instance, systemType, player, amount);
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), new[] { typeof(SystemTypes), typeof(PlayerControl), typeof(MessageReader) })]
    public static class ReaderRepairPatch
    {
        public static bool Prefix(ShipStatus __instance, SystemTypes systemType, PlayerControl player, MessageReader msgReader)
        {
            if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || player?.Data == null || ((ExPlayerControl)player).GetAbility<SHRHackerMadmateAbility>() == null) return true;
            var copy = MessageReader.Get(msgReader);
            try { return !Reject(__instance, systemType, player, copy.ReadByte()); }
            finally { copy.Recycle(); }
        }
    }
}
