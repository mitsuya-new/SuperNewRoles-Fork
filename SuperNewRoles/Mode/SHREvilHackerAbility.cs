using System;
using System.Collections.Generic;
using System.Linq;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

internal sealed class SHRAdminPages
{
    internal int Page { get; private set; } = -1;
    internal bool Open => Page >= 0;
    internal void Close() => Page = -1;
    internal void Next(int pages) => Page = pages <= 0 || Page + 1 >= pages ? -1 : Page + 1;
    internal void Clamp(int pages)
    {
        if (pages <= 0) Close();
        else if (Open) Page = Math.Min(Page, pages - 1);
    }
    internal void Rotate(int pages) { if (Open && pages > 0) Page = (Page + 1) % pages; }
    internal static bool CanRead(bool comms, bool enhanced, bool duringComms)
        => !comms || enhanced && duringComms;
    internal static bool CanReadInMeeting(bool enhanced, bool duringMeeting)
        => enhanced && duringMeeting;
}

public sealed class SHREvilHackerAbility : AbilityBase
{
    private readonly SHRAdminPages pages = new();
    private readonly List<string> lines = new();
    private bool initialized;
    private bool frozen;
    private float readyAt;
    private float nextSnapshot;
    private float nextMeetingPage;
    private CustomSidekickButtonAbility Grant => Player.GetAbility<CustomSidekickButtonAbility>();
    internal float Remaining => Math.Max(0f, readyAt - Time.unscaledTime);
    internal bool HasMadmateUse => EvilHacker.EvilHackerCanCreateMadmate && Grant?.Count > 0;
    internal bool FreezesMovement => pages.Open && !EvilHacker.EvilHackerCanMoveWhileUsingAdmin && MeetingHud.Instance == null;
    private int PageCount => Math.Max(1, (lines.Count + 4) / 5);

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player.Role != RoleId.EvilHacker || !SHRPlayerOptions.IsVanillaGuest(player.Player)
            || player.GetAbility<SHREvilHackerAbility>() != null) return;
        player.AttachAbility(new SHREvilHackerAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => NextPage(), () => initialized && Player.IsAlive()), new AbilityParentAbility(this));
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Shapeshift, (_, target) => CreateMadmate(target), CanCreate), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(MeetingStartEvent.Instance, _ =>
        {
            Close();
            if (SHRAdminPages.CanReadInMeeting(EvilHacker.EvilHackerHasEnhancedAdmin, EvilHacker.EvilHackerCanUseAdminInMeeting))
            {
                Capture();
                pages.Next(PageCount);
                nextMeetingPage = Time.unscaledTime + 2.5f;
                SHRRoleNameDisplay.Refresh();
            }
        });
        SubscribeWithAbility(WrapUpEvent.Instance, _ => { Close(); ResetCooldown(); });
    }

    private bool CanCreate() => initialized && Player.IsAlive() && !FreezesMovement && Remaining <= 0f
        && HasMadmateUse;

    private void ResetCooldown()
    {
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(EvilHacker.EvilHackerCreateMadmateCooldown, GameSettingOptions.InitialCooldown);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: Player.IsAlive());
    }

    private void CreateMadmate(PlayerControl selected)
    {
        if (!CanCreate()) return;
        var target = TargetCustomButtonBase.SetTarget(targetingPlayer: Player.Player,
            isTargetable: candidate => candidate.Player == selected && Grant.CanCreateSidekickFor(candidate));
        if (target == null || !Grant.TryCreateSidekick(target)) return;
        SHRTestRoleAbility.SetSelfRole(Player.Player, AmongUs.GameOptions.RoleTypes.Impostor);
        readyAt = Time.unscaledTime + EvilHacker.EvilHackerCreateMadmateCooldown;
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: true);
        SHRRoleNameDisplay.Refresh();
    }

    private void NextPage()
    {
        if (!pages.Open && ModHelpers.IsComms() && !EvilHacker.EvilHackerCanUseAdminDuringComms) return;
        Capture();
        pages.Next(PageCount);
        ApplyMovement();
        SHRRoleNameDisplay.Refresh();
    }

    private void Close()
    {
        pages.Close();
        ApplyMovement();
        SHRRoleNameDisplay.Refresh();
    }

    private void ApplyMovement()
    {
        bool value = FreezesMovement;
        if (frozen == value) return;
        frozen = value;
        SHRPlayerOptions.Send(Player.Player);
    }

    private void Capture()
    {
        lines.Clear();
        if (!SHRAdminPages.CanRead(ModHelpers.IsComms(), EvilHacker.EvilHackerHasEnhancedAdmin, EvilHacker.EvilHackerCanUseAdminDuringComms))
        {
            pages.Clamp(1);
            return;
        }
        var snapshot = SHRAdminSnapshot.Capture(Player, true, true);
        var doors = new Dictionary<SystemTypes, int>();
        if (EvilHacker.EvilHackerHasEnhancedAdmin && EvilHacker.EvilHackerShowDoorInfoOnMap && ShipStatus.Instance?.AllDoors != null)
            foreach (var door in ShipStatus.Instance.AllDoors)
                if (door != null && !door.IsOpen)
                    doors[door.Room] = doors.GetValueOrDefault(door.Room) + 1;
        foreach (var pair in snapshot.OrderBy(room => (byte)room.Key))
        {
            int closedDoors = doors.GetValueOrDefault(pair.Key);
            if (pair.Value.Total == 0 && closedDoors == 0) continue;
            var details = pair.Value.VisibleDetails(EvilHacker.EvilHackerHasEnhancedAdmin,
                EvilHacker.EvilHackerCanSeeImpostorsOnAdmin, EvilHacker.EvilHackerCanSeeDeadBodiesOnAdmin);
            string name = FastDestroyableSingleton<TranslationController>.Instance.GetString(pair.Key);
            string text = name + ":" + pair.Value.Total;
            if (details.Impostors > 0) text += " <#f00>●" + details.Impostors + "</color>";
            if (details.Corpses > 0) text += " <#888>●" + details.Corpses + "</color>";
            if (closedDoors > 0) text += " ■" + closedDoors;
            lines.Add(text);
        }
        pages.Clamp(PageCount);
    }

    private void Tick()
    {
        var client = AmongUsClient.Instance;
        if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver || Player?.Data == null
            || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (!initialized)
        {
            initialized = true;
            ResetCooldown();
            if (!HasMadmateUse) SHRTestRoleAbility.SetSelfRole(Player.Player, AmongUs.GameOptions.RoleTypes.Impostor);
        }
        if (Player.IsDead() || Player.Data.Disconnected || ExileController.Instance != null) { if (pages.Open || frozen) Close(); return; }
        if (!pages.Open || Time.unscaledTime < nextSnapshot) return;
        nextSnapshot = Time.unscaledTime + 0.5f;
        Capture();
        if (MeetingHud.Instance != null && Time.unscaledTime >= nextMeetingPage)
        {
            pages.Rotate(PageCount);
            nextMeetingPage = Time.unscaledTime + 2.5f;
        }
        ApplyMovement();
        SHRRoleNameDisplay.Refresh();
    }

    internal string Status
    {
        get
        {
            if (!pages.Open) return "";
            string header = FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.Admin);
            string text = string.Join("\n", lines.Skip(pages.Page * 5).Take(5));
            return "\n" + header + " (" + (pages.Page + 1) + "/" + PageCount + ")\n" + (text.Length == 0 ? "—" : text);
        }
    }

    public override void DetachToAlls() => Close();
}
