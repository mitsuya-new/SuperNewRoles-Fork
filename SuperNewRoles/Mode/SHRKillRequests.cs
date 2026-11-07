using System.Collections.Generic;
using HarmonyLib;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
public static class ShrKillRequests
{
    private static readonly Dictionary<byte, float> ReadyAt = new();
    public static void Reset() => ReadyAt.Clear();

    public static float RemainingCooldown(PlayerControl player)
        => ReadyAt.TryGetValue(player.PlayerId, out float ready)
            ? Mathf.Max(0f, ready - Time.unscaledTime)
            : InitialCooldown(ShrPlayerOptions.KillCooldown(player), GameSettingOptions.InitialCooldown);

    internal static float InitialCooldown(float cooldown, InitialCooldownType setting) => setting switch
    {
        InitialCooldownType.Immediate => 0f,
        InitialCooldownType.OneThird => cooldown / 3f,
        _ => 10f,
    };

    public static void Initialize()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost
            || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        ReadyAt.Clear();
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!ShrPlayerOptions.IsVanillaGuest(player)) continue;
            float initial = InitialCooldown(ShrPlayerOptions.KillCooldown(player), GameSettingOptions.InitialCooldown);
            ReadyAt[player.PlayerId] = Time.unscaledTime + initial;
            ShrPlayerOptions.Send(player, killCooldown: initial, resetKill: true);
        }
    }

    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || !ShrPlayerOptions.IsVanillaGuest(__instance)) return true;
        ExPlayerControl killer = __instance;
        ExPlayerControl victim = target;
        var ability = killer.GetAbility<CustomKillButtonAbility>();
        bool permitted = ability != null ? ability.CanKill() : __instance.Data.Role?.IsImpostor == true;
        if (killer.Role == SuperNewRoles.Roles.RoleId.Tasker
            && (!SuperNewRoles.Roles.Impostor.Tasker.TaskerCanKill
                || killer.GetAbility<ShrTestRoleAbility>()?.TaskerImpostor != true)) permitted = false;
        string rejection = null;
        if (!ShrAbilityTriggerPatches.CanActivate(__instance)) rejection = "actor unavailable";
        else if (target == null || target == __instance || target.Data == null || target.Data.IsDead
            || target.Data.Disconnected || target.inVent || target.onLadder || target.inMovingPlat) rejection = "target unavailable";
        else if (!permitted || (ability?.IsTargetable?.Invoke(victim) == false)
            || ((ability == null || ability.OnlyCrewmates) && target.Data.Role.IsImpostor)) rejection = "role permission";
        else if (ReadyAt.TryGetValue(__instance.PlayerId, out float ready) && Time.unscaledTime < ready) rejection = "cooldown";
        else
        {
            Vector2 delta = target.GetTruePosition() - __instance.GetTruePosition();
            float distance = delta.magnitude;
            if (distance > GameManager.Instance.LogicOptions.GetKillDistance() + 0.5f
                || PhysicsHelpers.AnyNonTriggersBetween(__instance.GetTruePosition(), delta.normalized,
                    distance, Constants.ShipAndObjectsMask)) rejection = "range or wall";
        }
        if (rejection != null)
        {
            Logger.Debug($"Kill rejected player={__instance.PlayerId} target={target?.PlayerId} reason={rejection}", "SHR.Controls");
            __instance.RpcMurderPlayer(target ?? __instance, false);
            return false;
        }
        ReadyAt[__instance.PlayerId] = Time.unscaledTime + Mathf.Max(0.35f, ShrPlayerOptions.KillCooldown(killer));
        bool acted = ability?.TryKillTarget(victim) ?? false;
        if (ability == null) ShrMurderSync.Murder(killer, victim);
        Logger.Debug($"Kill request player={__instance.PlayerId} target={target.PlayerId} custom={ability != null} acted={acted}", "SHR.Controls");
        return false;
    }
}
