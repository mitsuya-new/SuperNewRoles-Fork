using SuperNewRoles.Mode;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRReviveSyncTests
{
    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    public void NativeReviveRequiresACurrentLivingConnectedPlayer(bool connected, bool alive, bool started, bool gameOver, bool expected)
        => Assert.Equal(expected, SHRReviveSync.CanRestore(connected, alive, started, gameOver));
}
