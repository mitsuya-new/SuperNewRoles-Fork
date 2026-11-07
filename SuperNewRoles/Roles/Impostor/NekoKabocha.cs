using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Modules;
using SuperNewRoles.Events;
using SuperNewRoles.Events.PCEvents;
using UnityEngine;
using SuperNewRoles.Roles.Ability;
using System.Linq;
using SuperNewRoles.Mode;

namespace SuperNewRoles.Roles.Impostor;

class NekoKabocha : RoleBase<NekoKabocha>
{
    public override RoleId Role { get; } = RoleId.NekoKabocha;
    public override Color32 RoleColor { get; } = Palette.ImpostorRed;
    public override List<Func<AbilityBase>> Abilities { get; } = [() => new NekoKabochaRevenge(
        new(NekoKabochaCanRevengeCrewmate,
            NekoKabochaCanRevengeNeutral,
            NekoKabochaCanRevengeImpostor,
            NekoKabochaCanRevengeExiled))
        ];

    public override QuoteMod QuoteMod { get; } = QuoteMod.TheOtherRolesGM;
    public override RoleTypes IntroSoundType { get; } = RoleTypes.Impostor;
    public override short IntroNum { get; } = 1;

    public override AssignedTeamType AssignedTeam { get; } = AssignedTeamType.Impostor;
    public override WinnerTeamType WinnerTeam { get; } = WinnerTeamType.Impostor;
    public override TeamTag TeamTag { get; } = TeamTag.Impostor;
    public override RoleTag[] RoleTags { get; } = [RoleTag.SpecialKiller];
    public override RoleOptionMenuType OptionTeam { get; } = RoleOptionMenuType.Impostor;

    [CustomOptionBool("NekoKabocha.CanRevengeCrewmate", true)]
    public static bool NekoKabochaCanRevengeCrewmate;

    [CustomOptionBool("NekoKabocha.CanRevengeNeutral", true)]
    public static bool NekoKabochaCanRevengeNeutral;

    [CustomOptionBool("NekoKabocha.CanRevengeImpostor", true)]
    public static bool NekoKabochaCanRevengeImpostor;

    [CustomOptionBool("NekoKabocha.CanRevengeExiled", true)]
    public static bool NekoKabochaCanRevengeExiled;
}

public record NekoKabochaData(bool CanRevengeCrewmate, bool CanRevengeNeutral, bool CanRevengeImpostor, bool CanRevengeExiled);

public class NekoKabochaRevenge : AbilityBase
{
    private bool revenged;
    private bool IsSHR => ModeManager.IsMode(ModeId.SuperHostRoles);

    public NekoKabochaData Data { get; }

    public NekoKabochaRevenge(NekoKabochaData data)
    {
        Data = data;
    }

    public override void AttachToLocalPlayer()
    {
        if (IsSHR && AmongUsClient.Instance?.AmHost != true) return;
        // 殺害イベントリスナーの登録
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);

        // 会議終了/追放イベントリスナーの登録
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
    }

    public override void AttachToOthers()
    {
        if (!IsSHR || AmongUsClient.Instance?.AmHost != true) return;
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);
        SubscribeWithAbility(WrapUpEvent.Instance, OnWrapUp);
    }

    internal static bool CanRevenge(NekoKabochaData settings, bool crewmate, bool neutral, bool impostor)
        => (crewmate && settings.CanRevengeCrewmate) || (neutral && settings.CanRevengeNeutral)
            || (impostor && settings.CanRevengeImpostor);

    internal void OnSHRSpecialMurder(ExPlayerControl killer, MurderResultFlags flags)
        => OnMurder(new MurderEventData(killer, Player, flags));

    private void OnMurder(MurderEventData data)
    {
        if (data.target == null || data.killer == null) return;
        if (data.target != Player) return;
        if (!data.resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;
        // 無限ループ防止
        if (data.killer == data.target) return;
        if (IsSHR)
        {
            if (revenged || !data.killer.IsAlive() || data.killer.Data.Disconnected) return;
            if (!CanRevenge(Data, data.killer.IsCrewmate(), data.killer.IsNeutral(), data.killer.IsImpostor())) return;
            revenged = true;
            SHRMurderSync.Murder(Player, data.killer, finalStatus: FinalStatus.Kill);
            return;
        }

        bool canRevenge = false;
        ExPlayerControl killer = data.killer;

        // 殺害したプレイヤーのタイプに基づいて復讐できるかどうかを判断
        if (killer.IsCrewmate() && Data.CanRevengeCrewmate)
            canRevenge = true;
        else if (killer.IsNeutral() && Data.CanRevengeNeutral)
            canRevenge = true;
        else if (killer.IsImpostor() && Data.CanRevengeImpostor)
            canRevenge = true;

        if (canRevenge)
            ExPlayerControl.LocalPlayer.RpcCustomDeath(killer, CustomDeathType.Kill);
    }

    private void OnWrapUp(WrapUpEventData data)
    {
        if (Data.CanRevengeExiled &&
            data.exiled != null && data.exiled.Object != null &&
            data.exiled.PlayerId == Player.PlayerId)
        {
            if (IsSHR && revenged) return;
            List<ExPlayerControl> targets = ExPlayerControl.ExPlayerControls.Where(x => x.IsAlive() && x != Player
                && !x.Data.Disconnected).ToList();

            if (targets.Count > 0)
            {
                ExPlayerControl randomTarget = ModHelpers.GetRandom(targets);
                revenged = true;
                Player.RpcCustomDeath(randomTarget, CustomDeathType.Revange);
            }
        }
    }

}
