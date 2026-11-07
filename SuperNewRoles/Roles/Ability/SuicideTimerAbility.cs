using System;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Mode;
using TMPro;
using UnityEngine;

namespace SuperNewRoles.Roles.Ability;

public class SuicideTimerAbility : AbilityBase
{
    public Func<float> SuicideTimeGetter { get; }
    public Func<bool> ResetOnMeetingGetter { get; }
    private float timer;
    public float CurrentTimer => timer;
    private bool firstKilled = false;
    internal float NativeCooldown => firstKilled ? Math.Max(0f, timer) : 0f;
    private bool IsSHR => ModeManager.IsMode(ModeId.SuperHostRoles);
    private TextMeshPro timerText;
    private int _lastDisplayedSeconds = int.MinValue;

    public SuicideTimerAbility(Func<float> suicideTimeGetter, Func<bool> resetOnMeetingGetter)
    {
        SuicideTimeGetter = suicideTimeGetter;
        ResetOnMeetingGetter = resetOnMeetingGetter;
    }

    public override void AttachToLocalPlayer()
    {
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
        SubscribeWithAbility(FixedUpdateEvent.Instance, OnUpdate);
        // 初期状態でタイマーをセット
        ResetSuicideTimer();

        Transform timerParent = GetTimerTextParent();
        timerText = GameObject.Instantiate(FastDestroyableSingleton<HudManager>.Instance.KillButton.cooldownTimerText, timerParent);
        timerText.text = "";
        timerText.enableWordWrapping = false;
        timerText.transform.localScale = Vector3.one * 0.5f;
        timerText.transform.localPosition += new Vector3(-0.05f, 0.7f, 0);
        timerText.gameObject.SetActive(true);
    }

    public override void AttachToOthers()
    {
        if (!IsSHR || AmongUsClient.Instance?.AmHost != true) return;
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
        SubscribeWithAbility(FixedUpdateEvent.Instance, OnUpdate);
        ResetSuicideTimer();
    }

    public override void DetachToLocalPlayer()
    {
        base.DetachToLocalPlayer();
        if (timerText != null)
        {
            GameObject.Destroy(timerText.gameObject);
            timerText = null;
        }
    }

    private void OnMurder(MurderEventData data)
    {
        if (Player == null) return;
        if (data.killer == null) return;
        if (IsSHR && (!data.resultFlags.HasFlag(MurderResultFlags.Succeeded)
            || data.target == null || data.target.PlayerId == Player.PlayerId || !data.target.IsDead())) return;
        if (Player.PlayerId == data.killer.PlayerId)
        {
            // キルが発生した時にタイマーをリセット
            ResetSuicideTimer();
            firstKilled = true;
            SyncNativeTimer();
        }
    }

    private void OnWrapUp(WrapUpEventData data)
    {
        if (Player == null) return;

        if (IsSHR)
        {
            if (ResetOnMeetingGetter()) ResetSuicideTimer();
            new LateTask(() =>
            {
                if (Player?.GetAbility<SuicideTimerAbility>() != this || !Player.IsAlive()) return;
                SyncNativeTimer();
            }, 0.5f, "SHR SerialKiller Timer After Meeting");
            return;
        }

        // 会議後にリセットする設定の場合
        if (ResetOnMeetingGetter())
        {
            new LateTask(() =>
            {
                ResetSuicideTimer();
            }, 0.5f, "SerialKiller ResetTimer After Meeting");
        }
    }

    private void ResetSuicideTimer()
    {
        // 新しいタイマーをセット
        float suicideTime = SuicideTimeGetter();
        timer = suicideTime;
    }

    private void SyncNativeTimer()
    {
        if (IsSHR && AmongUsClient.Instance?.AmHost == true && Player.IsAlive())
            SHRPlayerOptions.Send(Player.Player, abilityCooldown: NativeCooldown, resetAbility: true);
    }

    private void OnUpdate()
    {
        if (MeetingHud.Instance != null || ExileController.Instance != null || !firstKilled
            || (IsSHR && (AmongUsClient.Instance == null || AmongUsClient.Instance.IsGameOver
                || HudManager.Instance == null || HudManager.Instance.IsIntroDisplayed)))
            return;
        if (Player == null || Player.Data == null || Player.IsDead() || Player.Data.Disconnected)
        {
            if (timerText != null)
            {
                GameObject.Destroy(timerText.gameObject);
                timerText = null;
            }
            return;
        }
        timer -= Time.fixedDeltaTime;
        if (timer <= 0)
        {
            if (!IsSHR || AmongUsClient.Instance.AmHost)
                Player.RpcCustomDeath(CustomDeathType.Suicide);
        }
        if (timerText == null) return;
        int displaySeconds = ((int)timer) + 1;
        if (displaySeconds != _lastDisplayedSeconds)
        {
            _lastDisplayedSeconds = displaySeconds;
            timerText.text = string.Format(ModTranslation.GetString("SerialKillerSuicideText"), displaySeconds);
        }
    }

    private Transform GetTimerTextParent()
    {
        var killButton = Player?.GetAbility<CustomKillButtonAbility>();
        if (killButton?.actionButton != null)
            return killButton.actionButton.transform;

        return FastDestroyableSingleton<HudManager>.Instance.KillButton.cooldownTimerText.transform.parent;
    }
}
