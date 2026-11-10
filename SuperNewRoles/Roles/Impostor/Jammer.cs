using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Events;

namespace SuperNewRoles.Roles.Impostor;

// 提案者：gamerkun さん
class Jammer : RoleBase<Jammer>
{
    public override RoleId Role => RoleId.Jammer;
    public override Sprite RoleIcon => AssetManager.GetAsset<Sprite>("JammerRoleIcon.png");
    public override Color32 RoleColor => Palette.ImpostorRed;
    public override List<Func<AbilityBase>> Abilities => [
        () => new JammerAbility(
            JammerCoolTime,
            JammerDurationTime,
            JammerAbilityCount,
            JammerCanUseAbilitiesAgainstImposter
        )
    ];

    public override QuoteMod QuoteMod => QuoteMod.SuperNewRoles;
    public override RoleTypes IntroSoundType => RoleTypes.Shapeshifter;
    public override short IntroNum => 1;

    public override AssignedTeamType AssignedTeam => AssignedTeamType.Impostor;
    public override WinnerTeamType WinnerTeam => WinnerTeamType.Impostor;
    public override TeamTag TeamTag => TeamTag.Impostor;
    public override RoleTag[] RoleTags => [RoleTag.Information];
    public override RoleOptionMenuType OptionTeam => RoleOptionMenuType.Impostor;

    [CustomOptionFloat("JammerCoolTime", 2.5f, 60f, 2.5f, 25f, translationName: "CoolTime")]
    public static float JammerCoolTime;
    [CustomOptionFloat("JammerDurationTime", 2.5f, 120f, 2.5f, 10f, translationName: "DurationTime")]
    public static float JammerDurationTime;
    [CustomOptionInt("JammerAbilityCount", 1, 15, 1, 3, translationName: "UseLimit")]
    public static int JammerAbilityCount;
    [CustomOptionBool("JammerCanUseAbilitiesAgainstImposter", false, translationName: "JammerCanUseAbilitiesAgainstImposter")]
    public static bool JammerCanUseAbilitiesAgainstImposter;
}

public class JammerAbility : TargetCustomButtonBase, IButtonEffect
{
    private float _coolTime;
    private float _durationTime;
    private int _abilityCount;
    private bool _canUseAgainstImpostors;
    private int _usedCount;
    private ExPlayerControl _invisibleTarget;
    private float shrInvisibleUntil;
    private readonly OpacityFadeController _opacityFader = new();

    public bool isEffectActive { get; set; }
    public float EffectTimer { get; set; }
    public float EffectDuration => _durationTime;
    public Action OnEffectEnds => () =>
    {
        if (_invisibleTarget != null)
        {
            RpcSetInvisible(_invisibleTarget, false);
            _invisibleTarget = null;
        }
    };
    public bool effectCancellable => true;

    public override Color32 OutlineColor => Color.red;
    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("JammerButton.png");
    public override string buttonText => ModTranslation.GetString("JammerButtonName");
    protected override KeyType keytype => KeyType.Ability1;
    public override float DefaultTimer => _coolTime;
    public override bool OnlyCrewmates => !_canUseAgainstImpostors;
    public override bool TargetPlayersInVents => false;
    internal int SHRUsesRemaining => Math.Max(0, _abilityCount - _usedCount);
    internal bool TryUseSHR(ExPlayerControl target)
    {
        if (target == null || SHRUsesRemaining == 0) return false;
        Target = target;
        OnClick();
        return true;
    }

    public JammerAbility(float coolTime, float durationTime, int abilityCount, bool canUseAgainstImpostors)
    {
        _coolTime = coolTime;
        _durationTime = durationTime;
        _abilityCount = abilityCount;
        _canUseAgainstImpostors = canUseAgainstImpostors;
        _usedCount = 0;
    }

    public override bool CheckIsAvailable()
    {
        if (!Player.IsAlive()) return false;
        if (!Player.Player.CanMove) return false;
        if (_usedCount >= _abilityCount) return false;
        if (!TargetIsExist) return false;
        return true;
    }

    public override void OnClick()
    {
        if (Target == null) return;

        if (_invisibleTarget != null && _invisibleTarget != Target)
        {
            RpcSetInvisible(_invisibleTarget, false);
        }

        _invisibleTarget = Target;
        RpcSetInvisible(Target, true);
        _usedCount++;
        ResetTimer();
    }

    public override void AttachToAlls()
    {
        base.AttachToAlls();
        SubscribeWithAbility(FixedUpdateEvent.Instance, () => OnFixedUpdate());
    }

    public override void DetachToAlls()
    {
        base.DetachToAlls();
        _opacityFader.StopAll();
        if (_invisibleTarget != null && SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles) && AmongUsClient.Instance?.AmHost == true)
            SuperNewRoles.Mode.SHRPrivatePlayerEffects.SetHidden(_invisibleTarget, this, false, null);
    }

    public override void AttachToLocalPlayer()
    {
        base.AttachToLocalPlayer();
        SubscribeWithAbility(MeetingStartEvent.Instance, OnMeetingStart);
    }

    public override void DetachToLocalPlayer()
    {
        base.DetachToLocalPlayer();
        // クリーンアップ：透明効果を確実に解除
        if (_invisibleTarget != null)
        {
            RpcSetInvisible(_invisibleTarget, false);
            _invisibleTarget = null;
        }
    }

    private void OnMeetingStart(MeetingStartEventData data)
    {
        if (_invisibleTarget != null)
        {
            RpcSetInvisible(_invisibleTarget, false);
            _invisibleTarget = null;
        }
    }

    private void OnFixedUpdate()
    {
        if (SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles) && AmongUsClient.Instance.AmHost
            && _invisibleTarget != null && (Player.IsDead() || Player.Data.Disconnected || _invisibleTarget.IsDead()
                || _invisibleTarget.Data.Disconnected || Time.unscaledTime >= shrInvisibleUntil || MeetingHud.Instance != null))
        {
            RpcSetInvisible(_invisibleTarget, false);
            isEffectActive = false;
            return;
        }
        if (_invisibleTarget != null && !_invisibleTarget.IsDead())
        {
            _opacityFader.Apply(_invisibleTarget, CanSeeTranslucentState(_invisibleTarget, out var opacity) ? opacity : 0f, forceSnap: true);
        }
    }

    [CustomRPC]
    public void RpcSetInvisible(ExPlayerControl target, bool isInvisible)
    {
        if (SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles))
        {
            if (isInvisible)
            {
                _invisibleTarget = target;
                shrInvisibleUntil = Time.unscaledTime + _durationTime;
            }
            else if (_invisibleTarget == target) _invisibleTarget = null;
        }
        SetInvisible(target, isInvisible);
        if (AmongUsClient.Instance?.AmHost == true && SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles))
            SuperNewRoles.Mode.SHRPrivatePlayerEffects.SetHidden(target, this, isInvisible,
                viewer => SuperNewRoles.Mode.SHRPrivatePlayerEffects.ShouldHideFrom(viewer == target.Player,
                    SuperNewRoles.Mode.SHRPlayerOptions.IsVanillaGuest(viewer), ((ExPlayerControl)viewer).IsImpostor()));
    }

    private void SetInvisible(ExPlayerControl target, bool isInvisible)
    {
        if (isInvisible)
        {
            _opacityFader.Apply(target, CanSeeTranslucentState(target, out var opacity) ? opacity : 0f);
        }
        else
        {
            _opacityFader.Apply(target, 1f);
        }
    }

    private bool CanSeeTranslucentState(ExPlayerControl invisibleTarget, out float opacity)
    {
        if (invisibleTarget == ExPlayerControl.LocalPlayer)
        {
            opacity = 1f;
            return true;
        }
        if (ExPlayerControl.LocalPlayer.IsImpostor())
        {
            opacity = 0.4f;
            return true;
        }
        opacity = 0f;
        return false;
    }
}
