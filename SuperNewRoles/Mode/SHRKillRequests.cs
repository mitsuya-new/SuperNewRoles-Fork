using System.Collections.Generic;
using HarmonyLib;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
public static class SHRKillRequests
{
    private static readonly Dictionary<byte, float> ReadyAt = new();
    public static void Reset() => ReadyAt.Clear();

    internal static void SetRemainingCooldown(PlayerControl player, float remaining, PlayerControl protectedTarget = null)
    {
        ReadyAt[player.PlayerId] = Time.unscaledTime + Mathf.Max(0f, remaining);
        SHRPlayerOptions.Send(player, killCooldown: remaining, resetKill: true, protectedTarget: protectedTarget);
    }

    public static float RemainingCooldown(PlayerControl player)
        => player.AmOwner ? Mathf.Max(0f, player.killTimer) : ReadyAt.TryGetValue(player.PlayerId, out float ready)
            ? Mathf.Max(0f, ready - Time.unscaledTime)
            : InitialCooldown(SHRPlayerOptions.KillCooldown(player), GameSettingOptions.InitialCooldown);

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
            if (!SHRPlayerOptions.IsVanillaGuest(player)) continue;
            float initial = InitialCooldown(SHRPlayerOptions.KillCooldown(player), GameSettingOptions.InitialCooldown);
            ReadyAt[player.PlayerId] = Time.unscaledTime + initial;
            SHRPlayerOptions.Send(player, killCooldown: initial, resetKill: true);
        }
    }

    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || !SHRPlayerOptions.IsVanillaGuest(__instance)) return true;
        ExPlayerControl killer = __instance;
        ExPlayerControl victim = target;
        if (killer.GetAbility<SHRRocketAbility>()?.TryLaunchFromKill() == true) return false;
        var ability = killer.GetAbility<CustomKillButtonAbility>();
        var chief = killer.GetAbility<SuperNewRoles.Roles.Crewmate.ChiefAbility>();
        bool permitted = ability != null ? ability.CanKill() : __instance.Data.Role?.IsImpostor == true;
        if (chief != null) permitted = chief.CanAppointSHR;
        if (killer.Role is SuperNewRoles.Roles.RoleId.ToiletFan or SuperNewRoles.Roles.RoleId.NiceButtoner or SuperNewRoles.Roles.RoleId.Doorr or SuperNewRoles.Roles.RoleId.Conjurer or SuperNewRoles.Roles.RoleId.Rocket) permitted = false;
        if (killer.Role == SuperNewRoles.Roles.RoleId.DoubleKiller) permitted = true;
        if (killer.GetAbility<WaveCannonAbility>()?.SHRController?.Busy == true) permitted = false;
        if (killer.GetAbility<SHREvilHackerAbility>()?.FreezesMovement == true) permitted = false;
        if (killer.GetAbility<PenguinAbility>() is { } penguin && !penguin.CanUseNormalKill) permitted = false;
        if (killer.Role == SuperNewRoles.Roles.RoleId.Tasker
            && (!SuperNewRoles.Roles.Impostor.Tasker.TaskerCanKill
                || killer.GetAbility<SHRTestRoleAbility>()?.TaskerImpostor != true)) permitted = false;
        string rejection = null;
        if (!SHRAbilityTriggerPatches.CanActivate(__instance)) rejection = "actor unavailable";
        else if (target == null || target == __instance || target.Data == null || target.Data.IsDead
            || target.Data.Disconnected || target.inVent || target.onLadder || target.inMovingPlat) rejection = "target unavailable";
        else if (!permitted || SuperNewRoles.Patches.SetTargetPatch.IsMadKillerTeammate(killer, victim)
            || (ability?.IsTargetable?.Invoke(victim) == false)
            || (chief == null && (ability == null || ability.OnlyCrewmates) && target.Data.Role.IsImpostor)) rejection = "role permission";
        else if (SHRWaveCannonController.IsProtected(victim)) rejection = "wave cannon protection";
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
            if (rejection == "actor unavailable")
                Logger.Debug($"Kill actor state player={__instance.PlayerId} game={client.GameState} gameOver={client.IsGameOver}"
                    + $" transition={SHRAntiBlackout.Active} meeting={MeetingHud.Instance != null} exile={ExileController.Instance != null}"
                    + $" hud={HudManager.Instance != null} intro={HudManager.Instance?.IsIntroDisplayed}"
                    + $" dead={__instance.Data?.IsDead} disconnected={__instance.Data?.Disconnected}"
                    + $" vent={__instance.inVent} walkingToVent={__instance.walkingToVent} ladder={__instance.onLadder}"
                    + $" platform={__instance.inMovingPlat} dragged={PenguinAbility.IsDragged(killer)}"
                    + $" zipline={ShipStatus.Instance?.TryCast<FungleShipStatus>()?.Zipline?.playerIdHands.ContainsKey(__instance.PlayerId) == true}",
                    "SHR.Controls");
            __instance.RpcMurderPlayer(target ?? __instance, false);
            return false;
        }
        ReadyAt[__instance.PlayerId] = Time.unscaledTime + Mathf.Max(0.35f, SHRPlayerOptions.KillCooldown(killer));
        if (chief != null)
        {
            chief.TryAppointSHR(victim);
            return false;
        }
        bool doubleMain = killer.Role == SuperNewRoles.Roles.RoleId.DoubleKiller;
        bool acted = !doubleMain && (ability?.TryKillTarget(victim) ?? false);
        if (ability == null || doubleMain) SHRMurderSync.Murder(killer, victim);
        Logger.Debug($"Kill request player={__instance.PlayerId} target={target.PlayerId} custom={ability != null} acted={acted}", "SHR.Controls");
        return false;
    }
}
