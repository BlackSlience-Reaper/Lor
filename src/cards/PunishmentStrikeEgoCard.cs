using System;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards;

public sealed class PunishmentStrikeEgoCard : EgoCardBase
{
    private int _previewDamage = HistoryFloorWaspBoss.PunishmentStrikeBaseDamage;

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        HistoryFloorWaspBoss.AttackBuffSfxPath
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move),
        new PowerVar<LibraryOfRuinaConfusionPower>("Confusion", HistoryFloorWaspBoss.PunishmentStrikeConfusionAmount),
        new PowerVar<LibraryVulnerablePower>("Vulnerable", HistoryFloorWaspBoss.PunishmentStrikeVulnerableAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaConfusionPower>(),
        HoverTipFactory.FromPower<LibraryVulnerablePower>()
    ];

    public PunishmentStrikeEgoCard()
        : base(3)
    {
    }

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        LocalOggOneShotPlayer.Play(HistoryFloorWaspBoss.AttackBuffSfxPath, -2f);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars["Confusion"].BaseValue,
            Owner.Creature,
            this);

        await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
            cardPlay.Target,
            (int)DynamicVars["Vulnerable"].BaseValue,
            turns: -1,
            Owner.Creature,
            this);
    }
}
