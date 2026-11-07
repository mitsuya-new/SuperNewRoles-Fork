using System;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using UnityEngine;
using AmongUs.GameOptions;
using SuperNewRoles.Mode;

namespace SuperNewRoles.Roles.Ability;

public record EvilGamblerData(
    float successKillCooldown,
    float failureKillCooldown,
    int successChance
);

public class EvilGamblerAbility : AbilityBase
{
    private readonly EvilGamblerData _data;
    internal float? SHRKillCooldown { get; private set; }
    private bool IsVanillaHost => AmongUsClient.Instance?.AmHost == true
        && ModeManager.IsMode(ModeId.SuperHostRoles) && SHRPlayerOptions.IsVanillaGuest(Player.Player);

    internal static float SelectCooldown(EvilGamblerData settings, int roll)
        => roll < settings.successChance ? settings.successKillCooldown : settings.failureKillCooldown;

    public EvilGamblerAbility(EvilGamblerData data)
    {
        _data = data;
    }

    public override void AttachToLocalPlayer()
    {
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
    }

    public override void AttachToOthers()
    {
        if (!IsVanillaHost) return;
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
    }

    private void OnMurder(MurderEventData data)
    {
        if (Player == null) return;
        if (data.killer == null) return;
        if (!data.resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;
        if (Player.PlayerId == data.killer.PlayerId)
        {
            if (IsVanillaHost)
            {
                if (data.target == null || data.target == Player || !data.target.IsDead() || !Player.IsAlive()) return;
                float cooldown = SelectCooldown(_data, UnityEngine.Random.Range(0, 100));
                SHRKillCooldown = cooldown;
                SHRKillRequests.SetRemainingCooldown(Player.Player, cooldown);
                Logger.Debug($"Gambler kill cooldown player={Player.PlayerId} cooldown={cooldown}", "SHR.Controls");
                return;
            }
            // キルが発生した時に確率判定を行い、キルクールを設定
            bool isSuccess = UnityEngine.Random.Range(0, 100) < _data.successChance;
            float killTime = isSuccess ? _data.successKillCooldown : _data.failureKillCooldown;

            new LateTask(() =>
            {
                ((ExPlayerControl)Player).SetKillTimerUnchecked(killTime, killTime);
            }, 0.05f, "EvilGamblerSetKillTimerOnMurder");
        }
    }

    private void OnWrapUp(WrapUpEventData data)
    {
        if (Player == null) return;
        if (IsVanillaHost)
        {
            new LateTask(() =>
            {
                if (Player?.GetAbility<EvilGamblerAbility>() != this || !Player.IsAlive()
                    || !IsVanillaHost || AmongUsClient.Instance.IsGameOver) return;
                SHRKillCooldown = null;
                SHRKillRequests.SetRemainingCooldown(Player.Player, SHRPlayerOptions.KillCooldown(Player));
            }, 0.5f, "SHR EvilGambler Reset Kill Cooldown After Meeting");
            return;
        }
        // 会議終了時にキルクールをリセット（インポスター共通設定に準じる）
        new LateTask(() =>
        {
            float killTime = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
            ((ExPlayerControl)Player).SetKillTimerUnchecked(killTime, killTime);
        }, 0.5f, "EvilGamblerResetKillTimerOnWrapUp");
    }
}
