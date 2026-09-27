using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.specialguests.Kali;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.RedMist;

public abstract class RedMistEgoCardBase : EgoCardBase
{
    protected RedMistEgoCardBase(int cost, TargetType targetType = TargetType.AnyEnemy)
        : base(cost, targetType)
    {
    }

    public override int MaxUpgradeLevel => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override void OnUpgrade()
    {
    }

    public void UpgradePreview()
    {
        UpgradeInternal();
        FinalizeUpgradeInternal();
    }
}

public sealed class RedMistVerticalSplitEgoCard : RedMistEgoCardBase
{
    private int _previewDamage = Kali.VerticalSplitDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move),
        new DynamicVar("Hits", Kali.VerticalSplitHits),
        new DynamicVar("Threshold", Kali.RepeatUnblockedDamageThreshold)
    ];

    public RedMistVerticalSplitEgoCard()
        : base(2)
    {
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await Kali.ExecuteCardAttack(
            choiceContext,
            this,
            cardPlay.Target,
            _previewDamage,
            Kali.VerticalSplitHits,
            Kali.SlashHitVfx,
            "AttackSlash");
    }
}

public sealed class RedMistThrustEgoCard : RedMistEgoCardBase
{
    private int _previewDamage = Kali.ThrustDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move),
        new DynamicVar("Hits", Kali.ThrustHits),
        new DynamicVar("Threshold", Kali.RepeatUnblockedDamageThreshold)
    ];

    public RedMistThrustEgoCard()
        : base(2)
    {
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await Kali.ExecuteCardAttack(
            choiceContext,
            this,
            cardPlay.Target,
            _previewDamage,
            Kali.ThrustHits,
            Kali.PierceHitVfx,
            "AttackPierce");
    }
}

public sealed class RedMistHorizontalSlashEgoCard : RedMistEgoCardBase
{
    private int _previewDamage = Kali.HorizontalSlashDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move),
        new DynamicVar("Hits", Kali.HorizontalSlashHits),
        new DynamicVar("Threshold", Kali.RepeatUnblockedDamageThreshold),
        new PowerVar<LibraryBleedingPower>("Bleed", Kali.HorizontalSlashBleed)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    public RedMistHorizontalSlashEgoCard()
        : base(2)
    {
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await Kali.ExecuteCardAttack(
            choiceContext,
            this,
            cardPlay.Target,
            _previewDamage,
            Kali.HorizontalSlashHits,
            Kali.SlashHitVfx,
            "AttackSlash");
        await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
            choiceContext,
            cardPlay.Target,
            Kali.HorizontalSlashBleed,
            Owner.Creature,
            this);
    }
}

public sealed class RedMistBloodMistEgoCard : RedMistEgoCardBase
{
    private int _previewDamage = Kali.BloodMistDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move),
        new DynamicVar("Cards", 1m)
    ];

    public RedMistBloodMistEgoCard()
        : base(5)
    {
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await Kali.ExecuteCardAttack(
            choiceContext,
            this,
            cardPlay.Target,
            _previewDamage,
            1,
            Kali.SlashHitVfx,
            "BloodMist",
            onUnblockedHit: target => Kali.ExhaustRandomDrawPileCard(choiceContext, Owner, target));
    }
}

public sealed class RedMistBattleWillEgoCard : RedMistEgoCardBase
{
    private int _previewDamage = Kali.BattleWillDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move)
    ];

    public RedMistBattleWillEgoCard()
        : base(3)
    {
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        bool attackedAgain = false;
        await Kali.ExecuteCardAttack(
            choiceContext,
            this,
            cardPlay.Target,
            _previewDamage,
            1,
            Kali.BluntHitVfx,
            "AttackBlunt",
            onUnblockedHit: async target =>
            {
                if (attackedAgain)
                {
                    return;
                }

                attackedAgain = true;
                await Kali.ExecuteCardAttack(
                    choiceContext,
                    this,
                    target,
                    _previewDamage,
                    1,
                    Kali.BluntHitVfx,
                    "AttackBlunt");
            });
    }
}

public sealed class RedMistFocusBreathEgoCard : RedMistEgoCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(Kali.FocusBreathBlock, ValueProp.Move),
        new PowerVar<LibraryStrongPower>("Strong", Kali.FocusBreathStrong),
        new DynamicVar("Turns", Kali.FocusBreathStrongDurationTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    public RedMistFocusBreathEgoCard()
        : base(2, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Guard", 0.4f);
        await CreatureCmd.GainBlock(Owner.Creature, Kali.FocusBreathBlock, ValueProp.Move, cardPlay);
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            Kali.FocusBreathStrong,
            Kali.FocusBreathStrongDurationTurns - 1,
            IsPermanent: false,
            Owner.Creature,
            this);
    }
}

public sealed class RedMistFieldOfCorpsesEgoCard : RedMistEgoCardBase
{
    private int _previewDamage = Kali.FieldOfCorpsesDamage;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move),
        new PowerVar<LibraryBleedingPower>("Bleed", Kali.FieldOfCorpsesBleed)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    public RedMistFieldOfCorpsesEgoCard()
        : base(6)
    {
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await Kali.ExecuteCardAttack(
            choiceContext,
            this,
            cardPlay.Target,
            _previewDamage,
            1,
            Kali.SlashHitVfx,
            "FieldOfCorpses",
            onUnblockedHit: target => PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                choiceContext,
                target,
                Kali.FieldOfCorpsesBleed,
                Owner.Creature,
                this));
    }
}
