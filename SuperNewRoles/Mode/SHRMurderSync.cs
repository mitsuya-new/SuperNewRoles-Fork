using Hazel;
using InnerNet;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;

namespace SuperNewRoles.Mode;

public static class SHRMurderSync
{
    internal static bool UsesVanillaResult(ModeId mode, bool isHost)
        => mode == ModeId.SuperHostRoles && isHost;

    internal static MurderResultFlags ResultFlags(bool protectedByGuardian)
        => MurderResultFlags.DecisionByHost | (protectedByGuardian
            ? MurderResultFlags.FailedProtected : MurderResultFlags.Succeeded);

    public static void Murder(ExPlayerControl source, ExPlayerControl target, bool explosion = false, FinalStatus? finalStatus = null)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !UsesVanillaResult(CustomOptions.Categories.Categories.ModeOption, client.AmHost)
            || source?.Player == null || target?.Player == null || target.Data == null
            || target.Data.Disconnected || target.Data.IsDead) return;

        bool suicide = source == target;
        if (!suicide && !explosion && !finalStatus.HasValue
            && Patches.SetTargetPatch.IsMadKillerTeammate(source, target)) return;
        if (SHRWaveCannonController.BlocksAttack(SHRWaveCannonController.IsProtected(target), suicide, CustomDeathType.Kill)) return;
        if (!suicide && !TryKillEvent.Invoke(source, ref target).RefSuccess) return;
        if (target?.Player == null || target.Data == null || target.Data.Disconnected || target.Data.IsDead) return;
        if (source != target && !explosion && !finalStatus.HasValue
            && Patches.SetTargetPatch.IsMadKillerTeammate(source, target)) return;
        if (SHRWaveCannonController.BlocksAttack(SHRWaveCannonController.IsProtected(target), source == target, CustomDeathType.Kill)) return;
        var flags = ResultFlags(!suicide && target.Player.protectedByGuardianThisRound);
        PlayerControl actor = explosion ? target.Player : source.Player;
        var writer = client.StartRpcImmediately(actor.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, -1);
        writer.WriteNetObject(target.Player);
        writer.Write((int)flags);
        client.FinishRpcImmediately(writer);
        actor.MurderPlayer(target.Player, flags);
        Logger.Debug($"Result source={source.PlayerId} target={target.PlayerId} flags={flags} explosion={explosion}", "SHR.Murder");
        if (!flags.HasFlag(MurderResultFlags.Succeeded)) return;
        FinalStatusManager.SetFinalStatus(target, finalStatus ?? (explosion
            ? (suicide ? FinalStatus.SelfBomb : FinalStatus.BombBySelfBomb) : FinalStatus.Kill));
        if (!suicide) MurderDataManager.AddMurderData(source, target);
    }
}
