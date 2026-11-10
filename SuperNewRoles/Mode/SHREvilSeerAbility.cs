using System;
using System.Collections.Generic;
using System.Linq;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Roles.CrewMate;
using SuperNewRoles.Roles.Impostor;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHREvilSeerAbility : AbilityBase
{
    private sealed record Soul(Vector2 Position, Color Color, float? ExpiresAt);
    private readonly List<Soul> souls = new();
    private bool initialized;
    private float readyAt;
    private float flashUntil;
    private string flashText = "";
    private CustomModifierGrantedButtonAbility Grant => Player.GetAbility<CustomModifierGrantedButtonAbility>();
    internal float Remaining => Math.Max(0f, readyAt - Time.unscaledTime);
    internal static bool CanGiveHaunt(EvilSeerHauntMode mode) => mode is EvilSeerHauntMode.Both or EvilSeerHauntMode.OnlyOthersHaunt;
    internal static bool SoulVisible(float now, float? expiresAt) => !expiresAt.HasValue || now < expiresAt.Value;

    internal static void AttachIfSupported(ExPlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || player.Role != RoleId.EvilSeer || !SHRPlayerOptions.IsVanillaGuest(player.Player)
            || player.GetAbility<SHREvilSeerAbility>() != null) return;
        player.AttachAbility(new SHREvilSeerAbility(), new AbilityParentRole(player, player.roleBase));
    }

    public override void AttachToAlls()
    {
        Player.AttachAbility(new SHRAbilityTriggerAbility(SHRAbilityTrigger.Pet, _ => Give(), CanUse), new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, Tick);
        SubscribeWithAbility(DieEvent.Instance, OnDeath);
        SubscribeWithAbility(WrapUpEvent.Instance, _ => Reset());
    }

    private ExPlayerControl Target() => Grant == null ? null : TargetCustomButtonBase.SetTarget(targetingPlayer: Player.Player,
        isTargetable: Grant.CanGrantFor);
    private bool CanUse() => initialized && Remaining <= 0f && Player.IsAlive() && Target() != null;
    private void Give()
    {
        if (!CanUse() || Grant.TryGrantModifier(Target()) != true) return;
        readyAt = Time.unscaledTime + EvilSeer.EvilSeerCreateAbilityCooldown;
        SHRRoleNameDisplay.Refresh();
    }

    private void Reset()
    {
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(EvilSeer.EvilSeerCreateAbilityCooldown, GameSettingOptions.InitialCooldown);
        SHRRoleNameDisplay.Refresh();
    }

    private void Tick()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || Player?.Data == null || Player.Data.Disconnected || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (!initialized) { initialized = true; Reset(); }
    }

    private void OnDeath(DieEventData data)
    {
        if (!Player.IsAlive() || data.player?.Data == null || MeetingHud.Instance != null || ExileController.Instance != null) return;
        var seer = Player.GetAbility<SeerAbility>();
        if (seer == null) return;
        if (seer.Data.Mode is SeerMode.Both or SeerMode.SoulOnly)
            souls.Add(new Soul(data.player.transform.position, Palette.PlayerColors[seer.SoulColorId(data.player)],
                seer.Data.LimitSoulDuration ? Time.unscaledTime + seer.Data.SoulDuration : null));
        if (seer.Data.Mode is SeerMode.Both or SeerMode.FlashOnly)
        {
            SHRCrewRoleAbility.Flash(Player.Player, 1f);
            flashText = seer.FlashColorText(data.player) ?? "";
            flashUntil = Time.unscaledTime + 3f;
        }
        SHRRoleNameDisplay.Refresh();
    }

    internal string Status
    {
        get
        {
            if (!Player.IsAlive() || MeetingHud.Instance != null) return "";
            souls.RemoveAll(soul => !SoulVisible(Time.unscaledTime, soul.ExpiresAt));
            string text = Time.unscaledTime < flashUntil ? flashText : "";
            foreach (var soul in souls) text += ColoredArrow(soul.Color, "◆" + Direction(soul.Position));
            var arrows = Player.GetAbility<DeadBodyArrowsAbility>();
            if (arrows?.ShowArrows == true)
                foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>().Where(DeadBodyArrowsAbility.IsTrackableDeadBody)
                    .GroupBy(body => body.ParentId).Select(group => group.First()))
                    text += ColoredArrow(arrows.ResolveDeadBodyArrowColor(body), Direction(body.transform.position));
            if (CanGiveHaunt(EvilSeer.EvilSeerHauntMode) && Grant?.Count > 0)
                text += " [" + ModTranslation.GetString("EvilSeerHauntButtonText") + ": " + Mathf.CeilToInt(Remaining) + "]";
            return text.Length == 0 ? "" : "\n" + text;
        }
    }

    private string Direction(Vector2 target) => SHRBatchRoleAbility.DirectionArrow(target.x - Player.transform.position.x, target.y - Player.transform.position.y);
    private static string ColoredArrow(Color color, string glyph) => "<#" + ColorUtility.ToHtmlStringRGB(color) + ">" + glyph + "</color>";
    public override void DetachToAlls() => souls.Clear();
}
