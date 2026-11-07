using System;
using SuperNewRoles.CustomOptions.Categories;

namespace SuperNewRoles.Modules;
[Flags]
public enum DisplayModeId
{
    None = 0,
    Default = 1 << 0,
    SuperHostRolesOnly = 1 << 1,
    WCBattleRoyal = 1 << 2,
    BattleRoyal = 1 << 3,
    All = Default | SuperHostRolesOnly | WCBattleRoyal | BattleRoyal
}
public enum ModeId
{
    Default,
    WCBattleRoyal,
    BattleRoyal,
    SuperHostRoles,
}

public static class DisplayMode
{
    public static bool HasMode(DisplayModeId current, DisplayModeId target)
    {
        if (current == DisplayModeId.SuperHostRolesOnly)
            return (target & (DisplayModeId.Default | DisplayModeId.SuperHostRolesOnly)) != 0;
        return target.HasFlag(current);
    }

    public static DisplayModeId GetCurrentMode()
    {
        switch (Categories.ModeOption)
        {
            case ModeId.Default:
                return DisplayModeId.Default;
            case ModeId.SuperHostRoles:
                return DisplayModeId.SuperHostRolesOnly;
            case ModeId.WCBattleRoyal:
                return DisplayModeId.WCBattleRoyal;
            case ModeId.BattleRoyal:
                return DisplayModeId.BattleRoyal;
            default:
                return DisplayModeId.Default;
        }
    }
}
