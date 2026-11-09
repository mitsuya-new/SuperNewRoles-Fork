using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using FluentAssertions;
using SuperNewRoles.CustomOptions;
using SuperNewRoles.HelpMenus.MenuCategories;
using SuperNewRoles.Modules;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Ability;
using UnityEngine;
using Xunit;

namespace SuperNewRoles.Tests;

public class RoleDictionaryHelpMenuTests
{
    [Theory]
    [InlineData(RoleId.Penguin, true, true)]
    [InlineData(RoleId.Penguin, false, true)]
    [InlineData(RoleId.SuicideWisher, true, true)]
    [InlineData(RoleId.EvilHacker, true, false)]
    [InlineData(RoleId.EvilHacker, false, true)]
    [InlineData(RoleId.Crab, true, false)]
    [InlineData(RoleId.Crab, false, true)]
    [InlineData(RoleId.Tasker, true, false)]
    [InlineData(RoleId.Tasker, false, true)]
    public void SHRFilteringAppliesToSettingsWhileTheDictionaryKeepsAllRoles(RoleId roleId, bool shrMode, bool expected)
    {
        var role = new TestRole
        {
            Role = roleId,
            AssignedTeam = AssignedTeamType.Impostor,
            OptionTeam = RoleOptionMenuType.Impostor,
        };
        SuperNewRoles.Mode.SHRRoleTypes.ShouldShowInSettings(roleId, shrMode).Should().Be(expected);
        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Impostor).Should().BeTrue();
        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Crewmate).Should().BeFalse();
    }

    [Fact]
    public void ShouldShowRoleForTeam_ShowsHiddenRoleInWinnerTeam()
    {
        var role = new TestRole
        {
            Role = RoleId.ShermansServant,
            AssignedTeam = AssignedTeamType.Crewmate,
            WinnerTeam = WinnerTeamType.Neutral,
            OptionTeam = RoleOptionMenuType.Hidden,
            HiddenOption = true,
        };

        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Neutral).Should().BeTrue();
        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Crewmate).Should().BeFalse();
    }

    [Fact]
    public void ShouldShowRoleForTeam_KeepsDictionaryHiddenRolesHidden()
    {
        var role = new TestRole
        {
            Role = RoleId.VampireDependent,
            AssignedTeam = AssignedTeamType.Impostor,
            OptionTeam = RoleOptionMenuType.Hidden,
            HiddenOption = true,
            HideInRoleDictionary = true,
        };

        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Impostor).Should().BeFalse();
    }

    [Fact]
    public void ShouldShowRoleForTeam_UsesOptionTeamForVisibleRoles()
    {
        var role = new TestRole
        {
            Role = RoleId.NiceNekomata,
            AssignedTeam = AssignedTeamType.Crewmate,
            WinnerTeam = WinnerTeamType.Neutral,
            OptionTeam = RoleOptionMenuType.Crewmate,
        };

        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Crewmate).Should().BeTrue();
        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Neutral).Should().BeFalse();
    }

    [Fact]
    public void ShouldShowRoleForTeam_DoesNotShowVanillaRoles()
    {
        var role = new TestRole
        {
            Role = RoleId.Crewmate,
            AssignedTeam = AssignedTeamType.Crewmate,
            OptionTeam = RoleOptionMenuType.Crewmate,
            QuoteMod = QuoteMod.Vanilla,
        };

        RoleDictionaryHelpMenu.ShouldShowRoleForTeam(role, RoleOptionMenuType.Crewmate).Should().BeFalse();
    }

    private sealed class TestRole : IRoleBase
    {
        public RoleId Role { get; init; }
        public string RoleName => Role.ToString();
        public Color32 RoleColor => default;
        public bool HiddenOption { get; init; }
        public bool HideInRoleDictionary { get; init; }
        public List<AssignedTeamType> AssignedTeams => new() { AssignedTeam };
        public CustomOption[] Options => Array.Empty<CustomOption>();
        public int? PercentageOption => null;
        public int? NumberOfCrews => null;
        public List<Func<AbilityBase>> Abilities { get; } = new();
        public QuoteMod QuoteMod { get; init; } = QuoteMod.SuperNewRoles;
        public AssignedTeamType AssignedTeam { get; init; }
        public WinnerTeamType WinnerTeam { get; init; }
        public TeamTag TeamTag { get; init; }
        public RoleTag[] RoleTags { get; init; } = Array.Empty<RoleTag>();
        public RoleOptionMenuType OptionTeam { get; init; }
        public short IntroNum { get; init; } = 1;
        public RoleTypes IntroSoundType { get; init; } = RoleTypes.Crewmate;
        public AudioClip CustomIntroSound => null!;
        public RoleId[] RelatedRoleIds { get; init; } = Array.Empty<RoleId>();
        public MapNames[] AvailableMaps { get; init; } = Array.Empty<MapNames>();
        public Sprite RoleIcon => null!;
    }
}
