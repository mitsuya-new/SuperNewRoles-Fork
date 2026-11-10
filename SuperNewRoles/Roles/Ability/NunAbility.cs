using System.Collections;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Modules;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using SuperNewRoles.Mode;

namespace SuperNewRoles.Roles.Ability;

public class NunAbility : CustomButtonBase
{
    private float coolDown;

    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("NunButton.png");
    public override string buttonText => ModTranslation.GetString("NunButtonName");
    protected override KeyType keytype => KeyType.Ability1;
    public override float DefaultTimer => coolDown;

    public NunAbility(float coolDown)
    {
        this.coolDown = coolDown;
    }

    public override void OnClick()
    {
        AirshipStatus airshipStatus = ShipStatus.Instance.TryCast<AirshipStatus>();
        if (airshipStatus == null) return;

        MovingPlatformBehaviour platform = airshipStatus.GapPlatform;
        if (platform == null) return;
        RpcUsePlatform(platform.IsLeft, ModeManager.IsMode(ModeId.SuperHostRoles) ? SHRNunPlatform.PassengerId(platform) : platform.Target?.PlayerId ?? byte.MaxValue);
        ResetTimer();
    }

    internal bool TryUseSHR()
    {
        var platform = ShipStatus.Instance?.TryCast<AirshipStatus>()?.GapPlatform;
        if (platform == null) return false;
        RpcUsePlatform(platform.IsLeft, SHRNunPlatform.PassengerId(platform));
        ResetTimer();
        return true;
    }

    [CustomRPC]
    public void RpcUsePlatform(bool startIsLeft, byte movingTargetId)
    {
        if (ModeManager.IsMode(ModeId.SuperHostRoles) && AmongUsClient.Instance?.AmHost == true
            && (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started || AmongUsClient.Instance.IsGameOver
                || MeetingHud.Instance != null || ExileController.Instance != null)) return;
        AirshipStatus airshipStatus = GameObject.FindObjectOfType<AirshipStatus>();
        if (airshipStatus && airshipStatus.GapPlatform != null)
        {
            if (ModeManager.IsMode(ModeId.SuperHostRoles)) SHRNunPlatform.Cancel();
            airshipStatus.GapPlatform.StopAllCoroutines();
            airshipStatus.GapPlatform.StartCoroutine(NotMoveUsePlatform(airshipStatus.GapPlatform, startIsLeft, movingTargetId).WrapToIl2Cpp());
        }
    }

    private static IEnumerator NotMoveUsePlatform(MovingPlatformBehaviour platform, bool startIsLeft, byte movingTargetId)
    {
        platform.IsLeft = startIsLeft;

        PlayerControl movingTarget = movingTargetId == byte.MaxValue ? null : ExPlayerControl.ById(movingTargetId);
        bool hasMovingTarget = movingTarget != null;
        bool carryMovingTarget = hasMovingTarget && IsTargetOnPlatform(platform, movingTarget);
        if (hasMovingTarget)
        {
            platform.Target = movingTarget;
        }
        if (platform.Target == null)
        {
            platform.Target = PlayerControl.LocalPlayer;
        }
        Vector3 val2 = (!startIsLeft) ? platform.LeftUsePosition : platform.RightUsePosition;
        Vector3 targetPos = (!startIsLeft) ? platform.LeftPosition : platform.RightPosition;
        Vector3 worldUseTargetPos = platform.transform.parent.TransformPoint(val2);
        Vector3 worldTargetPos2 = platform.transform.parent.TransformPoint(targetPos);
        Vector3 movingTargetOffset = Vector3.zero;
        Vector3 movingTargetDestination = Vector3.zero;

        if (hasMovingTarget)
        {
            RestoreInterruptedTarget(movingTarget, releaseMovement: !carryMovingTarget);
            if (carryMovingTarget)
            {
                movingTarget.moveable = false;
                movingTarget.NetTransform.SetPaused(true);
                movingTarget.SetKinematic(true);
                movingTarget.inMovingPlat = true;
                movingTarget.ForceKillTimerContinue = true;
                movingTargetOffset = movingTarget.transform.position - platform.transform.position;
                movingTargetDestination = worldTargetPos2 + movingTargetOffset;
            }
        }

        if (Constants.ShouldPlaySfx())
        {
            SoundManager.Instance.StopNamedSound("PlatformMoving");
            SoundManager.Instance.PlayDynamicSound("PlatformMoving", platform.MovingSound, loop: true, (DynamicSound.GetDynamicsFunction)platform.SoundDynamics, SoundManager.instance.sfxMixer);
        }

        platform.IsLeft = !startIsLeft;
        if (ModeManager.IsMode(ModeId.SuperHostRoles)) SHRNunPlatform.Begin(platform, carryMovingTarget ? movingTarget : null, movingTarget);

        if (carryMovingTarget)
        {
            yield return Effects.All(
                Effects.Slide2D(platform.transform, platform.transform.localPosition, targetPos, movingTarget.MyPhysics.Speed),
                Effects.Slide2DWorld(movingTarget.transform, movingTarget.transform.position, movingTargetDestination, movingTarget.MyPhysics.Speed)
            );
        }
        else
        {
            yield return Effects.All(Effects.Slide2D(platform.transform, platform.transform.localPosition, targetPos, PlayerControl.LocalPlayer.MyPhysics.Speed));
        }

        if (Constants.ShouldPlaySfx())
        {
            SoundManager.Instance.StopNamedSound("PlatformMoving");
        }

        if (hasMovingTarget)
        {
            if (carryMovingTarget)
            {
                yield return movingTarget.MyPhysics.WalkPlayerTo(worldUseTargetPos);
                yield return Effects.Wait(0.1f);
            }
            if (movingTarget != null)
                RestoreInterruptedTarget(movingTarget, releaseMovement: true);
        }

        platform.Target = null;
        if (ModeManager.IsMode(ModeId.SuperHostRoles)) SHRNunPlatform.Finish(platform);
    }

    public override bool CheckIsAvailable()
    {
        AirshipStatus airshipStatus = ShipStatus.Instance.TryCast<AirshipStatus>();
        return Timer <= 0 && PlayerControl.LocalPlayer.CanMove && airshipStatus?.GapPlatform != null;
    }

    private static bool IsTargetOnPlatform(MovingPlatformBehaviour platform, PlayerControl target)
    {
        if (platform == null || target == null) return false;
        return SHRNunPlatform.IsOnPlatform(Vector3.Distance(target.transform.position, platform.transform.position));
    }

    internal static void RestoreInterruptedTarget(PlayerControl target, bool releaseMovement)
    {
        if (target == null) return;

        target.MyPhysics.enabled = true;
        target.MyPhysics.ResetMoveState();
        target.MyPhysics.ResetAnimState();
        target.MyPhysics.body.velocity = Vector2.zero;
        target.Collider.enabled = true;
        target.NetTransform.enabled = true;

        if (!releaseMovement) return;

        target.inMovingPlat = false;
        target.onLadder = false;
        target.moveable = true;
        target.ForceKillTimerContinue = false;
        target.NetTransform.SetPaused(false);
        target.SetKinematic(false);
        target.NetTransform.SnapTo(target.transform.position);
        target.SetPetPosition(target.transform.position);
    }
}
