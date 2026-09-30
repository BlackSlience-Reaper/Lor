using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.abnormalities.CosmicFragment;

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
