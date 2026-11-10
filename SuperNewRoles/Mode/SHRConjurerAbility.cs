using System;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRConjurerAbility : AbilityBase
{
    private bool initialized;
    private float readyAt;
    private ConjurerAbility Conjurer => Player.GetAbility<ConjurerAbility>();
    internal float Remaining => RemainingAt(Conjurer?.Count ?? 0, Time.unscaledTime, readyAt);
    internal static float RemainingAt(int count, float now, float readyAt) => count == 3 ? 0f : Math.Max(0f, readyAt - now);

    internal static bool CanPlace(int count, float distance, float limit)
        => count >= 0 && count < 3 && (count == 0 || distance < limit);

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player.Role != RoleId.Conjurer || !SHRPlayerOptions.IsVanillaGuest(player.Player)
            || player.GetAbility<SHRConjurerAbility>() != null) return;
        player.AttachAbility(new SHRConjurerAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Shapeshift | SHRAbilityTrigger.ShapeshiftSelf,
            _ => Use(), CanUse), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(PreCalledMeetingEvent.Instance, _ => Reset());
        SubscribeWithAbility(WrapUpEvent.Instance, _ => Reset());
    }

    private bool CanUse()
    {
        if (!initialized || !Player.IsAlive() || Remaining > 0f || Conjurer == null) return false;
        int count = Conjurer.Count;
        return count == 3 || CanPlace(count, count > 0
            ? Vector2.Distance(Player.transform.position, Conjurer.Positions[count - 1]) : 0f, Conjurer.Data.CanAddLength);
    }

    private void Reset()
    {
        if (Conjurer != null) Conjurer.Count = 0;
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(Roles.Impostor.Conjurer.ConjurerBeaconCooldown, GameSettingOptions.InitialCooldown);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: Remaining, resetAbility: Player.IsAlive());
        SHRRoleNameDisplay.Refresh();
    }

    private void Use()
    {
        if (!CanUse()) return;
        if (Conjurer.Count == 3) Conjurer.StartTriangle();
        else
        {
            Conjurer.AddBeacon(Player.transform.position);
            readyAt = Time.unscaledTime + Conjurer.Data.ConjurerBeaconCooldown;
        }
        SHRRoleNameDisplay.Refresh();
    }

    private void Tick()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (!initialized) { initialized = true; Reset(); }
        if (Player.IsDead() && Conjurer?.Count > 0) Reset();
    }

    internal string Status => " [" + ModTranslation.GetString("ConjurerBeaconName") + ": " + (Conjurer?.Count ?? 0) + "/3]";
}
