using System.Collections.Generic;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRPets
{
    private const string DefaultPet = "pet_test";
    private static readonly Dictionary<byte, (PlayerControl Player, string Original)> Granted = new();
    private static readonly Dictionary<(byte, byte), (PlayerControl Subject, PlayerControl Viewer, string Pet)> Sent = new();
    private static bool active;
    private static float nextUpdate;

    internal static string PetForViewer(bool isSelf, string petId) => isSelf ? petId ?? "" : "";

    internal static bool NeedsPet(string petId)
        => string.IsNullOrEmpty(petId) || petId.ToLowerInvariant() is "pet_emptypet" or "pet_enmptypet" or "pet_empty" or "pet_none" or "none";

    internal static void GiveMissingPets()
    {
        if (AmongUsClient.Instance?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        Granted.Clear();
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player?.Data == null || player.Data.Disconnected) continue;
            string original = player.Data.DefaultOutfit.PetId;
            if (!NeedsPet(original)) continue;
            Granted[player.PlayerId] = (player, original);
            SetPetImmediately(player, DefaultPet);
        }
        active = true;
        Sent.Clear();
        nextUpdate = 0f;
        Update();
    }

    private static void SetPetImmediately(PlayerControl player, string pet)
    {
        byte sequence = player.GetNextRpcSequenceId(RpcCalls.SetPetStr);
        player.Data.UpdatePet(pet);
        player.SetPet(pet, player.Data.DefaultOutfit.ColorId);
        var client = AmongUsClient.Instance;
        var writer = client.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetPetStr, SendOption.Reliable);
        new RpcSetPetStrMessage(player.NetId, pet, sequence).SerializeRpcValues(writer);
        client.FinishRpcImmediately(writer);
    }

    private static string RealPet(PlayerControl player) => SHRCamouflagerController.OriginalPet(player) ?? "";

    internal static void Invalidate(PlayerControl player)
    {
        if (!active || AmongUsClient.Instance?.AmHost != true || player == null) return;
        foreach (var viewer in PlayerControl.AllPlayerControls) Sent.Remove((player.PlayerId, viewer.PlayerId));
        nextUpdate = Mathf.Min(nextUpdate, Time.unscaledTime + 0.15f);
    }

    private static void WritePet(MessageWriter writer, PlayerControl subject, PlayerControl viewer, byte sequence)
    {
        string pet = PetForViewer(subject == viewer, RealPet(subject));
        new RpcSetPetStrMessage(subject.NetId, pet, sequence).SerializeRpcValues(writer);
        Sent[(subject.PlayerId, viewer.PlayerId)] = (subject, viewer, pet);
    }

    internal static void AppendPetState(MessageWriter writer, PlayerControl subject, PlayerControl viewer)
    {
        if (!active) return;
        writer.StartMessage((byte)GameDataTypes.RpcFlag);
        writer.WritePacked(subject.NetId);
        writer.Write((byte)RpcCalls.SetPetStr);
        WritePet(writer, subject, viewer, subject.GetNextRpcSequenceId(RpcCalls.SetPetStr));
        writer.EndMessage();
    }

    private static void Update()
    {
        var client = AmongUsClient.Instance;
        if (client == null || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver) return;
        foreach (var player in PlayerControl.AllPlayerControls)
            if (player != null && !player.AmOwner) player.cosmetics?.SetPetVisible(false);
        if (!client.AmHost || !active || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 0.5f;
        foreach (var subject in PlayerControl.AllPlayerControls)
        {
            if (subject?.Data == null || subject.Data.Disconnected) continue;
            byte? sequence = null;
            foreach (var viewer in PlayerControl.AllPlayerControls)
            {
                if (viewer?.Data == null || viewer.Data.Disconnected || viewer.AmOwner) continue;
                string pet = PetForViewer(subject == viewer, RealPet(subject));
                if (Sent.TryGetValue((subject.PlayerId, viewer.PlayerId), out var previous)
                    && previous.Subject == subject && previous.Viewer == viewer && previous.Pet == pet) continue;
                int id = client.GetClientIdFromCharacter(viewer);
                if (id < 0) continue;
                sequence ??= subject.GetNextRpcSequenceId(RpcCalls.SetPetStr);
                var writer = client.StartRpcImmediately(subject.NetId, (byte)RpcCalls.SetPetStr, SendOption.Reliable, id);
                WritePet(writer, subject, viewer, sequence.Value);
                client.FinishRpcImmediately(writer);
            }
        }
    }

    internal static void Restore()
    {
        bool restoreVisibility = active;
        active = false;
        Sent.Clear();
        if (AmongUsClient.Instance?.AmHost == true)
            foreach (var entry in Granted.Values)
                if (entry.Player?.Data != null && !entry.Player.Data.Disconnected
                    && entry.Player.Data.DefaultOutfit.PetId == DefaultPet)
                    entry.Player.RpcSetPet(entry.Original ?? "");
        Granted.Clear();
        if (restoreVisibility && AmongUsClient.Instance?.AmHost == true)
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player?.Data != null && !player.Data.Disconnected)
                {
                    SetPetImmediately(player, player.Data.DefaultOutfit.PetId ?? "");
                    player.cosmetics?.SetPetVisible(true);
                }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class UpdatePatch { public static void Postfix() => Update(); }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetPet), new[] { typeof(string), typeof(int) })]
    public static class PetPatch { public static void Postfix(PlayerControl __instance) => Invalidate(__instance); }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
    public static class ShapePatch { public static void Postfix(PlayerControl __instance) => Invalidate(__instance); }

    [HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Serialize))]
    public static class SerializePatch
    {
        public static void Postfix(NetworkedPlayerInfo __instance, bool __result)
        {
            if (__result && !SHRMeetingChat.IsSerializingPrivateState) Invalidate(__instance.Object);
        }
    }
}
