using Aero.Cms.Modules.Setup.Bootstrap;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class SetupBootstrapHandoffGateTests
{
    [Test]
    public async Task Only_one_handoff_can_claim_persistence_until_a_failure_releases_it()
    {
        var gate = new SetupBootstrapHandoffGate();

        await Assert.That(gate.TryClaim()).IsTrue();
        await Assert.That(gate.TryClaim()).IsFalse();

        gate.Release();

        await Assert.That(gate.TryClaim()).IsTrue();
    }
}
