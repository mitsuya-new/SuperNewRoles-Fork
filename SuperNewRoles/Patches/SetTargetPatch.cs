using SuperNewRoles.Events;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles.Roles.Impostor;
using System.Linq;

namespace SuperNewRoles.Patches;

public static class SetTargetPatch
{
    public static void Register()
    {
        FixedUpdateEvent.Instance.AddListener(ImpostorSetTarget);
    }
    private static void ImpostorSetTarget()
    {
        if (FastDestroyableSingleton<HudManager>.Instance.KillButton.isActiveAndEnabled)
            FastDestroyableSingleton<HudManager>.Instance.KillButton.SetTarget(SetImpostorTarget());
    }
    /// <summary>
    /// MadKillerのターゲット設定を行う
    /// </summary>
    /// <param name="player">プレイヤー</param>
    /// <returns>ターゲット設定が可能かどうか</returns>
    public static bool ValidMadkiller(ExPlayerControl player)
    {
        if (player.IsImpostor())
            return false;
        if (player.Role == Roles.RoleId.MadKiller)
            return !IsMadKillerTeammate(true, player.Role,
                player.GetAbility<MadKillerAbility>()?.IsAwakened == true, SideKiller.CannotSeeMadKillerBeforePromotion);
        return true;
    }
    internal static bool IsMadKillerTeammate(bool killerIsImpostor, Roles.RoleId targetRole, bool awakened, bool hideBeforePromotion)
        => killerIsImpostor && targetRole == Roles.RoleId.MadKiller && (awakened || !hideBeforePromotion);
    internal static bool IsMadKillerTeammate(ExPlayerControl killer, ExPlayerControl target)
        => killer != null && target != null
            && IsMadKillerTeammate(killer.roleBase?.AssignedTeam == Roles.AssignedTeamType.Impostor, target.Role,
                target.GetAbility<MadKillerAbility>()?.IsAwakened == true, SideKiller.CannotSeeMadKillerBeforePromotion);
    public static ExPlayerControl SetImpostorTarget()
    {
        if (ExPlayerControl.LocalPlayer != null &&
            ExPlayerControl.LocalPlayer.TryGetAbility<RemoteControllerAbility>(out var remote) &&
            remote.UnderOperation &&
            remote.TargetPlayer != null)
        {
            return TargetCustomButtonBase.SetTarget(onlyCrewmates: true, targetingPlayer: remote.TargetPlayer.Player, isTargetable: ValidMadkiller);
        }
        return TargetCustomButtonBase.SetTarget(onlyCrewmates: true, isTargetable: ValidMadkiller);
    }
}
