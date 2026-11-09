using System;
using System.Collections.Generic;
using UnityEngine;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Events;
using SuperNewRoles.CustomOptions.Categories;

namespace SuperNewRoles.Roles.Impostor;

class VampireDependent : RoleBase<VampireDependent>
{
    public override RoleId Role => RoleId.VampireDependent;
    public override Color32 RoleColor => Palette.ImpostorRed;
    public override List<Func<AbilityBase>> Abilities { get; } = [() => new VampireDependentAbility(
        data: new VampireDependentData(
            killCooldown: Vampire.VampireDependentKillCooldown,
            canUseVent: Vampire.VampireDependentCanUseVent
        ),
        vampire: new VampireData(
            vampireInvisibleOnAdmin: Vampire.VampireInvisibleOnAdmin,
            vampireCannotFixSabotage: Vampire.VampireCannotFixSabotage,
            vampireCannotUseDevice: Vampire.VampireCannotUseDevice,
            vampireDependentHasReverseVision: Vampire.VampireDependentHasReverseVision,
            vampireDependentHasImpostorVisionInLightsoff: Vampire.VampireDependentHasImpostorVisionInLightsoff,
            vampireNoDeathOnVitals: Vampire.VampireNoDeathOnVitals
        )
    )];

    public override QuoteMod QuoteMod => QuoteMod.SuperNewRoles;
    public override RoleTypes IntroSoundType => RoleTypes.Shapeshifter;
    public override short IntroNum => 1;

    public override AssignedTeamType AssignedTeam => AssignedTeamType.Impostor;
    public override WinnerTeamType WinnerTeam => WinnerTeamType.Impostor;
    public override TeamTag TeamTag => TeamTag.Impostor;
    public override RoleTag[] RoleTags => [RoleTag.ImpostorTeam];
    public override RoleOptionMenuType OptionTeam => RoleOptionMenuType.Hidden;
    public override bool HideInRoleDictionary => true; // 役職辞典で非表示にする
}
public record VampireDependentData(float killCooldown, bool canUseVent);
public class VampireDependentAbility : AbilityBase
{
    public VampireAbility vampire { get; private set; }
    public VampireData VampireData { get; }
    public CustomVentAbility ventAbility;
    private CustomKillButtonAbility killButtonAbility;
    public VampireDependentData Data { get; }
    private SabotageCanUseAbility sabotageCanUseAbility;
    private DeviceCanUseAbility deviceCanUseAbility;
    private HideInAdminAbility hideInAdminAbility;
    private ReverseVisionAbility reverseVisionAbility;

    public VampireDependentAbility(VampireDependentData data, VampireData vampire)
    {
        this.Data = data;
        this.VampireData = vampire;
    }

    public override void AttachToAlls()
    {
        base.AttachToAlls();
        killButtonAbility = new CustomKillButtonAbility(
            canKill: () => true,
            killCooldown: () => Data.killCooldown,
            onlyCrewmates: () => true,
            isTargetable: (player) => player != vampire?.Player
        );
        ventAbility = new CustomVentAbility(() => Data.canUseVent);
        sabotageCanUseAbility = new SabotageCanUseAbility(
            () => VampireData.vampireCannotFixSabotage ? SabotageType.Lights : SabotageType.None
        );
        deviceCanUseAbility = new DeviceCanUseAbility(
            () => VampireData.vampireCannotUseDevice ? DeviceTypeFlag.All : DeviceTypeFlag.None
        );
        hideInAdminAbility = new HideInAdminAbility(
            () => VampireData.vampireInvisibleOnAdmin
        );
        reverseVisionAbility = new ReverseVisionAbility(
            () => VampireData.vampireDependentHasReverseVision,
            () => VampireData.vampireDependentHasImpostorVisionInLightsoff
        );

        Player.AttachAbility(killButtonAbility, new AbilityParentAbility(this));
        Player.AttachAbility(ventAbility, new AbilityParentAbility(this));
        Player.AttachAbility(sabotageCanUseAbility, new AbilityParentAbility(this));
        Player.AttachAbility(deviceCanUseAbility, new AbilityParentAbility(this));
        Player.AttachAbility(hideInAdminAbility, new AbilityParentAbility(this));
        Player.AttachAbility(reverseVisionAbility, new AbilityParentAbility(this));
        Player.AttachAbility(new KnowOtherAbility((player) => player.Player == vampire?.Player, () => true), new AbilityParentAbility(this));
        if (SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles) && AmongUsClient.Instance.AmHost)
            SubscribeWithAbility(FixedUpdateEvent.Instance, () =>
            {
                if (Player.IsAlive() && vampire?.Player is { } owner && (owner.IsDead() || owner.Data.Disconnected))
                    Player.RpcCustomDeath(MeetingHud.Instance != null || ExileController.Instance != null
                        ? CustomDeathType.VampireWithDeadNonDeadbody : CustomDeathType.VampireWithDead);
            });
    }

    public override void AttachToLocalPlayer()
    {
        base.AttachToLocalPlayer();
        if (!SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles) || AmongUsClient.Instance.AmHost)
        {
            SubscribeWithAbility(MurderEvent.Instance, OnMurder);
            SubscribeWithAbility(ExileEvent.Instance, OnExile);
        }
        SubscribeWithAbility(NameTextUpdateEvent.Instance, OnNameTextUpdate);
    }

    public override void AttachToOthers()
    {
        if (!SuperNewRoles.Mode.ModeManager.IsMode(ModeId.SuperHostRoles) || AmongUsClient.Instance?.AmHost != true) return;
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);
        SubscribeWithAbility(ExileEvent.Instance, OnExile);
    }

    private void OnNameTextUpdate(NameTextUpdateEventData data)
    {
        // 親ヴァンパイアの名前に印を付ける
        if (vampire?.Player == data.Player)
        {
            NameText.SetNameTextColor(data.Player, Palette.ImpostorRed, true);
        }
    }

    private void OnMurder(MurderEventData data)
    {
        if (data.target == vampire?.Player && Player.IsAlive() && data.resultFlags.HasFlag(MurderResultFlags.Succeeded))
            Player.RpcCustomDeath(CustomDeathType.VampireWithDead);
    }

    private void OnExile(ExileEventData data)
    {
        if (data.exiled == vampire?.Player && Player.IsAlive())
            Player.RpcCustomDeath(CustomDeathType.VampireWithDeadNonDeadbody);
    }

    public void SetVampire(VampireAbility vampire)
    {
        this.vampire = vampire;
    }
}
