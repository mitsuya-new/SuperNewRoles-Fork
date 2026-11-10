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
            SHRPets.GiveMissingPets();
            SHRKillRequests.Initialize();
            foreach (var player in ExPlayerControl.ExPlayerControls)
                if (SHRVanillaRpcRouter.CanAssignTasks(player)) player.AssignCustomTasks();
        });
        SuperNewRoles.Events.WrapUpEvent.Instance.AddListener(_ => SHRKillRequests.Initialize());
    }
    public override void OnGameEnd() => SHRPets.Restore();
    public override bool CheckWinCondition() => false;
}
