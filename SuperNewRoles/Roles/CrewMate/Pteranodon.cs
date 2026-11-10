using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Events;
using SuperNewRoles.Mode;
using InnerNet;

namespace SuperNewRoles.Roles.CrewMate;

class Pteranodon : RoleBase<Pteranodon>
{
    public override RoleId Role { get; } = RoleId.Pteranodon;
    public override Color32 RoleColor { get; } = new Color32(17, 128, 45, byte.MaxValue);
    public override List<Func<AbilityBase>> Abilities { get; } = [
        () => new PteranodonAbility(new PteranodonData(
            coolTime: PteranodonCoolTime
        ))
    ];

    public override QuoteMod QuoteMod { get; } = QuoteMod.SuperNewRoles;
    public override RoleTypes IntroSoundType { get; } = RoleTypes.Crewmate;
    public override short IntroNum { get; } = 1;

    public override AssignedTeamType AssignedTeam { get; } = AssignedTeamType.Crewmate;
    public override WinnerTeamType WinnerTeam { get; } = WinnerTeamType.Crewmate;
    public override TeamTag TeamTag { get; } = TeamTag.Crewmate;
    public override RoleTag[] RoleTags { get; } = [];
    public override RoleOptionMenuType OptionTeam { get; } = RoleOptionMenuType.Crewmate;
    public override RoleId[] RelatedRoleIds { get; } = [];
    public override MapNames[] AvailableMaps { get; } = [MapNames.Airship];

    [CustomOptionFloat("PteranodonCoolTime", 0, 120f, 2.5f, 5f, translationName: "CoolTime")]
    public static float PteranodonCoolTime;
}

public class PteranodonAbility : CustomButtonBase
{
    private readonly PteranodonData _data;

    // 飛行状態の管理
    private bool _isFlyingNow;
    private Vector2 _startPosition;
    private Vector2 _targetPosition;
    private Vector2 _currentPosition;
    private float _timer;
    private const float StartTime = 2f;
    private FollowerCamera _camera;
    private readonly SHRFlightFrames shrFrames = new();
    private int flightGameId;
    internal bool IsFlyingSHR => ModeManager.IsMode(ModeId.SuperHostRoles) && _isFlyingNow;
    private bool IsAuthority => ModeManager.IsMode(ModeId.SuperHostRoles) ? AmongUsClient.Instance?.AmHost == true : Player.AmOwner;

    public PteranodonAbility(PteranodonData data)
    {
        _data = data;
    }

    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("PteranodonButton.png");
    public override string buttonText => ModTranslation.GetString("Pteranodon.ButtonTitle");
    protected override KeyType keytype => KeyType.Ability1;
    public override float DefaultTimer => _data.CoolTime;

    public override void AttachToLocalPlayer()
    {
        base.AttachToLocalPlayer();
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
        SubscribeWithAbility(MeetingStartEvent.Instance, OnMeetingStart);
        _isFlyingNow = false;
        _startPosition = new Vector2();
        _targetPosition = new Vector2();
        _timer = 0;
        _camera = Camera.main.GetComponent<FollowerCamera>();
    }

    public override void AttachToAlls()
    {
        base.AttachToAlls();
        SubscribeWithAbility(FixedUpdateEvent.Instance, OnFixedUpdate);
        SubscribeWithAbility(MeetingStartEvent.Instance, _ => CancelSHRFlight());
    }

    private void OnWrapUp(WrapUpEventData data)
    {
        if (ModeManager.IsMode(ModeId.SuperHostRoles) && !IsAuthority) return;
        // 会議時には飛行状態を解除
        if (!_isFlyingNow || Player?.Player == null) return;

        // 通常の飛行終了と同じ終了RPCを送信し、ローカルと他クライアントの状態を復元する
        SetStatusRPC(this, false, _startPosition, _startPosition, 0f);
    }

    private void OnMeetingStart(MeetingStartEventData data)
    {
        if (ModeManager.IsMode(ModeId.SuperHostRoles) && !IsAuthority) return;
        if (!_isFlyingNow || Player?.Player == null) return;

        SetStatusRPC(this, false, _startPosition, _startPosition, 0f);
    }

    public override void DetachToLocalPlayer()
    {
        if (_isFlyingNow && Player?.AmOwner == true && IsAuthority)
            SetStatusRPC(this, false, _startPosition, _startPosition, 0f);

        base.DetachToLocalPlayer();
    }

    public override void DetachToAlls()
    {
        CancelSHRFlight();
        RestoreFlyingStateLocally();
        base.DetachToAlls();
    }

    public override void OnClick()
    {
        if (ModeManager.IsMode(ModeId.SuperHostRoles))
        {
            TryStartSHR();
            return;
        }
        if (!TryGetFlightPath(out var start, out var destination)) return;
        // 飛行機能の実装
        // 現在位置と目標位置を設定
        _startPosition = start;
        _targetPosition = destination;
        _currentPosition = _startPosition;
        _timer = StartTime;
        _isFlyingNow = true;

        // プレイヤーの移動を無効化
        PlayerControl.LocalPlayer.Collider.enabled = false;
        PlayerControl.LocalPlayer.moveable = false;

        SetStatusRPC(this, true, _targetPosition, _startPosition, StartTime);
    }

    internal bool TryStartSHR()
    {
        if (_isFlyingNow || AmongUsClient.Instance == null || !TryGetFlightPath(out var start, out var destination)) return false;
        if (AmongUsClient.Instance.AmHost)
        {
            if (!SHRAbilityTriggerPatches.CanActivate(Player.Player)) return false;
        }
        else if (!Player.AmOwner || !Player.Player.CanMove || MeetingHud.Instance != null || ExileController.Instance != null)
            return false;
        SetStatusRPC(this, true, destination, start, StartTime);
        ResetTimer();
        return true;
    }

    private void CancelSHRFlight()
    {
        if (!IsFlyingSHR || !IsAuthority) return;
        if (AmongUsClient.Instance.GameId != flightGameId
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver)
        {
            RestoreFlyingStateLocally();
            return;
        }
        SetStatusRPC(this, false, _startPosition, _startPosition, 0f);
    }

    public override bool CheckIsAvailable()
    {
        if (!PlayerControl.LocalPlayer.CanMove) return false;
        return TryGetFlightPath(out _, out _);
    }

    internal static bool? SelectDeparture(float leftDistance, float rightDistance, bool ridingPlatform)
    {
        if (ridingPlatform) return null;
        if (leftDistance <= 0.9f) return true;
        if (rightDistance <= 0.9f) return false;
        return null;
    }

    internal bool TryGetFlightPath(out Vector3 start, out Vector3 destination)
    {
        start = default;
        destination = default;
        if (Player?.Player == null) return false;
        AirshipStatus status = ShipStatus.Instance.TryCast<AirshipStatus>();
        if (status?.GapPlatform == null) return false;
        var platform = status.GapPlatform;
        start = Player.transform.position;
        Vector3 left = platform.transform.parent.TransformPoint(platform.LeftUsePosition);
        Vector3 right = platform.transform.parent.TransformPoint(platform.RightUsePosition);
        bool? fromLeft = SelectDeparture(Vector3.Distance(left, start), Vector3.Distance(right, start),
            platform.Target != null && platform.Target.PlayerId == Player.PlayerId);
        if (!fromLeft.HasValue) return false;
        destination = fromLeft.Value ? right : left;
        return true;
    }

    public void OnFixedUpdate()
    {
        // 自分自身の飛行の処理
        if (!_isFlyingNow) return;
        if (ModeManager.IsMode(ModeId.SuperHostRoles) && IsAuthority
            && (AmongUsClient.Instance.GameId != flightGameId || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started
                || AmongUsClient.Instance.IsGameOver || Player.Data.Disconnected || Player.IsDead()
                || MeetingHud.Instance != null || ExileController.Instance != null))
        {
            CancelSHRFlight();
            return;
        }

        _timer -= Time.fixedDeltaTime;
        Vector3 pos = _currentPosition;
        Player.MyPhysics.Animations.PlayIdleAnimation();
        Player.cosmetics.AnimateSkinIdle();
        float tarpos = (_targetPosition.x - _startPosition.x);

        if ((_timer + 0.025f) > (StartTime / 2f))
        {
            pos.y += (((_timer + 0.025f) - (StartTime / 2)) * 4f) * Time.fixedDeltaTime;
        }
        else
        {
            pos.y -= (((StartTime / 2) - (_timer + 0.025f)) * 4f) * Time.fixedDeltaTime;
        }

        pos.x += tarpos * Time.fixedDeltaTime * 0.5f;
        Player.MyPhysics.body.velocity = Vector2.zero;

        if (_timer <= 0)
        {
            _isFlyingNow = false;
            Player.Player.Collider.enabled = true;
            Player.Player.moveable = true;

            Vector3 position = Player.Player.transform.position;

            if (IsAuthority)
            {
                SetStatusRPC(this, false, position, _startPosition, 0f);
            }
            else
                Player.NetTransform.SnapTo(position);
            return;
        }

        _currentPosition = pos;
        Player.transform.position = pos;
        if (ModeManager.IsMode(ModeId.SuperHostRoles) && IsAuthority && shrFrames.Advance(StartTime - _timer))
            SHRFlightSync.Send(Player, pos);

        if (Player.AmOwner && _camera != null)
        {
            _camera.transform.position = Vector3.Lerp(_camera.centerPosition, (Vector2)_camera.Target.transform.position + _camera.Offset, 5f * Time.deltaTime);
        }

    }

    [CustomRPC]
    public static void SetStatusRPC(PteranodonAbility ability, bool status, Vector3 targetPosition, Vector3 startPosition, float timer)
    {
        bool shr = ModeManager.IsMode(ModeId.SuperHostRoles);
        if (shr && status && AmongUsClient.Instance.AmHost
            && (!SHRAbilityTriggerPatches.CanActivate(ability.Player.Player) || ability._isFlyingNow
                || !ability.TryGetFlightPath(out var actualStart, out var actualTarget)
                || Vector3.Distance(actualStart, startPosition) > 0.1f || Vector3.Distance(actualTarget, targetPosition) > 0.1f)) return;
        // プレイヤーの飛行状態を設定
        if (status)
        {

            ability._targetPosition = targetPosition;
            ability._currentPosition = ability._startPosition = startPosition;
            ability._timer = timer;
            ability._isFlyingNow = true;
            ability.shrFrames.Reset();
            ability.flightGameId = AmongUsClient.Instance.GameId;

            ability.Player.Player.NetTransform.enabled = false;
            ability.Player.Player.Collider.enabled = false;
            ability.Player.Player.moveable = false;
            if (shr)
            {
                ability.Player.NetTransform.SetPaused(true);
                if (AmongUsClient.Instance.AmHost) SHRPlayerOptions.Send(ability.Player.Player);
            }
        }
        else
        {
            ability._isFlyingNow = false;
            ability.Player.Player.NetTransform.enabled = true;
            ability.Player.Player.Collider.enabled = true;
            ability.Player.Player.moveable = true;
            ability.Player.Player.transform.position = targetPosition;
            if (shr)
            {
                ability.Player.NetTransform.SetPaused(false);
                ability.Player.NetTransform.SnapTo(targetPosition);
                if (AmongUsClient.Instance.AmHost)
                {
                    SHRFlightSync.Send(ability.Player, targetPosition);
                    SHRPlayerOptions.Send(ability.Player.Player);
                }
            }
            else if (ability.Player.AmOwner)
                ability.Player.NetTransform.RpcSnapTo(targetPosition);
            else
                ability.Player.NetTransform.SnapTo(targetPosition);
            ability._isFlyingNow = false;
        }
    }

    private void RestoreFlyingStateLocally()
    {
        if (!_isFlyingNow || Player?.Player == null) return;

        if (ModeManager.IsMode(ModeId.SuperHostRoles)) Player.NetTransform.SetPaused(false);

        Player.Player.NetTransform.enabled = true;
        Player.Player.Collider.enabled = true;
        Player.Player.moveable = true;
        Player.Player.transform.position = _startPosition;
        Player.NetTransform.SnapTo(_startPosition);
        _isFlyingNow = false;
    }
}

public class PteranodonData
{
    public float CoolTime { get; }

    public PteranodonData(float coolTime)
    {
        CoolTime = coolTime;
    }
}
