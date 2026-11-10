using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Crewmate;
using SuperNewRoles.Roles.CrewMate;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRCrewRoleAbility : AbilityBase
{
    private bool initialized;
    private float readyAt;
    private float toiletInputAt;
    private float lastVentAt;
    private int lastVent = -1;
    private byte lastVentPlayer;
    private int analyses;
    private int repairs;
    private readonly HashSet<int> pending = new();
    private readonly Dictionary<int, List<byte>> ventUsers = new();
    private readonly Dictionary<byte, string> names = new();
    private static readonly Dictionary<byte, float> FlashUntil = new();

    internal static bool Supports(RoleId role) => role is RoleId.NiceGuesser or RoleId.Chief or RoleId.Mayor
        or RoleId.Celebrity or RoleId.ElectionCommissioner or RoleId.ToiletFan or RoleId.HamburgerShop
        or RoleId.Bait or RoleId.BestFalseCharge or RoleId.Seer or RoleId.NiceButtoner
        or RoleId.HomeSecurityGuard or RoleId.Stuntman or RoleId.Technician
        or RoleId.SatsumaAndImo or RoleId.NiceNekomata
        or RoleId.Datahacker or RoleId.SpeedBooster or RoleId.Bakery or RoleId.MeetingSheriff or RoleId.Lighter or RoleId.HauntedWolf
        or RoleId.Moving or RoleId.NiceTeleporter or RoleId.Doorr or RoleId.DyingMessenger or RoleId.Clergy or RoleId.WiseMan or RoleId.Santa or RoleId.Balancer;

    internal static bool HasCrewImpostorControls(RoleId role) => role is RoleId.Sheriff or RoleId.Chief or RoleId.Doorr;

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || (!SHRPlayerOptions.IsVanillaGuest(player.Player) && player.Role is not (RoleId.Celebrity or RoleId.Chief or RoleId.NiceButtoner or RoleId.ToiletFan)) || !Supports(player.Role)
            || player.GetAbility<SHRCrewRoleAbility>() != null) return;
        if (player.Role == RoleId.Celebrity && Celebrity.CelebrityYellowChangedRole)
            player.AttachAbility(new SHRCrewRoleAbility(), new AbilityParentPlayer(player));
        else player.AttachAbility(new SHRCrewRoleAbility(), new AbilityParentRole(player, player.roleBase));
    }

    internal float RemainingCooldown => Mathf.Max(0f, readyAt - Time.unscaledTime);
    internal static float Cooldown(RoleId role) => role switch
    {
        RoleId.Chief => Chief.ChiefAppointCooldown,
        RoleId.ToiletFan => ToiletFan.ToiletFanCoolTime,
        RoleId.NiceButtoner => NiceButtoner.NiceButtonerCooldown,
        RoleId.SilverBullet => SilverBullet.SilverBulletCoolTime,
        _ => 0f
    };

    public override void AttachToAlls()
    {
        analyses = SilverBullet.SilverBulletAnalysisCount;
        repairs = SilverBullet.SilverBulletCanUseRepair ? SilverBullet.SilverBulletRepairCount : 0;
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(MeetingStartEvent.Instance, _ => OnMeeting());
        SubscribeWithAbility(WrapUpEvent.Instance, _ => OnWrapUp());
        SubscribeWithAbility(DieEvent.Instance, OnDeath);
        SubscribeWithAbility(VentEnterEvent.Instance, data => RecordVent(data.player.PlayerId, data.vent.Id));
        SubscribeWithAbility(SaboEndEvent.Instance, _ => ExitTechnicianVent());
        if (Player.Role == RoleId.SilverBullet)
            Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => UseSilverBullet(), CanUseSilverBullet),
                new AbilityParentAbility(this));
        if (Player.Role == RoleId.NiceButtoner)
            Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => UseNiceButtoner(), CanUseNiceButtoner),
                new AbilityParentAbility(this));
        if (Player.Role == RoleId.ToiletFan)
            Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => UseToiletFan(), CanUseToiletFan),
                new AbilityParentAbility(this));
    }

    private void ResetCooldown()
    {
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(Cooldown(Player.Role), GameSettingOptions.InitialCooldown);
        if (Player.Role == RoleId.ToiletFan)
            SHRPlayerOptions.Send(Player.Player, abilityCooldown: RemainingCooldown, resetAbility: true);
    }

    private void Tick()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (!initialized)
        {
            initialized = true;
            ResetCooldown();
        }
        if (SHRAntiBlackout.Active) { names.Clear(); return; }
        if (Player.Role == RoleId.Chief) UpdateChiefName();
        if (Player.Role != RoleId.Celebrity && !Player.HasAbility<AlwaysCelebrityAbility>()) return;
        UpdateCelebrityNames();
    }

    private void UpdateChiefName()
    {
        if (!SHRPlayerOptions.IsVanillaGuest(Player.Player)) return;
        if (SHRAntiBlackout.Active) { names.Clear(); return; }
        var sheriff = Player.GetAbility<ChiefAbility>()?.VisibleSheriff;
        if (sheriff == null) return;
        string name = GetChiefDisplayName(Player.Player, sheriff.Player);
        if (name == null) { names.Clear(); return; }
        if (names.TryGetValue(sheriff.PlayerId, out string previous) && previous == name) return;
        sheriff.Player.RpcSetNamePrivate(name, Player.Player);
        names[sheriff.PlayerId] = name;
    }

    internal static string GetChiefDisplayName(PlayerControl viewer, PlayerControl subject)
    {
        if (SHRCamouflagerController.IsActive && MeetingHud.Instance == null) return null;
        ExPlayerControl chief = viewer;
        ExPlayerControl sheriff = subject;
        if (chief?.GetAbility<ChiefAbility>()?.CanIdentifySheriff(sheriff) != true) return null;
        return SHRRoleNameDisplay.FormatName(NameText.GetRoleInfoText(sheriff),
            ModHelpers.Cs(Sheriff.Instance.RoleColor, SHRCamouflagerController.OriginalName(subject)), MeetingHud.Instance != null);
    }

    private bool CanUseToiletFan() => initialized && Player.IsAlive() && Time.unscaledTime >= readyAt && Time.unscaledTime >= toiletInputAt
        && GameOptionsManager.Instance.CurrentGameOptions.MapId == (byte)MapNames.Airship;

    private void UseToiletFan()
    {
        if (!CanUseToiletFan()) return;
        toiletInputAt = Time.unscaledTime + 0.35f;
        readyAt = Time.unscaledTime + Cooldown(Player.Role);
        foreach (byte door in new byte[] { 79, 80, 81, 82 }) UpdateSystem(SystemTypes.Doors, door);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: RemainingCooldown, resetAbility: true);
    }

    internal static void HandleVentAbility(ExPlayerControl player)
    {
        if (player != null && SHRCrewActionAbility.UsesEngineer(player.Role) && SHRAbilityTriggerPatches.CanActivate(player.Player, allowVentInput: true))
            player.GetAbility<SHRCrewActionAbility>()?.ActivateFromVent();
        if (player != null && SHRCrewBoostAbility.UsesEngineer(player.Role) && SHRAbilityTriggerPatches.CanActivate(player.Player, allowVentInput: true))
            player.GetAbility<SHRCrewBoostAbility>()?.Activate();
        if (player?.Role == RoleId.ToiletFan && SHRAbilityTriggerPatches.CanActivate(player.Player))
            player.GetAbility<SHRCrewRoleAbility>()?.UseToiletFan();
    }

    internal static void RefreshVentCooldown(ExPlayerControl player)
    {
        if (player?.GetAbility<SHRCrewActionAbility>() is { } action && SHRCrewActionAbility.UsesEngineer(player.Role))
            SHRPlayerOptions.Send(player.Player, abilityCooldown: action.Remaining, resetAbility: true);
        if (player?.GetAbility<SHRCrewBoostAbility>() is { } boost)
            SHRPlayerOptions.Send(player.Player, abilityCooldown: boost.Remaining, resetAbility: true);
        if (player?.Role == RoleId.ToiletFan && player.GetAbility<SHRCrewRoleAbility>() is { } ability)
            SHRPlayerOptions.Send(player.Player, abilityCooldown: ability.RemainingCooldown, resetAbility: true);
    }

    private bool CanUseNiceButtoner() => initialized && Player.IsAlive() && Time.unscaledTime >= readyAt
        && Player.GetAbility<ButtonerAbility>()?.HasCount == true && !ModHelpers.IsSabotageAvailable();

    private void UseNiceButtoner()
    {
        if (!CanUseNiceButtoner()) return;
        readyAt = Time.unscaledTime + Cooldown(Player.Role);
        Player.GetAbility<ButtonerAbility>().OnClick();
        SHRRoleNameDisplay.Refresh();
    }

    private Vent NearestVent() => ShipStatus.Instance?.AllVents?.Where(v => v != null
        && Vector2.Distance(Player.Player.GetTruePosition(), v.transform.position) <= 1.5f
        && !PhysicsHelpers.AnythingBetween(Player.Player.GetTruePosition(), v.transform.position, Constants.ShipAndObjectsMask, false))
        .OrderBy(v => Vector2.Distance(Player.Player.GetTruePosition(), v.transform.position)).FirstOrDefault();

    private bool CanUseSilverBullet() => initialized && Player.IsAlive() && Time.unscaledTime >= readyAt
        && ((analyses > 0 && NearestVent() != null) || (repairs > 0 && ModHelpers.IsSabotageAvailable()));

    private void UseSilverBullet()
    {
        if (!CanUseSilverBullet()) return;
        var vent = NearestVent();
        if (vent != null && analyses > 0)
        {
            pending.Add(vent.Id);
            analyses--;
        }
        else
        {
            repairs--;
            RepairSabotages(Player.Player);
        }
        readyAt = Time.unscaledTime + Cooldown(Player.Role);
        SHRRoleNameDisplay.Refresh();
    }

    private void RecordVent(byte playerId, int ventId)
    {
        if (Player.Role != RoleId.SilverBullet) return;
        if (lastVent == ventId && lastVentPlayer == playerId && Time.unscaledTime - lastVentAt < 0.2f) return;
        lastVent = ventId;
        lastVentPlayer = playerId;
        lastVentAt = Time.unscaledTime;
        if (!ventUsers.TryGetValue(ventId, out var users)) ventUsers[ventId] = users = new();
        users.Add(playerId);
    }

    private void OnMeeting()
    {
        names.Clear();
        if (Player.Role != RoleId.SilverBullet) return;
        var reports = pending.Select(vent =>
        {
            if (!ventUsers.TryGetValue(vent, out var users) || users.Count == 0)
                return ModTranslation.GetString("SilverBulletVentNotUsed");
            string report = string.Format(ModTranslation.GetString("SilverBulletVentUsed"), users.Count);
            if (SilverBullet.SilverBulletAnalysisLight)
                for (int i = 0; i < users.Count; i++)
                {
                    var user = ExPlayerControl.ById(users[i]);
                    if (user != null) report += "\n" + string.Format(ModTranslation.GetString("SilverBulletVentUsedColor"
                        + (CustomCosmetics.CustomColors.IsLighter(user) ? "Light" : "Dark")), i + 1);
                }
            return report;
        }).ToArray();
        pending.Clear();
        ventUsers.Clear();
        if (reports.Length > 0)
            new LateTask(() =>
            {
                if (Player?.Data != null && !Player.Data.Disconnected && MeetingHud.Instance != null)
                    SHRMeetingChat.Reply(Player.Player, string.Join("\n\n", reports), ModTranslation.GetString("SilverBullet"));
            }, 0.5f, "SHR SilverBullet report");
    }

    private void OnWrapUp()
    {
        names.Clear();
        ResetCooldown();
        if (Player.Role == RoleId.BestFalseCharge && Player.IsAlive())
            new LateTask(() =>
            {
                if (Player?.Data != null && Player.IsAlive() && !Player.Data.Disconnected
                    && Player.GetAbility<SHRCrewRoleAbility>() == this)
                {
                    SHRVanillaRpcRouter.Exile(Player);
                    FinalStatusManager.SetFinalStatus(Player, FinalStatus.FalseCharge);
                }
            }, 0.1f, "SHR BestFalseCharge exile");
        if (Player.Role == RoleId.HomeSecurityGuard && Player.IsAlive())
            SHRMeetingChat.Reply(Player.Player, ModTranslation.GetString("HomeSecurityGuardTask" + UnityEngine.Random.Range(1, 13)));
    }

    private void ExitTechnicianVent()
    {
        if (Player.Role != RoleId.Technician || !Player.Player.inVent || ModHelpers.IsSabotageAvailable(
            isMushroomMixAsSabotage: Technician.TechnicianCanUseVentOnMushroomMixup)) return;
        var vent = ShipStatus.Instance.AllVents.OrderBy(v => Vector2.Distance(v.transform.position, Player.Player.GetTruePosition())).FirstOrDefault();
        if (vent != null) SHRVentRecovery.Begin(Player, vent.Id);
    }

    private void OnDeath(DieEventData data)
    {
        if (Player.Role != RoleId.Seer || !Player.IsAlive() || ExileController.Instance != null || MeetingHud.Instance != null) return;
        Flash(Player.Player, 1.5f);
    }

    private void UpdateCelebrityNames()
    {
        if (SHRCamouflagerController.IsActive && MeetingHud.Instance == null) { names.Clear(); return; }
        string name = ModHelpers.Cs(Celebrity.Instance.RoleColor, Player.Data.PlayerName);
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (viewer == Player.Player || !SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            if (names.TryGetValue(viewer.PlayerId, out string previous) && previous == name) continue;
            Player.Player.RpcSetNamePrivate(name, viewer);
            names[viewer.PlayerId] = name;
        }
    }

    private void UpdateSystem(SystemTypes system, byte amount) => UpdateSystem(Player.Player, system, amount);

    internal static void UpdateSystem(PlayerControl actor, SystemTypes system, byte amount)
    {
        var ship = ShipStatus.Instance;
        ship.UpdateSystem(system, actor, amount);
        var client = AmongUsClient.Instance;
        var writer = client.StartRpcImmediately(ship.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable);
        writer.Write((byte)system);
        writer.WriteNetObject(actor);
        writer.Write(amount);
        client.FinishRpcImmediately(writer);
    }

    internal static void RepairSabotages(PlayerControl actor)
    {
        if (AmongUsClient.Instance?.AmHost != true || ShipStatus.Instance == null) return;
        void UpdateSystem(SystemTypes system, byte amount) => SHRCrewRoleAbility.UpdateSystem(actor, system, amount);
        var ship = ShipStatus.Instance;
        if (ship.Systems.TryGetValue(SystemTypes.Electrical, out var electrical) && electrical.TryCast<SwitchSystem>() is { } lights)
            for (byte bit = 0; bit < 5; bit++)
                if (((lights.ActualSwitches ^ lights.ExpectedSwitches) & (1 << bit)) != 0) UpdateSystem(SystemTypes.Electrical, bit);
        foreach (var type in new[] { SystemTypes.LifeSupp, SystemTypes.Reactor, SystemTypes.Laboratory, SystemTypes.HeliSabotage })
            if (ship.Systems.TryGetValue(type, out var system) && system.TryCast<ICriticalSabotage>()?.IsActive == true)
            {
                if (type == SystemTypes.LifeSupp) { UpdateSystem(type, 64); UpdateSystem(type, 65); }
                else { UpdateSystem(type, 16); if (type == SystemTypes.HeliSabotage) UpdateSystem(type, 17); }
            }
        if (ModHelpers.IsComms())
        {
            UpdateSystem(SystemTypes.Comms, 16);
            if (GameOptionsManager.Instance.CurrentGameOptions.MapId is 1 or 5) UpdateSystem(SystemTypes.Comms, 17);
        }
        if (actor.IsMushroomMixupActive()) UpdateSystem(SystemTypes.MushroomMixupSabotage, 17);
    }

    internal static SystemTypes FlashSystem(int map) => map == 2 ? SystemTypes.Laboratory
        : map == 4 ? SystemTypes.HeliSabotage : SystemTypes.Reactor;

    internal static void Flash(PlayerControl viewer, float duration)
    {
        var client = AmongUsClient.Instance;
        var ship = ShipStatus.Instance;
        if (client?.AmHost != true || !SHRPlayerOptions.IsVanillaGuest(viewer) || ship == null) return;
        var system = FlashSystem(GameOptionsManager.Instance.CurrentGameOptions.MapId);
        bool RealAlarm() => ship.Systems.TryGetValue(system, out var data) && data.TryCast<ICriticalSabotage>()?.IsActive == true;
        if (RealAlarm()) return;
        int gameId = client.GameId;
        float until = Time.unscaledTime + duration + 0.1f;
        FlashUntil[viewer.PlayerId] = until;
        void Send(byte amount)
        {
            int id = client.GetClientIdFromCharacter(viewer);
            if (id < 0) return;
            var writer = client.StartRpcImmediately(ship.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, id);
            writer.Write((byte)system);
            writer.WriteNetObject(viewer);
            writer.Write(amount);
            client.FinishRpcImmediately(writer);
        }
        Send(128);
        new LateTask(() =>
        {
            if (AmongUsClient.Instance != client || client.GameId != gameId || client.GameState != InnerNetClient.GameStates.Started
                || viewer?.Data == null || viewer.Data.Disconnected || ShipStatus.Instance != ship || RealAlarm()
                || !FlashUntil.TryGetValue(viewer.PlayerId, out float current) || current != until) return;
            FlashUntil.Remove(viewer.PlayerId);
            Send(16);
            if (system == SystemTypes.HeliSabotage) Send(17);
        }, duration + 0.1f, "SHR restore death flash");
    }

    internal string Status => Player.Role switch
    {
        RoleId.SatsumaAndImo => Player.GetAbility<SatsumaAndImoAbility>()?.IsMadTeam == true ? " (M)" : " (C)",
        RoleId.SilverBullet => $" [調査:{analyses} 修理:{repairs}]",
        RoleId.NiceButtoner => $" [残り:{Math.Max(0, Player.GetAbility<ButtonerAbility>()?.Count ?? 0)}]",
        _ => ""
    };

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
    public static class VentPatch
    {
        public static void Prefix(PlayerPhysics __instance, byte callId, MessageReader reader)
        {
            if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
                || callId != (byte)RpcCalls.EnterVent || __instance.myPlayer == null) return;
            var copy = MessageReader.Get(reader);
            int vent;
            try { vent = copy.ReadPackedInt32(); }
            finally { copy.Recycle(); }
            ExPlayerControl actor = __instance.myPlayer;
            if (!actor.CanUseVent()) return;
            foreach (var player in ExPlayerControl.ExPlayerControls)
                player.GetAbility<SHRCrewRoleAbility>()?.RecordVent(__instance.myPlayer.PlayerId, vent);
        }
    }
}
