using LibraryOfRuina.relics.CosmicFragment;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.cards.CosmicFragment;

public abstract class CosmicFragmentPageChoiceCardBase : PageChoiceCard<CosmicFragmentPageMode>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaosLoss", CosmicFragmentPageRelic.OtherworldlyEchoChaosLoss),
        new HealVar(CosmicFragmentPageRelic.OtherworldlyEchoHeal),
        new DynamicVar("ChaosPercent", CosmicFragmentPageRelic.TentacleChaosPercent),
        new DynamicVar("ChaosStepPercent", CosmicFragmentPageRelic.IncomprehensibleChaosStepPercent),
        new DynamicVar("ResetAttacks", CosmicFragmentPageRelic.IncomprehensibleResetAttacks)
    ];
}
