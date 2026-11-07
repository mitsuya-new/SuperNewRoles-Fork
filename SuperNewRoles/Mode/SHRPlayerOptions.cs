using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Patches;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Impostor;

namespace SuperNewRoles.Mode;

public static class ShrPlayerOptions
{
    internal static bool IsVanillaGuest(PlayerControl player)
        => player != null && !player.AmOwner && player.Data != null && !player.Data.Disconnected
            && (!SyncVersion.VersionData.VersionMap.TryGetValue(player.PlayerId, out string version)
                || string.IsNullOrEmpty(version));

    public static float KillCooldown(ExPlayerControl player)
        => player.GetAbility<CustomKillButtonAbility>()?.DefaultTimer
            ?? GameManager.Instance.LogicOptions.currentGameOptions.GetFloat(FloatOptionNames.KillCooldown);

    public static float AbilityCooldown(ExPlayerControl player) => player.Role switch
    {
        RoleId.Jackal => player.GetAbility<JackalAbility>()?.JackData.SidekickCooldown ?? 0f,
        RoleId.SelfBomber => SelfBomber.SelfBomberCooldown,
        RoleId.Samurai => Samurai.SamuraiKillCooldown,
        _ => 0f,
    };

    public static void Send(PlayerControl player, float? abilityCooldown = null, float? killCooldown = null,
        bool resetAbility = false, bool resetKill = false)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || !IsVanillaGuest(player)) return;
        int id = client.GetClientIdFromCharacter(player);
        if (id < 0) return;
        var manager = GameManager.Instance;
        var factory = manager.LogicOptions.gameOptionsFactory;
        var options = factory.FromBytes(factory.ToBytes(manager.LogicOptions.currentGameOptions,
            AprilFoolsMode.IsAprilFoolsModeToggledOn));
        ExPlayerControl ex = player;
        float kill = UnityEngine.Mathf.Max(0.001f, killCooldown ?? KillCooldown(ex));
        options.SetFloat(FloatOptionNames.KillCooldown, resetKill ? kill * 2f : kill);
        if (ex.Role is RoleId.Jackal or RoleId.SelfBomber or RoleId.Samurai)
            options.SetFloat(FloatOptionNames.PhantomCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
        if (resetAbility) options.SetFloat(FloatOptionNames.ProtectionDurationSeconds, 0f);

        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            writer.StartMessage(Tags.GameDataTo);
            writer.Write(client.GameId);
            writer.WritePacked(id);
            writer.StartMessage(1);
            writer.WritePacked(manager.NetId);
            writer.StartMessage(4);
            writer.WriteBytesAndSize(factory.ToBytes(options, AprilFoolsMode.IsAprilFoolsModeToggledOn));
            writer.EndMessage();
            writer.EndMessage();
            writer.EndMessage();
            client.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }

        if (resetAbility)
        {
            var rpc = client.StartRpcImmediately(player.NetId, (byte)RpcCalls.ProtectPlayer, SendOption.Reliable, id);
            rpc.WriteNetObject(player);
            rpc.Write(0);
            client.FinishRpcImmediately(rpc);
        }
        if (resetKill)
        {
            var rpc = client.StartRpcImmediately(player.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, id);
            rpc.WriteNetObject(player);
            rpc.Write((int)(MurderResultFlags.DecisionByHost | MurderResultFlags.FailedProtected));
            client.FinishRpcImmediately(rpc);
        }
        if (resetAbility || resetKill) Send(player);
        Logger.Debug($"Options player={player.PlayerId} kill={kill} ability={abilityCooldown ?? AbilityCooldown(ex)} resetAbility={resetAbility} resetKill={resetKill}", "SHR.Controls");
    }
}
