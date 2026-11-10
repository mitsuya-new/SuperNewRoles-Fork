using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;

namespace SuperNewRoles.Roles.Ability;

public class StuntmanAbility : AbilityBase, IAbilityCount
{
    internal bool TryConsumeShield()
    {
        if (Count <= 0) return false;
        Count--;
        return true;
    }

    public StuntmanAbility(int count)
    {
        Count = count;
    }


    public override void AttachToAlls()
    {
        base.AttachToAlls();
        SubscribeWithAbility(TryKillEvent.Instance, OnTryKill);
    }
    private void OnTryKill(TryKillEventData data)
    {
        bool shr = SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles);
        if (shr && AmongUsClient.Instance?.AmHost != true) return;
        if (data.RefTarget != Player) return;
        if (!TryConsumeShield()) return;
        if (shr) RpcSetSHRShieldCount(this, Count);
        data.RefSuccess = false;
        if (shr)
        {
            SuperNewRoles.Mode.SHRKillRequests.SetRemainingCooldown(data.Killer.Player,
                SuperNewRoles.Mode.SHRPlayerOptions.KillCooldown(data.Killer), data.RefTarget.Player);
            RpcSHRShieldFeedback(data.Killer, data.RefTarget);
            return;
        }
        if (data.Killer.AmOwner || ExPlayerControl.LocalPlayer.IsDead())
            data.RefTarget.Player.ShowFailedMurder();
        if (data.Killer.AmOwner)
            data.Killer.ResetKillCooldown();
    }

    [CustomRPC]
    public static void RpcSetSHRShieldCount(StuntmanAbility ability, int remaining)
    {
        if (ability != null) ability.Count = System.Math.Max(0, remaining);
    }

    [CustomRPC]
    public static void RpcSHRShieldFeedback(ExPlayerControl killer, ExPlayerControl target)
    {
        if (killer?.Player == null || target?.Player == null || ExPlayerControl.LocalPlayer == null) return;
        if (killer.AmOwner || ExPlayerControl.LocalPlayer.IsDead()) target.Player.ShowFailedMurder();
        if (killer.AmOwner) killer.ResetKillCooldown();
    }
}
