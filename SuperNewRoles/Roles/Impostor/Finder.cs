using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.Events.PCEvents;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles.Ability;
using UnityEngine;

namespace SuperNewRoles.Roles.Impostor;

internal class Finder : RoleBase<Finder>
{
    public override RoleId Role => RoleId.Finder;
    public override Color32 RoleColor => Palette.ImpostorRed;
    public override List<Func<AbilityBase>> Abilities => new()
    {
        () => new CustomKillButtonAbility(() => true, () => FinderKillCooldown, onlyCrewmates: () => true),
        () => new FinderAbility(FinderRevealKillCount)
    };

    public override QuoteMod QuoteMod => QuoteMod.SuperNewRoles;
    public override RoleTypes IntroSoundType => RoleTypes.Impostor;
    public override short IntroNum => 1;

    public override AssignedTeamType AssignedTeam => AssignedTeamType.Impostor;
    public override WinnerTeamType WinnerTeam => WinnerTeamType.Impostor;
    public override TeamTag TeamTag => TeamTag.Impostor;
    public override RoleTag[] RoleTags => [RoleTag.Information];
    public override RoleOptionMenuType OptionTeam => RoleOptionMenuType.Impostor;

    [CustomOptionFloat("FinderKillCooldown", 2.5f, 60f, 2.5f, 25f, translationName: "KillCoolTime")]
    public static float FinderKillCooldown;

    [CustomOptionInt("FinderRevealKillCount", 1, 10, 1, 2)]
    public static int FinderRevealKillCount;
}

internal sealed class FinderKillProgress
{
    private readonly int needed;
    private int kills;
    internal bool Revealed { get; private set; }
    internal FinderKillProgress(int needed) => this.needed = Math.Max(1, needed);
    internal bool Record(bool succeeded)
    {
        if (!succeeded || Revealed) return false;
        kills++;
        if (kills < needed) return false;
        Revealed = true;
        return true;
    }
}

public class FinderAbility : AbilityBase
{
    private readonly FinderKillProgress progress;
    private KnowOtherAbility _knowMadmateAbility;

    public FinderAbility(int neededKills)
    {
        progress = new FinderKillProgress(neededKills);
    }

    public override void AttachToAlls()
    {
        _knowMadmateAbility = new KnowOtherAbility(
            player => progress.Revealed && player.IsMadRoles(),
            () => false
        );
        Player.AttachAbility(_knowMadmateAbility, new AbilityParentAbility(this));
        if (AmongUsClient.Instance?.AmHost == true && Mode.ModeManager.IsMode(ModeId.SuperHostRoles) && !Player.AmOwner)
            SubscribeWithAbility(MurderEvent.Instance, OnMurder);
    }

    public override void AttachToLocalPlayer()
    {
        base.AttachToLocalPlayer();
        SubscribeWithAbility(MurderEvent.Instance, OnMurder);
    }

    private void OnMurder(MurderEventData data)
    {
        if (data.killer != Player) return;
        bool succeeded = !Mode.ModeManager.IsMode(ModeId.SuperHostRoles) || data.resultFlags.HasFlag(MurderResultFlags.Succeeded);
        if (progress.Record(succeeded)) NameText.UpdateAllNameInfo();
    }

    internal bool CanIdentifySHR(ExPlayerControl target) => progress.Revealed && target.IsMadRoles();
}
