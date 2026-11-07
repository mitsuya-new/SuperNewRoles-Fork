using AmongUs.GameOptions;
using SuperNewRoles.Roles;
using SuperNewRoles.Roles.Impostor;

namespace SuperNewRoles.Mode;

public static class SHRRoleTypes
{
    public static bool IsSupported(RoleId role) => role is not (
        RoleId.Crab or RoleId.DimensionWalker or RoleId.EvilHawk or RoleId.EvilMechanic
        or RoleId.Kunoichi or RoleId.Matryoshka or RoleId.Mushroomer or RoleId.Necromancer
        or RoleId.Orpheus or RoleId.RocketLauncher or RoleId.Slugger or RoleId.TriggerHappy);

    public static RoleTypes? GetInitialRole(RoleId role) => role switch
    {
        RoleId.Banshee or RoleId.Conjurer or RoleId.Doppelganger or RoleId.EvilHacker
            or RoleId.ShiftActor or RoleId.SuicideWisher or RoleId.SerialKiller => RoleTypes.Shapeshifter,
        RoleId.Camouflager or RoleId.DoubleKiller or RoleId.EvilButtoner or RoleId.EvilDoorr
            or RoleId.EvilMoving or RoleId.EvilScientist or RoleId.EvilSpeedBooster
            or RoleId.Jammer or RoleId.Magaziner or RoleId.Nun or RoleId.Penguin
            or RoleId.Pusher or RoleId.RemoteController or RoleId.Robber or RoleId.Rocket
            or RoleId.Samurai or RoleId.SelfBomber or RoleId.SideKiller or RoleId.Teleporter
            or RoleId.WaveCannon or RoleId.Jackal => RoleTypes.Phantom,
        RoleId.DarkKiller or RoleId.EvilGambler or RoleId.EvilGuesser or RoleId.EvilNekomata
            or RoleId.EvilSeer or RoleId.Finder or RoleId.Mafia or RoleId.NekoKabocha
            or RoleId.OverKiller or RoleId.Pursuer or RoleId.VampireDependent
            or RoleId.Impostor or RoleId.Sheriff => RoleTypes.Impostor,
        RoleId.MadKiller or RoleId.Tasker => RoleTypes.Crewmate,
        RoleId.Vampire => GetVampireRole(Vampire.VampireCreateDependents),
        _ => null,
    };

    internal static RoleTypes GetVampireRole(bool canCreateDependents)
        => canCreateDependents ? RoleTypes.Phantom : RoleTypes.Impostor;
}
