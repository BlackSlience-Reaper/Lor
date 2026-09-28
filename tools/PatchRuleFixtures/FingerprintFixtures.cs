extern alias other;

// Methods whose IlFingerprint must (or must not) differ; checked by Program.cs.
namespace Fixture
{
    public static class SameName
    {
        public static int Value() => 1;
    }
}

namespace PatchRuleFixtures.Fingerprint
{
    internal static class Calls
    {
        // Same full type name and signature, different assembly: different call target.
        internal static int Local() => Fixture.SameName.Value();

        internal static int Other() => other::Fixture.SameName.Value();

        // Identical body to Local: same fingerprint.
        internal static int LocalTwin() => Fixture.SameName.Value();

        internal static int ConstantA() => 3 + Fixture.SameName.Value();

        internal static int ConstantB() => 4 + Fixture.SameName.Value();

        internal static int BranchA(int value) => value > 0 ? Fixture.SameName.Value() : 0;

        internal static int BranchB(int value) => value >= 0 ? Fixture.SameName.Value() : 0;
    }
}
