using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

public sealed class FlutteringHungerFrenzyEgoCard : EgoCardBase
{
    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        HistoryFloorFlutteringBoss.BossAttackSfxPath
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("Hits", HistoryFloorEgoNumbers.HungerFrenzyHitCount),
        new HealVar(HistoryFloorEgoNumbers.HungerFrenzyHeal),
        new DamageVar("FinalDamage", HistoryFloorEgoNumbers.HungerFrenzyCardFinalDamage, ValueProp.Move),
        new PowerVar<LibraryBleedingPower>("Bleed", HistoryFloorEgoNumbers.HungerFrenzyBleed)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    public FlutteringHungerFrenzyEgoCard()
        : base(3, previewDamage: HistoryFloorEgoNumbers.HungerFrenzyCardMultiHitDamage)
    {
    }

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    public void SetPreviewDamage(int multiHitDamage, int finalDamage)
    {
        base.SetPreviewDamage(multiHitDamage);
        DynamicVars["FinalDamage"].BaseValue = finalDamage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        for (int i = 0; i < HistoryFloorEgoNumbers.HungerFrenzyHitCount; i++)
        {
            LocalOggOneShotPlayer.Play(HistoryFloorFlutteringBoss.BossAttackSfxPath, -2f);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.IntValue);

        LocalOggOneShotPlayer.Play(HistoryFloorFlutteringBoss.BossAttackSfxPath, -2f);
        AttackCommand finalAttack = await DamageCmd.Attack(DynamicVars["FinalDamage"].BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        IReadOnlyList<Creature> bleedTargets = AttackCommandCompat.Results(finalAttack)
            .Where(static result => result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToList();
        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(
                choiceContext,
                bleedTargets,
                DynamicVars["Bleed"].BaseValue,
                Owner.Creature,
                this);
        }
    }
}
