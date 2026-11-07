using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using SuperNewRoles.Modules;
using SuperNewRoles.Patches;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Impostor;

namespace SuperNewRoles.Mode;

public static class SHRPlayerOptions
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
        RoleId.WaveCannon => WaveCannon.WaveCannonCooldown,
        RoleId.SerialKiller => player.GetAbility<SuicideTimerAbility>()?.NativeCooldown ?? 0f,
        _ => 0f,
    };

    public static void Send(PlayerControl player, float? abilityCooldown = null, float? killCooldown = null,
        bool resetAbility = false, bool resetKill = false, float? playerSpeed = null)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !ModeManager.IsMode(ModeId.SuperHostRoles)
            || !IsVanillaGuest(player)) return;
        int id = client.GetClientIdFromCharacter(player);
        if (id < 0) return;
        var manager = GameManager.Instance;
        if (manager?.LogicOptions?.currentGameOptions == null) return;
        var factory = manager.LogicOptions.gameOptionsFactory;
        var options = factory.FromBytes(factory.ToBytes(manager.LogicOptions.currentGameOptions,
            AprilFoolsMode.IsAprilFoolsModeToggledOn));
        ExPlayerControl ex = player;
        if (ex.GetAbility<WaveCannonAbility>()?.SHRController?.Busy == true) playerSpeed = 0.0001f;
        if (playerSpeed.HasValue) options.SetFloat(FloatOptionNames.PlayerSpeedMod, playerSpeed.Value);
        float kill = UnityEngine.Mathf.Max(0.001f, killCooldown ?? KillCooldown(ex));
        options.SetFloat(FloatOptionNames.KillCooldown, resetKill ? kill * 2f : kill);
        if (ex.Role is RoleId.Jackal or RoleId.SelfBomber or RoleId.Samurai or RoleId.WaveCannon)
            options.SetFloat(FloatOptionNames.PhantomCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
        if (ex.Role == RoleId.SerialKiller)
            options.SetFloat(FloatOptionNames.ShapeshifterCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
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
        if (resetAbility || resetKill) Send(player, playerSpeed: playerSpeed);
        Logger.Debug($"Options player={player.PlayerId} kill={kill} ability={abilityCooldown ?? AbilityCooldown(ex)} resetAbility={resetAbility} resetKill={resetKill}", "SHR.Controls");
    }
}
