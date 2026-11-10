using System.Collections.Generic;
using SuperNewRoles.Mode;
using SuperNewRoles.Roles;
using Xunit;

namespace SuperNewRoles.Tests;

public class SHRVentRecoveryTests
{
    [Fact]
    public void RepeatedRecoveryUsesTheOwnerExitEntryCooldownSequenceWithoutChangingRoles()
    {
        var calls = new List<RpcCalls>();
        SHRVentRecovery.WriteOwnerReplay(calls.Add);
        SHRVentRecovery.WriteOwnerReplay(calls.Add);
        Assert.Equal(new[] { RpcCalls.BootFromVent, RpcCalls.EnterVent, RpcCalls.ProtectPlayer,
            RpcCalls.BootFromVent, RpcCalls.EnterVent, RpcCalls.ProtectPlayer }, calls);
        Assert.DoesNotContain(RpcCalls.SetRole, calls);
    }

    [Theory]
    [InlineData(RoleId.Technician, true)]
    [InlineData(RoleId.ToiletFan, true)]
    [InlineData(RoleId.Sheriff, false)]
    [InlineData(RoleId.DoubleKiller, false)]
    [InlineData(RoleId.Crewmate, false)]
    public void OwnerReplayIsLimitedToTheAffectedEngineerRoles(RoleId role, bool replay)
        => Assert.Equal(replay, SHRVentRecovery.UsesOwnerReplay(role));

    [Theory]
    [InlineData(VentilationSystem.Operation.Enter)]
    [InlineData(VentilationSystem.Operation.Move)]
    public void OwnerReplayAllowsTheOriginalVentOperationToAdvanceItsSequence(VentilationSystem.Operation operation)
        => Assert.Equal(operation, SHRSamuraiRestrictions.ResolveVentOperation(operation, true, true));
}
