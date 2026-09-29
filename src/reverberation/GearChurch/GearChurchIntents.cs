using System;
using System.Linq;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using static LibraryOfRuina.reverberation.GearChurch.GearChurchRules;

namespace LibraryOfRuina.reverberation.GearChurch;

internal static class GearChurchIntents
{
    internal static AbstractIntent Create(GearChurchMonsterBase monster, GearChurchMove move)
    {
        string key = "GEAR_CHURCH_" + move.ToString().ToUpperInvariant();
        switch (move)
        {
            case GearChurchMove.ThoughtAcceleration:
                return new CombinedDefendBuffIntent(ThoughtAccelerationBlock, key,
                    IntentBadge.FromPower<NextTurnVigorPower>(ThoughtAccelerationVigor),
                    IntentBadge.FromPower<PlatingPower>(ThoughtAccelerationPlating));
            case GearChurchMove.ThoughtReveal:
                return new CombinedTargetedAttackBuffIntent(() => Damage(move), () => Hits(move),
                    key, key, true, _ => monster.SelectedTarget(),
                    IntentBadge.FromPower<NextTurnVigorPower>(RevealVigor),
                    IntentBadge.FromPower<PlatingPower>(RevealPlating));
            case GearChurchMove.ThoughtProselytize:
                return new GearChurchProselytizeIntent(monster, key);
            case GearChurchMove.FleshEncourage:
                return new GearChurchHealIntent(key);
            case GearChurchMove.FleshStrengthen:
                return new CombinedDefendBuffIntent(StrengthenBlock, key,
                    IntentBadge.FromPower<LibraryStrongPower>(() => DamageValue(StrengthenStrong, StrengthenHighStrong)));
            case GearChurchMove.FleshAcceleration:
                return new CombinedDefendBuffIntent(FleshAccelerationBlock, key,
                    IntentBadge.FromPower<NextTurnVigorPower>(FleshAccelerationVigor),
                    IntentBadge.FromPower<PlatingPower>(FleshAccelerationPlating));
            case GearChurchMove.Brainwash:
                return new IndiscriminateAttackIntent(() => Damage(move), () => Hits(move), key,
                    _ => monster.LivingPlayers(), IntentBadge.FromPower<RingingPower>(BrainwashRinging))
                {
                    CombinedEffect = GroupAttackCombinedEffect.Debuff
                };
            case GearChurchMove.DefenseInstruction:
                return new CombinedTargetedAttackDefendIntent(() => Damage(move), () => Hits(move),
                    key, key, true, _ => monster.SelectedTarget(), DefenseBlock,
                    IntentBadge.FromPower<GearChurchSmokePower>(DefenseSmoke));
            case GearChurchMove.Guidance:
            case GearChurchMove.Assault:
            case GearChurchMove.SteamEruption:
                return new GearChurchAttackIntent(monster, move, key);
            default:
                return new HiddenIntent();
        }
    }
}

internal sealed class GearChurchAttackIntent(GearChurchMonsterBase monster, GearChurchMove move, string key)
    : MultiAttackIntent(Damage(move), () => Hits(move)), IIntentTargetLineProvider
{
    protected override LocString IntentLabelFormat =>
        new("intents", Repeats > 1 ? "FORMAT_DAMAGE_MULTI" : "FORMAT_DAMAGE_SINGLE");

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = new("intents", key);
        description.Add("Damage", GetSingleDamage(targets, owner));
        description.Add("Repeat", Repeats);
        description.Add("Smoke", Smoke(move));
        return description;
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(Creature owner,
        IReadOnlyList<Creature>? fallbackTargets) =>
        monster.SelectedTarget() is { } target ? [new IntentTargetLineTarget(target)] : [];
}

internal sealed class GearChurchProselytizeIntent(GearChurchMonsterBase monster, string key)
    : DetailedStatusCardIntent<Dazed>(ProselytizeCardCount, PileType.Draw, showSingleTargetMarker: false),
      IDetailedIntentVisuals, IIntentTargetLineProvider
{
    private static IEnumerable<DetailedIntentVisualEffect> BuffEffects =>
    [
        DetailedIntentVisualEffect.FromBadge(IntentBadge.FromPower<NextTurnVigorPower>(ProselytizeVigor)),
        DetailedIntentVisualEffect.FromBadge(IntentBadge.FromPower<PlatingPower>(ProselytizePlating))
    ];

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(BuffEffects.SelectMany(effect => effect.AssetPaths));

    DetailedIntentVisualState IDetailedIntentVisuals.GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner) =>
        new([.. base.GetDetailedIntentVisuals(monster.LivingPlayers(), owner).Effects, .. BuffEffects]);

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = new("intents", key);
        description.Add("Cards", CardCount);
        description.Add("Vigor", ProselytizeVigor);
        description.Add("Plating", ProselytizePlating);
        return description;
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(Creature owner,
        IReadOnlyList<Creature>? fallbackTargets) =>
        monster.LivingPlayers().Select(target => new IntentTargetLineTarget(target)).ToArray();
}

internal sealed class GearChurchHealIntent(string key) : HealIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = new("intents", key);
        description.Add("HealPercent", EncourageHealPercent);
        return description;
    }
}
