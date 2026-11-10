using AmongUs.GameOptions;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;
using SuperNewRoles.Roles.Crewmate;

namespace SuperNewRoles.Mode;

public static class SHRRoleTypes
{
    public static bool HasImplementedSupport(RoleId role) => role is
        RoleId.Crewmate or RoleId.Impostor or RoleId.Sheriff or RoleId.Jackal
        or RoleId.SelfBomber or RoleId.Samurai or RoleId.WaveCannon or RoleId.SerialKiller
        or RoleId.SideKiller or RoleId.MadKiller or RoleId.EvilGuesser or RoleId.EvilGambler
        or RoleId.Penguin or RoleId.NekoKabocha or RoleId.SuicideWisher or RoleId.Doppelganger or RoleId.Camouflager
        or RoleId.EvilSpeedBooster or RoleId.Finder or RoleId.EvilScientist or RoleId.Conjurer or RoleId.EvilSeer or RoleId.Banshee or RoleId.Rocket or RoleId.Pusher or RoleId.Nun or RoleId.Pteranodon or RoleId.NiceRedRidingHood or RoleId.EvilHacker
        || SHRBatchRoleAbility.Supports(role) || SHRCrewRoleAbility.Supports(role) || role == RoleId.VampireDependent;

    internal static bool ShouldShowInSettings(RoleId role, bool shrMode)
        => !shrMode || HasImplementedSupport(role);

    internal static bool ShouldShowInSettings(RoleId role, bool shrMode, byte mapId)
        => ShouldShowInSettings(role, shrMode) && (!shrMode || IsAvailableOnMap(role, mapId));

    internal static bool IsAvailableOnMap(RoleId role, byte mapId)
        => role is RoleId.HamburgerShop or RoleId.Nun or RoleId.Pteranodon ? mapId == (byte)MapNames.Airship
            : role == RoleId.Pusher ? SHRPusherAbility.SupportsMap(mapId)
            : true;

    public static bool IsSupported(RoleId role) => role is not (
        RoleId.Crab or RoleId.DimensionWalker or RoleId.EvilHawk or RoleId.EvilMechanic
        or RoleId.Kunoichi or RoleId.Matryoshka or RoleId.Mushroomer or RoleId.Necromancer
        or RoleId.Orpheus or RoleId.RocketLauncher or RoleId.Slugger or RoleId.TriggerHappy or RoleId.Tasker or RoleId.SilverBullet);

    public static RoleTypes? GetInitialRole(RoleId role) => role switch
    {
        RoleId.Chief => RoleTypes.Impostor,
        RoleId.ToiletFan => RoleTypes.Engineer,
        RoleId.Technician => RoleTypes.Engineer,
        RoleId.SpeedBooster or RoleId.Lighter or RoleId.Clergy or RoleId.Moving or RoleId.NiceTeleporter or RoleId.WiseMan or RoleId.Santa or RoleId.Pteranodon => RoleTypes.Engineer,
        RoleId.Doorr => RoleTypes.Phantom,
        RoleId.NiceRedRidingHood => RoleTypes.Crewmate,
        _ when SHRCrewRoleAbility.Supports(role) => RoleTypes.Crewmate,
        RoleId.Banshee or RoleId.Conjurer or RoleId.Doppelganger or RoleId.EvilHacker
            or RoleId.ShiftActor or RoleId.SerialKiller => RoleTypes.Shapeshifter,
        RoleId.Camouflager or RoleId.DoubleKiller or RoleId.EvilButtoner or RoleId.EvilDoorr
            or RoleId.EvilMoving or RoleId.EvilScientist or RoleId.EvilSpeedBooster
            or RoleId.Jammer or RoleId.Magaziner or RoleId.Nun or RoleId.Penguin
            or RoleId.Pusher or RoleId.RemoteController or RoleId.Robber or RoleId.Rocket
            or RoleId.Samurai or RoleId.SelfBomber or RoleId.SideKiller or RoleId.Teleporter
            or RoleId.WaveCannon or RoleId.Jackal or RoleId.SuicideWisher => RoleTypes.Phantom,
        RoleId.Mafia => RoleTypes.Crewmate,
        RoleId.DarkKiller or RoleId.EvilGambler or RoleId.EvilGuesser or RoleId.EvilNekomata
            or RoleId.EvilSeer or RoleId.Finder or RoleId.NekoKabocha
            or RoleId.OverKiller or RoleId.Pursuer or RoleId.VampireDependent
            or RoleId.Impostor or RoleId.Sheriff => RoleTypes.Impostor,
        RoleId.MadKiller => GetMadKillerRole(SideKiller.MadKillerCanUseVent),
        RoleId.Vampire => GetVampireRole(Vampire.VampireCreateDependents),
        _ => null,
    };

    internal static RoleTypes GetVampireRole(bool canCreateDependents)
        => canCreateDependents ? RoleTypes.Phantom : RoleTypes.Impostor;

    internal static RoleTypes GetMadKillerRole(bool canUseVent)
        => canUseVent ? RoleTypes.Engineer : RoleTypes.Crewmate;

    internal static RoleTypes GetTaskerRole(bool canKill, bool impostorSelected)
        => !canKill ? RoleTypes.Crewmate : impostorSelected ? RoleTypes.Impostor : RoleTypes.Tracker;
}
