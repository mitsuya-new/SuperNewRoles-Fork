using System.Collections.Generic;

namespace SuperNewRoles.Modules;

internal sealed class AdminRoomCount
{
    private readonly HashSet<byte> players = new();
    internal int Total { get; private set; }
    internal int Impostors { get; private set; }
    internal int Corpses { get; private set; }

    internal bool AddPlayer(byte player, bool hidden, bool impostor)
    {
        if (!players.Add(player) || hidden) return false;
        Total++;
        if (impostor) Impostors++;
        return true;
    }

    internal void AddCorpse()
    {
        Total++;
        Corpses++;
    }

    internal (int Impostors, int Corpses) VisibleDetails(bool enhanced, bool showImpostors, bool showCorpses)
        => (enhanced && showImpostors ? Impostors : 0, enhanced && showCorpses ? Corpses : 0);
}
