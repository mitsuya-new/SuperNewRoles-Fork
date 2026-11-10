using AmongUs.GameOptions;
using System;
using AmongUs.InnerNet.GameDataMessages;
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
    internal static uint KillResetTarget(uint attackerNetId, uint? protectedTargetNetId)
        => protectedTargetNetId ?? attackerNetId;

    internal static void WriteResetSequence(bool resetKill, bool resetAbility, RoleTypes? controlRole,
        Action writeOptions, Action<RoleTypes> writeRole, Action writeKillReset, Action writeAbilityReset, Action restoreOptions)
    {
        writeOptions();
        if (controlRole.HasValue)
        {
            writeRole(RoleTypes.Impostor);
            if (controlRole.Value != RoleTypes.Impostor) writeRole(controlRole.Value);
        }
        if (resetKill) writeKillReset();
        if (resetAbility) writeAbilityReset();
        if (resetAbility || resetKill) restoreOptions();
    }

    internal static bool IsVanillaGuest(PlayerControl player)
        => player != null && !player.AmOwner && player.Data != null && !player.Data.Disconnected
            && (!SyncVersion.VersionData.VersionMap.TryGetValue(player.PlayerId, out string version)
                || string.IsNullOrEmpty(version));

    public static float KillCooldown(ExPlayerControl player)
        => player.Role == RoleId.Chief ? SHRCrewRoleAbility.Cooldown(player.Role)
            : player.Role == RoleId.Rocket ? 0.001f
            : player.Role == RoleId.DoubleKiller ? DoubleKiller.DoubleKillerMainKillCooldown
            : player.Role == RoleId.DarkKiller ? DarkKillerCooldown(ModHelpers.IsElectrical(),
                GameManager.Instance.LogicOptions.currentGameOptions.GetFloat(FloatOptionNames.KillCooldown), DarkKiller.DarkKillerKillCoolTime)
            : player.GetAbility<DoppelgangerAbility>()?.SHRKillCooldown
            ?? player.GetAbility<EvilGamblerAbility>()?.SHRKillCooldown
            ?? player.GetAbility<CustomKillButtonAbility>()?.DefaultTimer
            ?? GameManager.Instance.LogicOptions.currentGameOptions.GetFloat(FloatOptionNames.KillCooldown);

    internal static float DarkKillerCooldown(bool electrical, float vanillaCooldown, float darkCooldown)
        => electrical ? darkCooldown : vanillaCooldown;

    public static float AbilityCooldown(ExPlayerControl player) => player.Role switch
    {
        RoleId.EvilHacker => player.GetAbility<SHREvilHackerAbility>()?.Remaining ?? EvilHacker.EvilHackerCreateMadmateCooldown,
        RoleId.Pusher => player.GetAbility<SHRPusherAbility>()?.Remaining ?? Pusher.PusherCooldown,
        RoleId.Rocket => player.GetAbility<SHRRocketAbility>()?.Remaining ?? Rocket.RocketInitialGrabCooldown,
        RoleId.Banshee => player.GetAbility<SHRBansheeAbility>()?.Remaining ?? Banshee.BansheeReleaseCooldown,
        RoleId.Conjurer => player.GetAbility<SHRConjurerAbility>()?.Remaining ?? Conjurer.ConjurerBeaconCooldown,
        _ when SHRCrewActionAbility.Supports(player.Role) => player.GetAbility<SHRCrewActionAbility>()?.Remaining ?? 0f,
        _ when SHRCrewBoostAbility.Supports(player.Role) => player.GetAbility<SHRCrewBoostAbility>()?.Remaining ?? 0f,
        _ when SHRCrewRoleAbility.Supports(player.Role) => player.GetAbility<SHRCrewRoleAbility>()?.RemainingCooldown
            ?? SHRCrewRoleAbility.Cooldown(player.Role),
        _ when SHRBatchRoleAbility.Supports(player.Role) => player.GetAbility<SHRBatchRoleAbility>()?.RemainingCooldown
            ?? SHRBatchRoleAbility.DefaultCooldown(player.Role),
        RoleId.Jackal => player.GetAbility<JackalAbility>()?.JackData.SidekickCooldown ?? 0f,
        RoleId.SelfBomber => SelfBomber.SelfBomberCooldown,
        RoleId.Samurai => Samurai.SamuraiKillCooldown,
        RoleId.WaveCannon => WaveCannon.WaveCannonCooldown,
        RoleId.SerialKiller => player.GetAbility<SuicideTimerAbility>()?.NativeCooldown ?? 0f,
        RoleId.SideKiller => SideKiller.SideKillerKillCooldown,
        RoleId.Penguin => player.GetAbility<PenguinAbility>()?.SHRAbilityCooldown ?? Penguin.PenguinCooldown,
        RoleId.SuicideWisher => SuicideWisher.SuicideWisherCoolTime,
        RoleId.Doppelganger => Doppelganger.DoppelgangerCoolTime,
        RoleId.Camouflager => player.GetAbility<CamouflagerAbility>()?.SHRController?.RemainingCooldown ?? Camouflager.CamouflagerCoolTime,
        _ => 0f,
    };

    public static void Send(PlayerControl player, float? abilityCooldown = null, float? killCooldown = null,
        bool resetAbility = false, bool resetKill = false, float? playerSpeed = null, RoleTypes? controlRole = null,
        PlayerControl protectedTarget = null)
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
        if (SHRBalancerVotes.NativeVotingTime is { } votingTime) options.SetInt(Int32OptionNames.VotingTime, votingTime);
        if (ex.GetAbility<KnowVoteAbility>() is { } vote) options.SetBool(BoolOptionNames.AnonymousVotes, vote.IsAnonymousVotes());
        if (SHRCrewRoleAbility.HasCrewImpostorControls(ex.Role) && ShipStatus.Instance != null)
        {
            float radius = UnityEngine.Mathf.Lerp(ShipStatus.Instance.MinLightRadius, ShipStatus.Instance.MaxLightRadius,
                SHRMadKillerSupport.LightFraction());
            options.SetFloat(FloatOptionNames.ImpostorLightMod, options.GetFloat(FloatOptionNames.CrewLightMod)
                * radius / UnityEngine.Mathf.Max(0.001f, ShipStatus.Instance.MaxLightRadius));
        }
        if (ex.Role is RoleId.Technician or RoleId.SilverBullet)
        {
            options.SetFloat(FloatOptionNames.EngineerCooldown, 0.001f);
            options.SetFloat(FloatOptionNames.EngineerInVentMaxTime, 0f);
        }
        if (ex.Role == RoleId.ToiletFan || SHRCrewBoostAbility.UsesEngineer(ex.Role) || SHRCrewActionAbility.UsesEngineer(ex.Role))
        {
            options.SetFloat(FloatOptionNames.EngineerCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
            options.SetFloat(FloatOptionNames.EngineerInVentMaxTime, 0f);
        }
        if (ex.Role == RoleId.VampireDependent && Vampire.VampireDependentHasReverseVision && ShipStatus.Instance != null)
        {
            float radius = SHRVampireRestrictions.DependentVisionRadius(SHRMadKillerSupport.LightFraction(),
                ShipStatus.Instance.MinLightRadius, ShipStatus.Instance.MaxLightRadius,
                options.GetFloat(FloatOptionNames.CrewLightMod), options.GetFloat(FloatOptionNames.ImpostorLightMod),
                Vampire.VampireDependentHasImpostorVisionInLightsoff);
            options.SetFloat(FloatOptionNames.ImpostorLightMod, radius / UnityEngine.Mathf.Max(0.001f, ShipStatus.Instance.MaxLightRadius));
        }
        bool taskerTracker = ex.Role == RoleId.Tasker && Tasker.TaskerCanKill
            && ex.GetAbility<SHRTestRoleAbility>()?.TaskerImpostor != true;
        if (taskerTracker)
        {
            options.SetFloat(FloatOptionNames.TrackerCooldown,
                UnityEngine.Mathf.Max(0.001f, killCooldown ?? SHRKillRequests.RemainingCooldown(player)));
            options.SetFloat(FloatOptionNames.TrackerDuration, 0f);
            if (resetKill) resetAbility = true;
        }
        if ((ex.Role == RoleId.MadKiller || ex.GetAbility<SHRHackerMadmateAbility>() != null) && ShipStatus.Instance != null)
        {
            float light = SHRMadKillerSupport.LightFraction();
            float radius = UnityEngine.Mathf.Lerp(ShipStatus.Instance.MinLightRadius, ShipStatus.Instance.MaxLightRadius, light);
            bool impostorVision = ex.HasImpostorVision();
            if (impostorVision)
                options.SetFloat(FloatOptionNames.CrewLightMod, SHRHackerMadmateAbility.CrewVision(true,
                    options.GetFloat(FloatOptionNames.CrewLightMod), options.GetFloat(FloatOptionNames.ImpostorLightMod),
                    ShipStatus.Instance.MinLightRadius, ShipStatus.Instance.MaxLightRadius, light));
            else
                options.SetFloat(FloatOptionNames.ImpostorLightMod, options.GetFloat(FloatOptionNames.CrewLightMod)
                    * radius / UnityEngine.Mathf.Max(0.001f, ShipStatus.Instance.MaxLightRadius));
            options.SetFloat(FloatOptionNames.EngineerCooldown, 0.001f);
            options.SetFloat(FloatOptionNames.EngineerInVentMaxTime, 0f);
        }
        if (ex.GetAbility<WaveCannonAbility>()?.SHRController?.Busy == true) playerSpeed = 0.0001f;
        if (ex.GetAbility<SHRCrewBoostAbility>()?.Active == true)
        {
            if (ex.Role is RoleId.SpeedBooster or RoleId.EvilSpeedBooster)
                playerSpeed = options.GetFloat(FloatOptionNames.PlayerSpeedMod) * (ex.Role == RoleId.SpeedBooster
                    ? SuperNewRoles.Roles.Crewmate.SpeedBooster.SpeedBoosterMultiplier : EvilSpeedBooster.EvilSpeedBoosterMultiplier);
            if (ex.Role == RoleId.WiseMan) playerSpeed = 0.0001f;
            if (ex.Role == RoleId.Lighter && ShipStatus.Instance != null)
                options.SetFloat(FloatOptionNames.CrewLightMod, SHRCrewBoostAbility.LighterCrewVision(
                    SuperNewRoles.Roles.Crewmate.Lighter.LighterRadius, ShipStatus.Instance.MinLightRadius,
                    ShipStatus.Instance.MaxLightRadius, SHRMadKillerSupport.LightFraction()));
        }
        if (PenguinAbility.IsDragged(ex) || SHRNunPlatform.IsPassenger(ex) || SHRFlightSync.IsFlying(ex)
            || ex.GetAbility<SHREvilHackerAbility>()?.FreezesMovement == true) playerSpeed = 0.0001f;
        if (playerSpeed.HasValue) options.SetFloat(FloatOptionNames.PlayerSpeedMod, playerSpeed.Value);
        float clergyVision = SHRClergyVision.ForPlayer(ex);
        if (clergyVision != 1f)
        {
            options.SetFloat(FloatOptionNames.CrewLightMod, options.GetFloat(FloatOptionNames.CrewLightMod) * clergyVision);
            options.SetFloat(FloatOptionNames.ImpostorLightMod, options.GetFloat(FloatOptionNames.ImpostorLightMod) * clergyVision);
        }
        float kill = UnityEngine.Mathf.Max(0.001f, killCooldown ?? KillCooldown(ex));
        options.SetFloat(FloatOptionNames.KillCooldown, resetKill ? kill * 2f : kill);
        if (SHRBatchRoleAbility.HasPhantom(ex.Role) || ex.Role is RoleId.Doorr or RoleId.EvilSpeedBooster or RoleId.EvilScientist or RoleId.Rocket or RoleId.Pusher or RoleId.Nun)
            options.SetFloat(FloatOptionNames.PhantomCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
        if (ex.Role == RoleId.ShiftActor)
        {
            options.SetFloat(FloatOptionNames.ShapeshifterCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
            options.SetFloat(FloatOptionNames.ShapeshifterDuration, ShiftActor.ShiftActorDuration);
        }
        if (ex.Role is RoleId.Conjurer or RoleId.Banshee or RoleId.EvilHacker)
            options.SetFloat(FloatOptionNames.ShapeshifterCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
        if (ex.Role == RoleId.Doppelganger)
        {
            options.SetFloat(FloatOptionNames.ShapeshifterCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
            options.SetFloat(FloatOptionNames.ShapeshifterDuration, Doppelganger.DoppelgangerDurationTime);
        }
        if (ex.Role is RoleId.Jackal or RoleId.SelfBomber or RoleId.Samurai or RoleId.WaveCannon or RoleId.SideKiller or RoleId.Penguin or RoleId.SuicideWisher or RoleId.Camouflager)
            options.SetFloat(FloatOptionNames.PhantomCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
        if (ex.Role == RoleId.SerialKiller)
            options.SetFloat(FloatOptionNames.ShapeshifterCooldown, UnityEngine.Mathf.Max(0.001f, abilityCooldown ?? AbilityCooldown(ex)));
        float protectionDuration = options.GetFloat(FloatOptionNames.ProtectionDurationSeconds);
        if (resetAbility) options.SetFloat(FloatOptionNames.ProtectionDurationSeconds, 0f);

        int logicIndex = -1;
        for (int i = 0; i < manager.LogicComponents.Count; i++)
            if (manager.LogicComponents[i]?.TryCast<LogicOptions>() != null) { logicIndex = i; break; }
        if (logicIndex < 0) return;

        var writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            writer.StartMessage(Tags.GameDataTo);
            writer.Write(client.GameId);
            writer.WritePacked(id);
            WriteResetSequence(resetKill, resetAbility, controlRole, WriteOptions, WriteRole, () =>
            {
                StartRpc(RpcCalls.MurderPlayer);
                writer.WritePacked(KillResetTarget(player.NetId, protectedTarget?.NetId));
                writer.Write((int)(MurderResultFlags.DecisionByHost | MurderResultFlags.FailedProtected));
                writer.EndMessage();
            }, () =>
            {
                StartRpc(RpcCalls.ProtectPlayer);
                writer.WriteNetObject(player);
                writer.Write(0);
                writer.EndMessage();
            }, () =>
            {
                options.SetFloat(FloatOptionNames.KillCooldown, UnityEngine.Mathf.Max(0.001f, KillCooldown(ex)));
                options.SetFloat(FloatOptionNames.ProtectionDurationSeconds, protectionDuration);
                WriteOptions();
            });
            writer.EndMessage();
            client.SendOrDisconnect(writer);

            void WriteOptions()
            {
                writer.StartMessage(1);
                writer.WritePacked(manager.NetId);
                writer.StartMessage((byte)logicIndex);
                writer.WriteBytesAndSize(factory.ToBytes(options, AprilFoolsMode.IsAprilFoolsModeToggledOn));
                writer.EndMessage();
                writer.EndMessage();
            }
            void StartRpc(RpcCalls call)
            {
                writer.StartMessage((byte)GameDataTypes.RpcFlag);
                writer.WritePacked(player.NetId);
                writer.Write((byte)call);
            }
            void WriteRole(RoleTypes role)
            {
                StartRpc(RpcCalls.SetRole);
                new RpcSetRoleMessage(player.NetId, role, true).SerializeRpcValues(writer);
                writer.EndMessage();
            }
        }
        finally { writer.Recycle(); }
        Logger.Debug($"Options player={player.PlayerId} kill={kill} ability={abilityCooldown ?? AbilityCooldown(ex)} resetAbility={resetAbility} resetKill={resetKill}", "SHR.Controls");
    }
}
