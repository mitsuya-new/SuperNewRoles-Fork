using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.CustomOptions.Categories;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRCamouflagerController
{
    internal const string AnonymousName = "プレイヤー";
    private sealed record Appearance(PlayerControl Player, int Target, int Color);
    private static readonly HashSet<SHRCamouflagerController> Active = new();
    private static readonly Dictionary<byte, Appearance> Players = new();
    private static PlayerControl template;
    private static NetworkedPlayerInfo.PlayerOutfit original;
    private static bool randomColors;
    private static int[] originalColors = Array.Empty<int>();
    internal static bool Applying { get; private set; }
    internal static bool IsActive => Active.Count > 0;
    internal static bool IsMasked(PlayerControl player) => IsActive && Players.ContainsKey(player.PlayerId);
    private readonly CamouflagerAbility ability;
    private bool initialized;
    private float readyAt;
    private float endsAt;

    internal float RemainingCooldown => Mathf.Max(0f, (endsAt > 0f ? endsAt : readyAt) - Time.unscaledTime);
    private bool IsHost => AmongUsClient.Instance?.AmHost == true && ModeManager.IsMode(ModeId.SuperHostRoles);

    internal SHRCamouflagerController(CamouflagerAbility ability)
    {
        this.ability = ability;
    }

    internal void Tick()
    {
        if (!IsHost) return;
        if (AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
            || ability.Player?.Data == null || !ability.Player.IsAlive() || ability.Player.Data.Disconnected)
        {
            Stop();
            return;
        }
        if (MeetingHud.Instance != null || ExileController.Instance != null || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed) return;
        if (!initialized)
        {
            initialized = true;
            ResetCooldown();
        }
        if (endsAt > 0f && Time.unscaledTime >= endsAt) Stop();
        if (IsActive && (template?.Data == null || template.Data.Disconnected)) StopAll();
    }

    internal bool CanStart() => IsHost && initialized && endsAt == 0f && Time.unscaledTime >= readyAt
        && SHRAbilityTriggerPatches.CanActivate(ability.Player.Player)
        && PlayerControl.LocalPlayer?.Data != null && !PlayerControl.LocalPlayer.shapeshifting
        && !PlayerControl.LocalPlayer.IsMushroomMixupActive();

    internal void Start()
    {
        if (!CanStart()) return;
        bool first = !IsActive;
        Active.Add(this);
        endsAt = Time.unscaledTime + ability.DurationTime;
        readyAt = endsAt + ability.CoolTime;
        try
        {
            ability.RpcStartCamouflage();
            if (first) Apply();
            SHRRoleNameDisplay.Refresh();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    private void Apply()
    {
        template = PlayerControl.LocalPlayer;
        var outfit = template.Data.DefaultOutfit;
        original = new NetworkedPlayerInfo.PlayerOutfit
        {
            PlayerName = outfit.PlayerName, ColorId = outfit.ColorId, HatId = outfit.HatId,
            SkinId = outfit.SkinId, VisorId = outfit.VisorId, PetId = outfit.PetId
        };
        Players.Clear();
        randomColors = ability.ChangeColorType == 2;
        originalColors = PlayerControl.AllPlayerControls.ToArray().Where(p => p?.Data != null && !p.Data.Disconnected)
            .Select(p => p.Data.DefaultOutfit.ColorId).ToArray();
        foreach (var player in PlayerControl.AllPlayerControls)
            if (player?.Data != null && !player.Data.Disconnected && (!player.Data.IsDead || player == template))
                Players[player.PlayerId] = new Appearance(player,
                    player.CurrentOutfitType == PlayerOutfitType.Shapeshifted ? player.ShapeshiftedPlayerID() : player.PlayerId,
                    player.Data.DefaultOutfit.ColorId);
        Applying = true;
        try
        {
            SHRMadKillerSupport.Restore();
            if (template.CurrentOutfitType == PlayerOutfitType.Shapeshifted) template.RpcShapeshift(template, false);
            SetNameImmediately(template, AnonymousName);
            SetCosmeticImmediately(template, RpcCalls.SetHatStr, "");
            SetCosmeticImmediately(template, RpcCalls.SetSkinStr, "");
            SetCosmeticImmediately(template, RpcCalls.SetVisorStr, "");
            SetCosmeticImmediately(template, RpcCalls.SetPetStr, "");
            int color = SelectColor(ability.ChangeColorType, ability.CamouflageColor);
            SetColorImmediately(template, color);
            foreach (var state in Players.Values)
            {
                if (state.Player == template || state.Player.Data.IsDead) continue;
                state.Player.RpcShapeshift(template, false);
                if (randomColors)
                {
                    int random = RandomColor(state.Color);
                    SetColorImmediately(state.Player, random);
                    state.Player.Data.SetOutfit(PlayerOutfitType.Shapeshifted, new NetworkedPlayerInfo.PlayerOutfit
                    {
                        PlayerName = AnonymousName, ColorId = random, HatId = "", SkinId = "", VisorId = "", PetId = ""
                    });
                    state.Player.Data.MarkDirty();
                }
            }
            if (randomColors) SetColorImmediately(template, RandomColor(original.ColorId));
        }
        finally { Applying = false; }
    }

    internal static int SelectColor(int mode, int selected) => mode == 1 ? selected : 15;

    private static void SetNameImmediately(PlayerControl player, string name)
    {
        player.SetName(name);
        var client = AmongUsClient.Instance;
        var writer = client.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetName, SendOption.Reliable);
        new RpcSetNameMessage(player.NetId, player.Data.NetId, name).SerializeRpcValues(writer);
        client.FinishRpcImmediately(writer);
    }

    private static void SetColorImmediately(PlayerControl player, int color)
    {
        player.SetColor(color);
        var client = AmongUsClient.Instance;
        var writer = client.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetColor, SendOption.Reliable);
        new RpcSetColorMessage(player.NetId, player.Data.NetId, (byte)color).SerializeRpcValues(writer);
        client.FinishRpcImmediately(writer);
    }

    private static void SetCosmeticImmediately(PlayerControl player, RpcCalls rpc, string id)
    {
        int color = player.Data.DefaultOutfit.ColorId;
        byte sequence = player.GetNextRpcSequenceId(rpc);
        var client = AmongUsClient.Instance;
        var writer = client.StartRpcImmediately(player.NetId, (byte)rpc, SendOption.Reliable);
        switch (rpc)
        {
            case RpcCalls.SetHatStr:
                player.Data.UpdateHat(id);
                player.SetHat(id, color);
                new RpcSetHatStrMessage(player.NetId, id, sequence).SerializeRpcValues(writer);
                break;
            case RpcCalls.SetSkinStr:
                player.Data.UpdateSkin(id);
                player.SetSkin(id, color);
                new RpcSetSkinStrMessage(player.NetId, id, sequence).SerializeRpcValues(writer);
                break;
            case RpcCalls.SetVisorStr:
                player.Data.UpdateVisor(id);
                player.SetVisor(id, color);
                new RpcSetVisorStrMessage(player.NetId, id, sequence).SerializeRpcValues(writer);
                break;
            case RpcCalls.SetPetStr:
                player.Data.UpdatePet(id);
                player.SetPet(id, color);
                new RpcSetPetStrMessage(player.NetId, id, sequence).SerializeRpcValues(writer);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rpc));
        }
        client.FinishRpcImmediately(writer);
    }

    private static int RandomColor(int ownColor)
    {
        var colors = originalColors.Where(c => c != ownColor).ToArray();
        return colors.Length == 0 ? 15 : colors[UnityEngine.Random.Range(0, colors.Length)];
    }

    internal void Stop()
    {
        if (!IsHost || !Active.Remove(this)) return;
        endsAt = 0f;
        readyAt = Time.unscaledTime + ability.CoolTime;
        try
        {
            if (!IsActive) Restore();
        }
        finally
        {
            ability.RpcEndCamouflage();
            SHRPlayerOptions.Send(ability.Player.Player, abilityCooldown: RemainingCooldown, resetAbility: true);
            SHRRoleNameDisplay.Refresh();
        }
    }

    internal void ResetCooldown()
    {
        readyAt = Time.unscaledTime + SHRKillRequests.InitialCooldown(ability.CoolTime, GameSettingOptions.InitialCooldown);
        SHRPlayerOptions.Send(ability.Player.Player, abilityCooldown: RemainingCooldown, resetAbility: true);
    }

    private static void Restore()
    {
        Applying = true;
        try
        {
            if (template?.Data != null && !template.Data.Disconnected && original != null)
            {
                SetNameImmediately(template, original.PlayerName);
                SetColorImmediately(template, original.ColorId);
                SetCosmeticImmediately(template, RpcCalls.SetHatStr, original.HatId);
                SetCosmeticImmediately(template, RpcCalls.SetSkinStr, original.SkinId);
                SetCosmeticImmediately(template, RpcCalls.SetVisorStr, original.VisorId);
                SetCosmeticImmediately(template, RpcCalls.SetPetStr, original.PetId);
            }
            if (randomColors)
                foreach (var state in Players.Values)
                    if (state.Player != template && state.Player?.Data != null && !state.Player.Data.Disconnected)
                        SetColorImmediately(state.Player, state.Color);
            foreach (var state in Players.Values)
            {
                var player = state.Player;
                if (player?.Data == null || player.Data.Disconnected) continue;
                if (player == template && state.Target == player.PlayerId && player.CurrentOutfitType == PlayerOutfitType.Default) continue;
                var target = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p => p.PlayerId == state.Target && p.Data != null && !p.Data.Disconnected);
                player.RpcShapeshift(target ?? player, false);
            }
        }
        finally
        {
            Applying = false;
            Players.Clear();
            template = null;
            original = null;
            randomColors = false;
            originalColors = Array.Empty<int>();
        }
    }

    internal static bool BlocksShape(PlayerControl player, PlayerControl target)
    {
        if (Applying || !IsActive || !Players.TryGetValue(player.PlayerId, out var state)) return false;
        if (target == player)
        {
            Players[player.PlayerId] = state with { Target = player.PlayerId };
            ShapeshiftEvent.Invoke(player, player, false);
        }
        return true;
    }

    internal static string OriginalName(PlayerControl player) => IsActive && player == template && original != null
        ? original.PlayerName : player.Data.PlayerName;

    internal static string OriginalPet(PlayerControl player) => IsActive && player == template && original != null
        ? original.PetId : player.Data.DefaultOutfit.PetId;

    private static void StopAll()
    {
        foreach (var controller in Active.ToArray()) controller.Stop();
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
    public static class LayerPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(PlayerControl __instance)
        {
            if (!CamouflagerAbility.SHRActive || __instance?.Data == null || __instance.Data.IsDead) return;
            var layer = CustomCosmetics.CosmeticsPlayer.CustomCosmeticsLayers.ExistsOrInitialize(__instance.cosmetics);
            layer.hat2?.StartCamouflage(__instance.CurrentOutfit.ColorId);
            layer.visor2?.StartCamouflage(__instance.CurrentOutfit.ColorId);
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleDisconnect))]
    public static class DisconnectPatch
    {
        public static void Postfix()
        {
            Active.Clear();
            Players.Clear();
            template = null;
            original = null;
            randomColors = false;
            originalColors = Array.Empty<int>();
            Applying = false;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    public static class MeetingPatch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => StopAll();
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class EndPatch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => StopAll();
    }
}
