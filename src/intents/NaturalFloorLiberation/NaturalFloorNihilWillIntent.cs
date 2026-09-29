using System.Linq;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.monsters.NaturalFloorLiberation;

namespace LibraryOfRuina.intents;

internal sealed class NaturalFloorNihilWillIntent : CombinedAttackIntentBase,
    IIntentTargetLineProvider, IUsesVanillaPlayerTargetIntentVisual
{
    internal NaturalFloorNihilWillIntent(
        NaturalFloorNihilMove move,
        string descriptionKey,
        IntentBadge[] badges)
        : base(() => move.Damage, () => move.Hits, descriptionKey, badges)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackDebuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_DEBUFF";

    public bool UsesVanillaPlayerTargetIntentVisual => true;

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets) =>
        owner.CombatState?.PlayerCreatures
            .Where(player => player.IsAlive)
            .OrderBy(player => player.Player!.NetId)
            .Select(player => new IntentTargetLineTarget(player, "NihilWill"))
            .ToArray() ?? [];
}
