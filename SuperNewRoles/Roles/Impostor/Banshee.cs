using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Roles.Ability.CustomButton;
using SuperNewRoles;
using UnityEngine;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;

namespace SuperNewRoles.Roles.Impostor;

class Banshee : RoleBase<Banshee>
{
    public enum BansheeFairyRange
    {
        Short,
        Medium,
        Long,
    }
    public override RoleId Role { get; } = RoleId.Banshee;
    public override Color32 RoleColor { get; } = Palette.ImpostorRed;

    private static readonly Dictionary<BansheeFairyRange, float> BansheeFairyRangeValues = new()
    {
        { BansheeFairyRange.Short, 1f },
        { BansheeFairyRange.Medium, 1.8f },
        { BansheeFairyRange.Long, 2.5f },
    };

    public override List<Func<AbilityBase>> Abilities { get; } = new()
    {
        () => new BansheeAbility(
            releaseCooldown: BansheeReleaseCooldown,
            whisperCooldown: BansheeWhisperCooldown,
            fairyRange: BansheeFairyRangeValues.GetOrDefault(BansheeFairyRangeOption),
            canKillImpostor: BansheeCanKillImpostor,
            canDefaultKill: BansheeCanNormalKill
        ),
    };

    public override QuoteMod QuoteMod { get; } = QuoteMod.SuperNewRoles;
    public override RoleTypes IntroSoundType { get; } = RoleTypes.Impostor;
    public override short IntroNum { get; } = 1;

    public override AssignedTeamType AssignedTeam { get; } = AssignedTeamType.Impostor;
    public override WinnerTeamType WinnerTeam { get; } = WinnerTeamType.Impostor;
    public override TeamTag TeamTag { get; } = TeamTag.Impostor;
    public override RoleTag[] RoleTags { get; } = [RoleTag.SpecialKiller];
    public override RoleOptionMenuType OptionTeam { get; } = RoleOptionMenuType.Impostor;

    // オプション設定
    [CustomOptionFloat("BansheeReleaseCooldown", 2.5f, 60f, 2.5f, 25f)]
    public static float BansheeReleaseCooldown;

    [CustomOptionFloat("BansheeWhisperCooldown", 0f, 10f, 10f, 0f)]
    public static float BansheeWhisperCooldown;

    [CustomOptionSelect("BansheeFairyRange", typeof(BansheeFairyRange), "BansheeFairyRange.")]
    public static BansheeFairyRange BansheeFairyRangeOption;

    [CustomOptionBool("BansheeCanKillImpostor", false)]
    public static bool BansheeCanKillImpostor;

    [CustomOptionBool("BansheeCanNormalKill", false)]
    public static bool BansheeCanNormalKill;
}

public class BansheeAbility : AbilityBase
{
    /* Ability */
    private BansheeReleaseAbility bansheeReleaseAbility;
    private BansheeWhisperAbility bansheeWhisperAbility;
    private ShowPlayerUIAbility showPlayerUIAbility;
    private CustomKillButtonAbility customKillButtonAbility;

    /* クールタイム */
    private float releaseCooldown;
    private float whisperCooldown;
    private float fairyRange;
    private bool canKillImpostor;
    private bool canDefaultKill;

    /* データ */
    private ExPlayerControl currentFairyPlayer;
    private bool whisperTriggered = false;
    private readonly HashSet<byte> playersInRangeAlreadyChecked = new();
    private readonly List<ExPlayerControl> _uiPlayers = new(1);

    public BansheeAbility(float releaseCooldown, float whisperCooldown, float fairyRange, bool canKillImpostor, bool canDefaultKill)
    {
        this.canDefaultKill = canDefaultKill;
        this.releaseCooldown = releaseCooldown;
        this.whisperCooldown = whisperCooldown;
        this.fairyRange = fairyRange;
        this.canKillImpostor = canKillImpostor;
    }

    public override void AttachToAlls()
    {
        bansheeReleaseAbility = new BansheeReleaseAbility(
            releaseCooldown,
            () => currentFairyPlayer == null && !whisperTriggered,
            newFairyPlayer =>
            {
                if (Mode.ModeManager.IsMode(ModeId.SuperHostRoles)) RpcSHRFairy(newFairyPlayer, false);
                else { currentFairyPlayer = newFairyPlayer; whisperTriggered = false; }
            }
        );
        bansheeWhisperAbility = new BansheeWhisperAbility(
            whisperCooldown,
            () => currentFairyPlayer != null && !whisperTriggered,
            () =>
            {
                if (Mode.ModeManager.IsMode(ModeId.SuperHostRoles)) RpcSHRFairy(currentFairyPlayer, true);
                else whisperTriggered = true;
            }
        );
        showPlayerUIAbility = new ShowPlayerUIAbility(GetUiPlayers);
        customKillButtonAbility = new CustomKillButtonAbility(
            // 囁いた後は押せない
            () => canDefaultKill && !whisperTriggered,
            () => GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown),
            () => true,
            killedCallback: (player) =>
            {
                if (Mode.ModeManager.IsMode(ModeId.SuperHostRoles))
                {
                    if (AmongUsClient.Instance?.AmHost == true) RpcSHRReset(true, false);
                    return;
                }
                ResetStatus();
                bansheeReleaseAbility.ResetTimer();
                bansheeWhisperAbility.ResetTimer();
            });
        Player.AttachAbility(bansheeReleaseAbility, new AbilityParentAbility(this));
        Player.AttachAbility(bansheeWhisperAbility, new AbilityParentAbility(this));
        Player.AttachAbility(showPlayerUIAbility, new AbilityParentAbility(this));
        Player.AttachAbility(customKillButtonAbility, new AbilityParentAbility(this));
        if (AmongUsClient.Instance?.AmHost == true && Mode.ModeManager.IsMode(ModeId.SuperHostRoles) && !Player.AmOwner)
        {
            SubscribeWithAbility(FixedUpdateEvent.Instance, OnFixedUpdate);
            SubscribeWithAbility(MeetingStartEvent.Instance, OnStartMeeting);
            SubscribeWithAbility(MurderEvent.Instance, data =>
            {
                if (data.killer == Player && data.target != Player && data.resultFlags.HasFlag(MurderResultFlags.Succeeded))
                    RpcSHRReset(true, false);
            });
        }
    }

    public override void AttachToLocalPlayer()
    {
        base.AttachToLocalPlayer();

        playersInRangeAlreadyChecked.Clear();
        currentFairyPlayer = null;

        SubscribeWithAbility(FixedUpdateEvent.Instance, OnFixedUpdate);
        SubscribeWithAbility(MeetingStartEvent.Instance, OnStartMeeting);
    }

    private void OnStartMeeting(MeetingStartEventData data)
    {
        if (Mode.ModeManager.IsMode(ModeId.SuperHostRoles) && AmongUsClient.Instance?.AmHost != true) return;
        ResetSynchronized();
    }

    internal ExPlayerControl SHRFairy => currentFairyPlayer;
    internal bool SHRWhispering => whisperTriggered;

    [CustomRPC]
    public void RpcSHRFairy(ExPlayerControl target, bool whisper)
    {
        currentFairyPlayer = target;
        whisperTriggered = whisper;
        Mode.SHRRoleNameDisplay.Refresh();
    }

    [CustomRPC]
    public void RpcSHRReset(bool resetTimers, bool resetKill)
    {
        ResetStatus();
        if (resetTimers)
        {
            bansheeReleaseAbility.ResetTimer();
            bansheeWhisperAbility.ResetTimer();
            Player.GetAbility<Mode.SHRBansheeAbility>()?.ResetCooldowns();
        }
        if (resetKill)
        {
            Player.ResetKillCooldown();
            if (AmongUsClient.Instance?.AmHost == true && Player.IsAlive() && Mode.SHRPlayerOptions.IsVanillaGuest(Player.Player))
                Mode.SHRKillRequests.SetRemainingCooldown(Player.Player, Mode.SHRPlayerOptions.KillCooldown(Player));
        }
        Mode.SHRRoleNameDisplay.Refresh();
    }

    private void ResetSynchronized(bool resetKill = false)
    {
        if (Mode.ModeManager.IsMode(ModeId.SuperHostRoles) && AmongUsClient.Instance?.AmHost == true)
        {
            if (currentFairyPlayer != null || whisperTriggered || resetKill) RpcSHRReset(false, resetKill);
            else ResetStatus();
        }
        else ResetStatus();
    }
    private void ResetStatus()
    {
        currentFairyPlayer = null;
        whisperTriggered = false;
        playersInRangeAlreadyChecked.Clear();
        _uiPlayers.Clear();
    }

    private List<ExPlayerControl> GetUiPlayers()
    {
        if (currentFairyPlayer == null)
        {
            if (_uiPlayers.Count != 0)
                _uiPlayers.Clear();
            return _uiPlayers;
        }
        if (_uiPlayers.Count == 1 && _uiPlayers[0] == currentFairyPlayer)
            return _uiPlayers;
        _uiPlayers.Clear();
        _uiPlayers.Add(currentFairyPlayer);
        return _uiPlayers;
    }

    private void OnFixedUpdate()
    {
        bool hostSHR = Mode.ModeManager.IsMode(ModeId.SuperHostRoles);
        if (hostSHR && AmongUsClient.Instance?.AmHost != true) return;
        // 役職者が死亡・切断した後は、残留したイベントリスナーから囁きキルを発生させない。
        if (Player == null || Player.IsDead() || Player.Data.Disconnected)
        {
            ResetSynchronized();
            return;
        }

        if (currentFairyPlayer == null) return;

        // 妖精がついたプレイヤーが死んだらリセット
        if (currentFairyPlayer.IsDead() || currentFairyPlayer.Data.Disconnected)
        {
            ResetSynchronized();
            return;
        }

        var fairyBefore = currentFairyPlayer;
        foreach (var player in ExPlayerControl.ExPlayerControls)
        {
            if (player.IsDead() || player.Data.Disconnected) continue;
            if (player == Player) continue;
            if (player == currentFairyPlayer) continue;
            if (ModHelpers.IsPositionDistance(currentFairyPlayer.transform.position, player.transform.position, fairyRange))
            {
                if (playersInRangeAlreadyChecked.Contains(player.PlayerId)) continue;
                if (whisperTriggered)
                {
                    playersInRangeAlreadyChecked.Add(player.PlayerId);
                    if (player.IsImpostor() && !canKillImpostor) continue;
                    Player.RpcCustomDeath(player, CustomDeathType.BansheeWhisper);
                    if (hostSHR) ResetSynchronized(resetKill: true);
                    else { Player.ResetKillCooldown(); ResetStatus(); }
                    break;
                }
                currentFairyPlayer = player;
                playersInRangeAlreadyChecked.Add(player.PlayerId);
            }
            else
            {
                playersInRangeAlreadyChecked.Remove(player.PlayerId);
            }
        }
        if (hostSHR && currentFairyPlayer != null && fairyBefore != currentFairyPlayer)
            RpcSHRFairy(currentFairyPlayer, whisperTriggered);
    }
}

public class BansheeReleaseAbility : TargetCustomButtonBase
{
    public override float DefaultTimer { get; }
    public override string buttonText => ModTranslation.GetString("BansheeReleaseButtonText");
    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("BansheeReleaseButton.png");
    protected override KeyType keytype => KeyType.Ability1;
    public override Color32 OutlineColor => Palette.ImpostorRed;
    public override bool OnlyCrewmates => false;

    private Func<bool> CanReleaseFunc;
    private Action<ExPlayerControl> SetFairyPlayer;

    public BansheeReleaseAbility(float cooldown, Func<bool> canReleaseFunc, Action<ExPlayerControl> setFairyPlayer)
    {
        DefaultTimer = cooldown;
        CanReleaseFunc = canReleaseFunc;
        SetFairyPlayer = setFairyPlayer;
    }

    public override bool CheckHasButton()
    {
        return base.CheckHasButton() && CanReleaseFunc();
    }

    public override bool CheckIsAvailable()
    {
        return Target != null && Target.IsAlive();
    }

    public override void OnClick()
    {
        // ターゲットに妖精を付ける処理
        SetFairyPlayer(Target);
        Logger.Info($"Banshee released fairy to {Target.PlayerId}");
    }
}

public class BansheeWhisperAbility : CustomButtonBase
{
    public override float DefaultTimer { get; }
    public override string buttonText => ModTranslation.GetString("BansheeWhisperButtonText");
    public override Sprite Sprite => AssetManager.GetAsset<Sprite>("BansheeWhisperButton.png");
    protected override KeyType keytype => KeyType.Ability1;

    private Func<bool> CanWhisperFunc;
    private Action TriggerWhisperAction;

    public BansheeWhisperAbility(float cooldown, Func<bool> canWhisperFunc, Action triggerWhisper)
    {
        DefaultTimer = cooldown;
        CanWhisperFunc = canWhisperFunc;
        TriggerWhisperAction = triggerWhisper;
    }

    public override bool CheckHasButton()
    {
        return base.CheckHasButton() && CanWhisperFunc();
    }

    public override bool CheckIsAvailable()
    {
        return true;
    }

    public override void OnClick()
    {
        // 妖精の位置関係でキル処理
        TriggerWhisperAction();
        Logger.Info("Banshee whispered");
    }
}
