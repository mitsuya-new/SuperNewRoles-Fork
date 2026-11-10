using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Crewmate;
using UnityEngine;

namespace SuperNewRoles.Mode;

public static class SHRClergyVision
{
    private static readonly Dictionary<byte, float> Sent = new();
    private static float nextUpdate;
    internal static float VisionMultiplier(float progress, float multiplier)
    {
        float t = Math.Clamp(progress, 0f, 1f);
        return 1f + (multiplier - 1f) * t * t * (3f - 2f * t);
    }

    internal static float ForPlayer(ExPlayerControl player)
        => ExPlayerControl.ExPlayerControls.SelectMany(owner => owner.GetAbilities<ClergyAbility>())
            .Aggregate(1f, (value, ability) => value * ability.SHRMultiplier(player));

    private static void Update()
    {
        var client = AmongUsClient.Instance;
        if (client?.AmHost != true || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || client.GameState != InnerNetClient.GameStates.Started || client.IsGameOver)
        {
            Sent.Clear();
            return;
        }
        if (SHRAntiBlackout.Active || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 0.1f;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(player) || player.Data.IsDead) continue;
            float current = ForPlayer(player);
            if (!Sent.TryGetValue(player.PlayerId, out float previous)) previous = 1f;
            if (Math.Abs(current - previous) < 0.01f && !(current == 1f && previous != 1f)) continue;
            Sent[player.PlayerId] = current;
            SHRPlayerOptions.Send(player);
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class UpdatePatch { public static void Postfix() => Update(); }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    public static class StartPatch { public static void Prefix() { Sent.Clear(); nextUpdate = 0f; } }
}
