using System;
using System.Linq;
using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRAbilityTriggerPatches
{
    private static bool IsHostMode => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
        && ModeManager.IsMode(ModeId.SuperHostRoles);

    private static SHRAbilityTriggerAbility Find(PlayerControl player, SHRAbilityTrigger source)
    {
        ExPlayerControl exPlayer = player;
        return exPlayer?.GetAbilities<SHRAbilityTriggerAbility>().FirstOrDefault(a => a.Triggers.HasFlag(source));
    }

    internal static bool CanActivate(PlayerControl player)
        => IsHostMode && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started
            && !AmongUsClient.Instance.IsGameOver && MeetingHud.Instance == null && ExileController.Instance == null
            && HudManager.Instance != null && !HudManager.Instance.IsIntroDisplayed
            && player != null && player.Data != null && !player.Data.IsDead && !player.Data.Disconnected
            && !player.inVent && !player.walkingToVent && !player.onLadder && !player.inMovingPlat
            && !(ShipStatus.Instance?.TryCast<FungleShipStatus>()?.Zipline?.playerIdHands.ContainsKey(player.PlayerId) == true);

    private static bool Activate(PlayerControl player, SHRAbilityTriggerAbility ability, SHRAbilityTrigger source,
        PlayerControl target = null)
    {
        if (!CanActivate(player)) return false;
        try
        {
            bool accepted = ability.TryActivate(source, Time.unscaledTime, target);
            Logger.Debug($"Trigger player={player.PlayerId} source={source} accepted={accepted} remaining={ability.RemainingCooldown(Time.unscaledTime)}", "SHR.Controls");
            return accepted;
        }
        catch (Exception ex) { Logger.Error($"SHR {source} trigger failed for {player.PlayerId}: {ex}"); return false; }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
    public static class LocalPetPatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (!IsHostMode || !__instance.AmOwner || !__instance.petting) return;
            HandlePet(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
    public static class RemotePetPatch
    {
        public static void Postfix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
        {
            if (IsHostMode && callId == (byte)RpcCalls.Pet && __instance.myPlayer != null)
                HandlePet(__instance.myPlayer);
        }
    }

    private static void HandlePet(PlayerControl player)
    {
        var ability = Find(player, SHRAbilityTrigger.Pet);
        Logger.Debug($"Pet received player={player.PlayerId} handler={ability != null} allowed={CanActivate(player)}", "SHR.Controls");
        if (ability == null || !CanActivate(player)) return;
        if (!ability.TryConsumeInput(Time.unscaledTime)) return;
        Activate(player, ability, SHRAbilityTrigger.Pet);
        player.MyPhysics.RpcCancelPet();
        player.MyPhysics.CancelPet();
    }

    internal static SHRAbilityTrigger GetShapeshiftTrigger(bool isSelf)
        => isSelf ? SHRAbilityTrigger.ShapeshiftSelf : SHRAbilityTrigger.Shapeshift;

    public static bool HandleShapeshift(PlayerControl player, PlayerControl target)
    {
        if (!IsHostMode) return false;
        if (((ExPlayerControl)player).Role == Roles.RoleId.SerialKiller)
        {
            player.RpcRejectShapeshift();
            return true;
        }
        var source = GetShapeshiftTrigger(player.PlayerId == target.PlayerId);
        var ability = Find(player, source);
        if (ability == null) return false;
        player.RpcRejectShapeshift();
        if (!CanActivate(player) || target.Data == null || target.Data.Disconnected
            || (target.Data.IsDead && source == SHRAbilityTrigger.Shapeshift)
            || !ability.TryConsumeInput(Time.unscaledTime)) return true;
        Activate(player, ability, source, target);
        if (CanActivate(player))
            SHRPlayerOptions.Send(player, abilityCooldown: ability.RemainingCooldown(Time.unscaledTime), resetAbility: true);
        return true;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckVanish))]
    public static class PhantomPatch
    {
        public static bool Prefix(PlayerControl __instance) => HandlePhantom(__instance);
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
    public static class RemotePhantomPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId)
            => (callId != (byte)RpcCalls.CheckVanish && callId != (byte)RpcCalls.StartVanish)
                || HandlePhantom(__instance);
    }

    private static bool HandlePhantom(PlayerControl player)
    {
        if (!IsHostMode) return true;
        var ability = Find(player, SHRAbilityTrigger.Phantom);
        Logger.Debug($"Phantom received player={player.PlayerId} handler={ability != null} allowed={CanActivate(player)}", "SHR.Controls");
        if (ability == null) return true;
        if (!CanActivate(player) || !ability.TryConsumeInput(Time.unscaledTime)) return false;
        bool accepted = Activate(player, ability, SHRAbilityTrigger.Phantom);
        if (CanActivate(player))
        {
            if (accepted) ResetPhantomButton(player, ability.RemainingCooldown(Time.unscaledTime));
            else SHRPlayerOptions.Send(player, abilityCooldown: ability.RemainingCooldown(Time.unscaledTime), resetAbility: true);
        }
        return false;
    }

    private static void ResetPhantomButton(PlayerControl player, float remaining)
    {
        if (player.AmOwner)
        {
            HudManager.Instance.AbilityButton.SetCoolDown(remaining, Mathf.Max(0.001f, remaining));
            return;
        }
        int clientId = AmongUsClient.Instance.GetClientIdFromCharacter(player);
        if (clientId < 0) return;
        SendRole(RoleTypes.Impostor);
        ExPlayerControl exPlayer = player;
        bool completed = exPlayer.GetAbility<SHRTestRoleAbility>()?.PhantomAbilityCompleted == true;
        if (!completed) SendRole(RoleTypes.Phantom);
        SHRPlayerOptions.Send(player, abilityCooldown: remaining,
            killCooldown: SHRKillRequests.RemainingCooldown(player), resetAbility: !completed, resetKill: true);
        void SendRole(RoleTypes role)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetRole,
                SendOption.Reliable, clientId);
            new RpcSetRoleMessage(player.NetId, role, true).SerializeRpcValues(writer);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
}
