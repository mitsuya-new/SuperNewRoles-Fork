using System;
using System.Collections.Generic;
using UnityEngine;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules.Events.Bases;
using SuperNewRoles.Events;
using AmongUs.GameOptions;
using SuperNewRoles.Roles.Impostor;
using SuperNewRoles.Ability;
using SuperNewRoles.SuperTrophies;
using SuperNewRoles.Mode;
using System.Linq;
using Hazel;

namespace SuperNewRoles.Roles.Ability;

public class PenguinAbility : TargetCustomButtonBase, IButtonEffect
{
    private bool IsSHR => ModeManager.IsMode(ModeId.SuperHostRoles);
    private float remaining;
    private float nextSnap;
    private bool targetMoveable;
    internal bool CanUseNormalKill => CanKillNormally(CanDefaultKill, targetPlayer?.IsAlive() == true);
    internal bool IsHolding => targetPlayer != null;
    internal static bool CanKillNormally(bool canDefaultKill, bool holding) => canDefaultKill || holding;
    internal float SHRAbilityCooldown => targetPlayer == null ? DefaultTimer : Mathf.Max(0f, remaining);
    internal static bool IsDragged(ExPlayerControl player)
        => ModeManager.IsMode(ModeId.SuperHostRoles) && ExPlayerControl.ExPlayerControls.Any(
            owner => owner.GetAbility<PenguinAbility>()?.targetPlayer == player);

    internal ExPlayerControl FindSHRTarget()
        => targetPlayer != null ? null : SetTarget(onlyCrewmates: true, targetingPlayer: Player.Player,
            isTargetable: target => !SuperNewRoles.Patches.SetTargetPatch.IsMadKillerTeammate(Player, target)
                && !IsDragged(target));

    internal bool TryStartSHR()
    {
        var target = FindSHRTarget();
        if (target == null || !Player.IsAlive()) return false;
        RpcStartPenguin(target.Player);
        SHRKillRequests.SetRemainingCooldown(Player.Player, 0f);
        return true;
    }
    private float coolDown;
    // IButtonEffect
    private float effectDuration;
    public float EffectDuration => effectDuration;
    public Action OnEffectEnds => () =>
    {
        if (IsSHR && AmongUsClient.Instance?.AmHost != true) return;
        if (Player == null || Player.IsDead() || targetPlayer == null || targetPlayer.IsDead())
        {
            RpcEndPenguin();
            return;
        }
        RpcKillPenguinTarget(Player, this, targetPlayer, false);
    };
    public bool isEffectActive { get; set; }
    public float EffectTimer { get; set; }
    public bool effectCancellable => false;
    private bool meetingKill;
    public override Sprite Sprite => _sprite;
    public override string buttonText => ModTranslation.GetString("PenguinButtonText");
    protected override KeyType keytype => KeyType.Ability1;
    public override float DefaultTimer => coolDown;
    public override Color32 OutlineColor => Penguin.Instance.RoleColor;

    public override bool OnlyCrewmates => true;

    private ExPlayerControl targetPlayer;

    private KillableAbility customKillButtonAbility;
    private bool CanDefaultKill;
    private Sprite _sprite;
    public PenguinAbility(float coolDown, float effectDuration, bool meetingKill, bool CanDefaultKill)
    {
        this.coolDown = coolDown;
        this.effectDuration = effectDuration;
        this.meetingKill = meetingKill;
        this.CanDefaultKill = CanDefaultKill;
        _sprite = AssetManager.GetAsset<Sprite>($"PenguinButton_{ModHelpers.GetRandomInt(min: 1, max: 2)}.png");
    }

    public override void OnClick()
    {
        if (isEffectActive) return;
        if (Target == null || Target.IsDead()) return;
        targetPlayer = Target;
        RpcStartPenguin(targetPlayer);
        new LateTask(() => ExPlayerControl.LocalPlayer.SetKillTimerUnchecked(0.00001f, 0.00001f), 0f, "PenguinAbilitySetKillTimer");
        ResetTimer();
    }
    private void OnFixedUpdate()
    {
        if (targetPlayer == null) return;
        if (IsSHR)
        {
            if (targetPlayer.Data == null || Player.Data == null || targetPlayer.IsDead() || Player.IsDead()
                || targetPlayer.Data.Disconnected || Player.Data.Disconnected || AmongUsClient.Instance.IsGameOver)
            {
                if (AmongUsClient.Instance.AmHost) RpcEndPenguin();
                else EndPenguin();
                return;
            }
            if (MeetingHud.Instance != null || ExileController.Instance != null) return;
            if (targetPlayer.AmOwner) targetPlayer.Player.moveable = false;
            if (AmongUsClient.Instance.AmHost)
            {
                if (Time.unscaledTime >= nextSnap)
                {
                    nextSnap = Time.unscaledTime + 0.1f;
                    targetPlayer.NetTransform.RpcSnapTo(Player.transform.position);
                }
                remaining -= Time.fixedDeltaTime;
                if (remaining <= 0f) RpcKillPenguinTarget(Player, this, targetPlayer, false);
            }
            else targetPlayer.NetTransform.SnapTo(Player.transform.position);
            return;
        }
        // 死亡しても掴んでいる問題の対策
        if (targetPlayer.IsDead() || Player.IsDead())
        {
            EndPenguin();
            return;
        }
        // ここに来た時点で誰か掴んでる
        targetPlayer.NetTransform.SnapTo(Player.transform.position);
    }
    public override bool CheckIsAvailable()
    {
        return PlayerControl.LocalPlayer.CanMove && Target != null && Target.IsAlive();
    }
    public override void AttachToAlls()
    {
        base.AttachToAlls();
        SyncKillCoolTimeAbility.CreateAndAttach(this);
        SubscribeWithAbility(PreCalledMeetingEvent.Instance, OnPreCalledMeeting);
        customKillButtonAbility = new KillableAbility(() => CanDefaultKill || (targetPlayer != null && targetPlayer.IsAlive()));
        Player.AttachAbility(customKillButtonAbility, new AbilityParentAbility(this));
        SubscribeWithAbility(FixedUpdateEvent.Instance, OnFixedUpdate);
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
        SubscribeWithAbility(DieEvent.Instance, OnDie);
        SubscribeWithAbility(TryKillEvent.Instance, OnTryKill);
        if (IsSHR && AmongUsClient.Instance.AmHost && SHRPlayerOptions.IsVanillaGuest(Player.Player))
            SubscribeWithAbility(MurderEvent.Instance, data =>
            {
                if (data.killer != Player || !data.resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;
                Player.GetAbility<SHRAbilityTriggerAbility>()?.ResetCooldown(Time.unscaledTime, DefaultTimer);
                SHRPlayerOptions.Send(Player.Player, abilityCooldown: SHRAbilityCooldown, resetAbility: true);
            });
    }

    private void OnTryKill(TryKillEventData data)
    {
        if (data.Killer != Player || data.RefTarget != targetPlayer) return;
        if (IsSHR)
        {
            if (AmongUsClient.Instance.AmHost) RpcEndPenguin();
            return;
        }
        EndPenguin();
    }

    private void OnDie(DieEventData data)
    {
        if (targetPlayer == null) return;
        ExPlayerControl deadPlayer = data.player;
        if (deadPlayer != Player && deadPlayer != targetPlayer) return;
        RpcEndPenguin();
    }

    private void OnPreCalledMeeting(CalledMeetingEventData data)
    {
        if (!AmongUsClient.Instance.AmHost)
            return;
        if (targetPlayer != null && Player.IsAlive() && targetPlayer.IsAlive() && meetingKill)
        {
            RpcKillPenguinTargetBeforeMeeting(Player, this, targetPlayer);
        }
    }
    private void OnWrapUp(WrapUpEventData data)
    {
        if (targetPlayer == null) return;
        if (data.exiled?.PlayerId == Player.PlayerId || Player.IsDead())
            EndPenguin();
        else if (Player.AmOwner || (IsSHR && AmongUsClient.Instance.AmHost))
        {
            RpcKillPenguinTarget(Player, this, targetPlayer, true);
        }
    }
    [CustomRPC]
    public void RpcStartPenguin(PlayerControl target)
    {
        targetPlayer = target;
        if (!IsSHR || targetPlayer == null) return;
        remaining = EffectDuration;
        nextSnap = 0f;
        targetMoveable = targetPlayer.Player.moveable;
        if (targetPlayer.AmOwner) targetPlayer.Player.moveable = false;
        if (AmongUsClient.Instance.AmHost)
        {
            targetPlayer.NetTransform.RpcSnapTo(Player.transform.position);
            nextSnap = Time.unscaledTime + 0.1f;
            SHRPlayerOptions.Send(targetPlayer.Player);
        }
    }

    [CustomRPC]
    public void RpcEndPenguin()
    {
        EndPenguin();
    }

    private void EndPenguin()
    {
        var released = targetPlayer;
        targetPlayer = null;
        isEffectActive = false;
        EffectTimer = EffectDuration;
        if (!IsSHR || released == null) return;
        if (released.AmOwner && released.IsAlive()) released.Player.moveable = targetMoveable;
        if (AmongUsClient.Instance?.AmHost != true) return;
        SHRPlayerOptions.Send(released.Player);
        if (Player.IsAlive())
        {
            Player.GetAbility<SHRAbilityTriggerAbility>()?.ResetCooldown(Time.unscaledTime, DefaultTimer);
            SHRPlayerOptions.Send(Player.Player, abilityCooldown: DefaultTimer, resetAbility: true);
        }
    }

    public override void DetachToAlls()
    {
        if (targetPlayer != null)
        {
            if (IsSHR && AmongUsClient.Instance?.AmHost == true) RpcEndPenguin();
            else EndPenguin();
        }
        base.DetachToAlls();
    }

    [CustomRPC]
    public static void RpcKillPenguinTargetBeforeMeeting(ExPlayerControl source, PenguinAbility ability, ExPlayerControl target)
    {
        if (ModeManager.IsMode(ModeId.SuperHostRoles))
        {
            if (AmongUsClient.Instance.AmHost) SHRMurderSync.Murder(source, target, finalStatus: FinalStatus.Kill);
            ability?.EndPenguin();
            return;
        }
        if (source != null && source.IsAlive() && target != null && target.IsAlive())
        {
            // 会議開始時の死体集計に間に合わせるため、通常キルアニメーションを経由せず死体を生成する
            target.CustomDeath(CustomDeathType.KillWithoutKillAnimation, source: source);
        }
        ability?.EndPenguin();
    }

    [CustomRPC]
    public static void RpcKillPenguinTarget(ExPlayerControl source, PenguinAbility ability, ExPlayerControl target, bool afterMeeting)
    {
        if (ModeManager.IsMode(ModeId.SuperHostRoles))
        {
            if (AmongUsClient.Instance.AmHost && source?.IsAlive() == true && target?.IsAlive() == true)
            {
                if (afterMeeting)
                {
                    if (TryKillEvent.Invoke(source, ref target).RefSuccess)
                    {
                        var writer = AmongUsClient.Instance.StartRpcImmediately(target.Player.NetId,
                            (byte)RpcCalls.Exiled, SendOption.Reliable, -1);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                        target.Player.Exiled();
                        MurderEvent.Invoke(source, target, MurderResultFlags.Succeeded);
                        FinalStatusManager.SetFinalStatus(target, FinalStatus.Kill);
                        MurderDataManager.AddMurderData(source, target);
                    }
                }
                else SHRMurderSync.Murder(source, target, finalStatus: FinalStatus.Kill);
                if (source.IsAlive()) SHRKillRequests.SetRemainingCooldown(source.Player, SHRPlayerOptions.KillCooldown(source));
            }
            ability?.EndPenguin();
            return;
        }
        if (source != null && source.IsAlive() && target != null && target.IsAlive())
        {
            if (afterMeeting)
                target.CustomDeath(CustomDeathType.PenguinAfterMeeting, source: source);
            else
                target.CustomDeath(CustomDeathType.Kill, source: source);
        }
        ability?.EndPenguin();
    }
}

public class PenguinKillTrophy : SuperTrophyAbility<PenguinKillTrophy>
{
    public override TrophiesEnum TrophyId => TrophiesEnum.PenguinKill;
    public override TrophyRank TrophyRank => TrophyRank.Bronze;

    public override Type[] TargetAbilities => new Type[] { typeof(PenguinAbility) };

    private PenguinAbility _penguinAbility;
    private EventListener<MurderEventData> _onMurderEvent;

    public override void OnRegister()
    {
        // ペンギン能力の取得
        _penguinAbility = ExPlayerControl.LocalPlayer.GetAbility<PenguinAbility>();

        // 殺害イベントのリスナーを登録
        _onMurderEvent = MurderEvent.Instance.AddListener(HandleMurderEvent);
    }

    private void HandleMurderEvent(MurderEventData data)
    {
        // ローカルプレイヤーによるアクション以外は無視する
        if (data.killer != PlayerControl.LocalPlayer)
        {
            return;
        }

        // ペンギンの能力によるキルかどうか確認
        if (_penguinAbility != null && _penguinAbility.isEffectActive)
        {
            // 1回ペンギンでキルした実績を解除
            Complete();
        }
    }

    public override void OnDetached()
    {
        if (_onMurderEvent != null)
        {
            MurderEvent.Instance.RemoveListener(_onMurderEvent);
            _onMurderEvent = null;
        }
    }
}

public class PenguinHundredKillTrophy : SuperTrophyAbility<PenguinHundredKillTrophy>
{
    public override TrophiesEnum TrophyId => TrophiesEnum.PenguinHundredKill;
    public override TrophyRank TrophyRank => TrophyRank.Gold;

    public override Type[] TargetAbilities => new Type[] { typeof(PenguinAbility) };

    private PenguinAbility _penguinAbility;
    private EventListener<MurderEventData> _onMurderEvent;

    private const int RequiredKills = 100;

    public override void OnRegister()
    {
        // ペンギン能力の取得
        _penguinAbility = ExPlayerControl.LocalPlayer.GetAbility<PenguinAbility>();

        // 殺害イベントのリスナーを登録
        _onMurderEvent = MurderEvent.Instance.AddListener(HandleMurderEvent);
    }

    private void HandleMurderEvent(MurderEventData data)
    {
        // ローカルプレイヤーによるアクション以外は無視する
        if (data.killer != PlayerControl.LocalPlayer)
        {
            return;
        }

        // ペンギンの能力によるキルかどうか確認
        if (_penguinAbility != null && _penguinAbility.isEffectActive)
        {
            // キルカウントを増やす
            TrophyData++;

            // 100回達成したらトロフィー獲得
            if (TrophyData >= RequiredKills)
            {
                Complete();
            }
        }
    }

    public override void OnDetached()
    {
        if (_onMurderEvent != null)
        {
            MurderEvent.Instance.RemoveListener(_onMurderEvent);
            _onMurderEvent = null;
        }
    }
}
