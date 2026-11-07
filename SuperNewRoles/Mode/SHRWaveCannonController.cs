using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Impostor;
using SuperNewRoles.WaveCannonObj;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SHRWaveCannonController
{
    internal const float BeamDuration = 2.88f;
    internal const float BeamHalfWidth = 1.3f;
    private readonly WaveCannonAbility ability;
    private ExPlayerControl Player => ability.Player;
    private byte phase;
    private float transitionAt;
    private float readyAt;
    private float originalKillReadyAt;
    private Vector2 origin;
    private Vector2 lockPosition;
    private bool facingLeft;
    private float nextSnapAt;
    private readonly HashSet<byte> attemptedTargets = new();
    public bool Busy => phase != 0;
    public bool ApplyingBeamDamage { get; private set; }
    public bool CanStart => !Busy && Time.unscaledTime >= readyAt
        && SHRAbilityTriggerPatches.CanActivate(Player.Player);

    private sealed class DisplayState
    {
        public ExPlayerControl Player;
        public byte Phase;
        public bool Left;
        public string OriginalName;
        public bool InfoVisible;
        public bool Moveable;
        public float KillReadyAt;
        public Vector2 Anchor;
    }

    private static readonly Dictionary<byte, DisplayState> Displays = new();

    public SHRWaveCannonController(WaveCannonAbility ability) => this.ability = ability;

    public static bool IsDisplaying(byte id) => Displays.ContainsKey(id);

    public static bool IsProtected(ExPlayerControl target)
    {
        if (target == null || !ModeManager.IsMode(ModeId.SuperHostRoles)) return false;
        var client = AmongUsClient.Instance;
        if (client == null) return false;
        return client.AmHost
            ? target.GetAbility<WaveCannonAbility>()?.SHRController?.Busy == true
            : IsDisplaying(target.PlayerId);
    }

    internal static bool BlocksAttack(bool active, bool self, CustomDeathType death)
        => active && !self && death is CustomDeathType.Kill or CustomDeathType.KillWithoutKillAnimation
            or CustomDeathType.KilLWithoutDeadbodyAndTeleport or CustomDeathType.KnifeKill
            or CustomDeathType.WaveCannon or CustomDeathType.WaveCannonSanta or CustomDeathType.SuperWaveCannon
            or CustomDeathType.Samurai or CustomDeathType.BombBySelfBomb or CustomDeathType.Push
            or CustomDeathType.Ignite or CustomDeathType.LaunchByRocket or CustomDeathType.VampireKill
            or CustomDeathType.PenguinAfterMeeting or CustomDeathType.BansheeWhisper
            or CustomDeathType.HappyGatling or CustomDeathType.SluggerSlug or CustomDeathType.ConjurerMagic
            or CustomDeathType.RocketLauncher;

    internal static bool IsBeamHit(float deltaX, float deltaY, bool left)
        => (left ? -deltaX : deltaX) > 0f && Math.Abs(deltaY) <= BeamHalfWidth;

    internal static string FormatBeamName(string color, bool charging, bool left, bool self)
    {
        if (charging)
        {
            string bigStar = $"<size=800%><color=#{color}>★</color></size>";
            string padding = "　　　";
            return "<line-height=1200%>\n" + (left ? bigStar + padding : padding + bigStar) + "</line-height>";
        }
        string star = $"<voffset=0.35em><size=800%><color=#{color}>★</color></size></voffset>";
        string beam = "<#00CFFF>━━━━━━━</color>";
        string blank = "<size=1200%>　</size>";
        string starAndBlank = left ? star + blank : blank + star;
        string line = left ? beam + beam + starAndBlank : starAndBlank + beam + beam;
        string hugeBlank = "<alpha=#00>　　　　　　　　　　</alpha>";
        string spacing = self ? "<line-height=4300%>\n" : "<line-height=5300%>\n";
        string start = "<size=5000%>", end = "</size></line-height>";
        return spacing + (left ? $"{start}{line}{end}{start}{hugeBlank}{end}"
            : $"{start}{hugeBlank}{end}{start}{line}{end}");
    }

    public void ResetInitialCooldown()
    {
        Cancel();
        float initial = SHRKillRequests.InitialCooldown(ability.DefaultTimer,
            CustomOptions.Categories.GameSettingOptions.InitialCooldown);
        readyAt = Time.unscaledTime + initial;
        Player.GetAbility<SHRAbilityTriggerAbility>()?.ResetCooldown(Time.unscaledTime, initial);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: initial, resetAbility: true);
    }

    public bool TryStart()
    {
        if (!CanStart) return false;
        origin = Player.Player.GetTruePosition();
        lockPosition = Player.Player.transform.position;
        facingLeft = Player.cosmetics.FlipX;
        phase = 1;
        transitionAt = Time.unscaledTime + Mathf.Max(0f, WaveCannon.WaveCannonDuration);
        originalKillReadyAt = Time.unscaledTime + SHRKillRequests.RemainingCooldown(Player.Player);
        attemptedTargets.Clear();
        Player.GetAbility<SHRAbilityTriggerAbility>()?.ResetCooldown(Time.unscaledTime,
            WaveCannon.WaveCannonDuration + BeamDuration + ability.DefaultTimer);
        RpcDisplay(Player, phase, facingLeft, lockPosition);
        SHRKillRequests.SetRemainingCooldown(Player.Player, 60f);
        SHRPlayerOptions.Send(Player.Player, abilityCooldown: WaveCannon.WaveCannonDuration + BeamDuration + ability.DefaultTimer,
            resetAbility: true);
        Logger.Debug($"Charge player={Player.PlayerId} origin={origin} left={facingLeft}", "SHR.WaveCannon");
        return true;
    }

    public void Tick()
    {
        if (!Busy) return;
        if (!Player.IsAlive() || Player.Data.Disconnected || MeetingHud.Instance != null
            || AmongUsClient.Instance.IsGameOver || !ability.UsesSHRCannon
            || Player.Player.onLadder || Player.Player.inMovingPlat || Player.Player.inVent)
        {
            Cancel();
            return;
        }
        if (Time.unscaledTime >= nextSnapAt && Vector2.Distance(Player.Player.GetTruePosition(), origin) > 0.02f)
        {
            nextSnapAt = Time.unscaledTime + 0.1f;
            Player.NetTransform.RpcSnapTo(lockPosition);
        }
        if (phase == 1 && Time.unscaledTime >= transitionAt)
        {
            phase = 2;
            facingLeft = Player.cosmetics.FlipX;
            transitionAt = Time.unscaledTime + BeamDuration;
            RpcDisplay(Player, phase, facingLeft, lockPosition);
            Logger.Debug($"Fire player={Player.PlayerId} left={facingLeft}", "SHR.WaveCannon");
        }
        if (phase != 2) return;
        if (Time.unscaledTime >= transitionAt)
        {
            Cancel();
            return;
        }
        foreach (var target in ExPlayerControl.ExPlayerControls.ToArray())
        {
            if (!Busy || !Player.IsAlive()) break;
            if (target == null || target == Player || target.Data == null || target.Data.Disconnected
                || !target.IsAlive() || attemptedTargets.Contains(target.PlayerId)) continue;
            if (!ability.friendlyFire && target.IsImpostor()) continue;
            Vector2 delta = target.Player.GetTruePosition() - origin;
            if (!IsBeamHit(delta.x, delta.y, facingLeft)) continue;
            attemptedTargets.Add(target.PlayerId);
            ApplyingBeamDamage = true;
            try { SHRMurderSync.Murder(Player, target, explosion: true, finalStatus: FinalStatus.WaveCannon); }
            finally { ApplyingBeamDamage = false; }
        }
    }

    public void Cancel()
    {
        if (!Busy) return;
        phase = 0;
        attemptedTargets.Clear();
        readyAt = Time.unscaledTime + ability.DefaultTimer;
        RpcDisplay(Player, 0, facingLeft, lockPosition);
        if (Player.Data.Disconnected) return;
        if (Player.IsAlive())
        {
            float kill = ability.isResetKillCooldown ? SHRPlayerOptions.KillCooldown(Player)
                : Mathf.Max(0f, originalKillReadyAt - Time.unscaledTime);
            SHRKillRequests.SetRemainingCooldown(Player.Player, kill);
            Player.GetAbility<SHRAbilityTriggerAbility>()?.ResetCooldown(Time.unscaledTime, ability.DefaultTimer);
            SHRPlayerOptions.Send(Player.Player, resetAbility: true);
        }
        else SHRPlayerOptions.Send(Player.Player);
    }

    [CustomRPC]
    public static void RpcRequestFire(ExPlayerControl source)
    {
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
            && ModeManager.IsMode(ModeId.SuperHostRoles) && source?.Role == RoleId.WaveCannon)
            source.GetAbility<WaveCannonAbility>()?.SHRController?.TryStart();
    }

    [CustomRPC]
    public static void RpcDisplay(ExPlayerControl source, byte phase, bool left, Vector2 position)
    {
        if (source?.Player == null || !ModeManager.IsMode(ModeId.SuperHostRoles)) return;
        if (phase == 0)
        {
            RestoreDisplay(source.PlayerId);
        }
        else
        {
            bool createVisual = !Displays.TryGetValue(source.PlayerId, out var oldState)
                || oldState.Left != left || source.GetAbility<WaveCannonAbility>()?.WaveCannonObject == null;
            if (!Displays.TryGetValue(source.PlayerId, out var state))
            {
                state = new DisplayState
                {
                    Player = source,
                    OriginalName = source.cosmetics.nameText.text,
                    InfoVisible = source.PlayerInfoText != null && source.PlayerInfoText.gameObject.activeSelf,
                    Moveable = source.Player.moveable,
                    KillReadyAt = Time.unscaledTime + source.Player.killTimer,
                };
                Displays[source.PlayerId] = state;
            }
            state.Phase = phase;
            state.Left = left;
            state.Anchor = position;
            var wave = source.GetAbility<WaveCannonAbility>();
            if (wave != null)
            {
                if (createVisual)
                {
                    wave.WaveCannonObject?.Detach();
                    var anchor = new Vector3(position.x, position.y, source.transform.position.z);
                    var visual = WaveCannonObjectBase.SpawnFromType(wave.Type, wave, left, anchor);
                    wave.SpawnedWaveCannonObject(visual);
                }
                if (phase == 2) wave.WaveCannonObject?.OnShoot();
            }
            if (source.AmOwner) source.Player.SetKillTimer(60f);
        }
        if (!AmongUsClient.Instance.AmHost) return;
        string color = ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[source.Data.DefaultOutfit.ColorId]);
        foreach (var viewer in PlayerControl.AllPlayerControls)
        {
            if (!SHRPlayerOptions.IsVanillaGuest(viewer)) continue;
            string name = phase == 0
                ? viewer.PlayerId == source.PlayerId ? SHRRoleNameDisplay.GetSelfDisplayName(source.Player) : source.Data.PlayerName
                : FormatBeamName(color, phase == 1, left, viewer.PlayerId == source.PlayerId);
            source.Player.RpcSetNamePrivate(name, viewer);
        }
    }

    private static void RestoreDisplay(byte id)
    {
        if (!Displays.Remove(id, out var state)) return;
        var source = state.Player;
        if (source?.Player == null) return;
        var wave = source.GetAbility<WaveCannonAbility>();
        wave?.WaveCannonObject?.Detach();
        if (source.cosmetics?.nameText != null) source.cosmetics.nameText.text = state.OriginalName;
        if (source.PlayerInfoText != null) source.PlayerInfoText.gameObject.SetActive(state.InfoVisible);
        if (source.AmOwner)
        {
            if (source.IsAlive()) source.Player.moveable = state.Moveable;
            if (wave != null)
            {
                wave.isEffectActive = false;
                wave.ResetTimer();
                if (source.IsAlive())
                    source.Player.SetKillTimer(wave.isResetKillCooldown ? SHRPlayerOptions.KillCooldown(source)
                        : Mathf.Max(0f, state.KillReadyAt - Time.unscaledTime));
            }
        }
        NameText.UpdateAllNameInfo();
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class DisplayPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            foreach (var pair in Displays.ToArray())
            {
                var state = pair.Value;
                var source = state.Player;
                if (source?.Player == null || source.Data == null || source.Data.Disconnected || !source.IsAlive()
                    || MeetingHud.Instance != null || AmongUsClient.Instance.IsGameOver
                    || !ModeManager.IsMode(ModeId.SuperHostRoles))
                {
                    RestoreDisplay(pair.Key);
                    continue;
                }
                if (source.PlayerInfoText != null) source.PlayerInfoText.gameObject.SetActive(false);
                if (source.AmOwner)
                {
                    source.Player.moveable = false;
                    source.MyPhysics.body.velocity = Vector2.zero;
                    source.NetTransform.SnapTo(state.Anchor);
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckUseZipline))]
    public static class ZiplinePatch
    {
        public static bool Prefix(PlayerControl __instance)
            => !(AmongUsClient.Instance.AmHost && ModeManager.IsMode(ModeId.SuperHostRoles)
                && ((ExPlayerControl)__instance)?.GetAbility<WaveCannonAbility>()?.SHRController?.Busy == true);
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class EndPatch
    {
        public static void Prefix() => CancelAll();
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    public static class MeetingPatch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => CancelAll();
    }

    private static void CancelAll()
    {
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
            && ModeManager.IsMode(ModeId.SuperHostRoles))
            foreach (var player in ExPlayerControl.ExPlayerControls.ToArray())
                player.GetAbility<WaveCannonAbility>()?.SHRController?.Cancel();
        foreach (byte id in Displays.Keys.ToArray()) RestoreDisplay(id);
    }
}
