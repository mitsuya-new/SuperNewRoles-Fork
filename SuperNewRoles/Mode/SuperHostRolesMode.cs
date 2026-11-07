using SuperNewRoles.Modules;
using UnityEngine;

namespace SuperNewRoles.Mode;

public sealed class SuperHostRolesMode : ModeBase<SuperHostRolesMode>
{
    public override ModeId Mode => ModeId.SuperHostRoles;
    public override string ModeName => "SuperHostRoles";
    public override Color32 ModeColor => new(255, 165, 0, 255);
    public override void OnGameStart()
    {
        Logger.Info("SuperHostRoles: host-side custom assignment and private role labels enabled.");
        SuperNewRoles.Events.IntroCutsceneInitializeEvent.Instance.AddListener(() =>
        {
            ShrKillRequests.Initialize();
            foreach (var player in ExPlayerControl.ExPlayerControls)
                if (ShrVanillaRpcRouter.CanAssignTasks(player)) player.AssignCustomTasks();
        });
        SuperNewRoles.Events.WrapUpEvent.Instance.AddListener(_ => ShrKillRequests.Initialize());
    }
    public override void OnGameEnd() { }
    public override bool CheckWinCondition() => false;
}
