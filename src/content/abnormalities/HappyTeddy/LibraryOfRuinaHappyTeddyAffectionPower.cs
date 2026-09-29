using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.abnormalities.HappyTeddy;

public sealed class LibraryOfRuinaHappyTeddyAffectionPower : LibraryOfRuinaPowerModel
{
    private const int MaxAffectionStacks = 3;
    private const string AffectionGainSfxPath = "res://audio/sfx/happy_teddy/happy_teddy_music_box.ogg";

    protected override string LegacyPowerId => "HAPPY_TEDDY_AFFECTION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Affection", 1),
        new DynamicVar("Threshold", MaxAffectionStacks)
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner.Monster is not HappyTeddyMonster teddy || Amount >= MaxAffectionStacks)
        {
            return;
        }

        if (cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        Creature cardDealer = cardPlay.Card.Owner.Creature;
        if (cardDealer.Side != CombatSide.Player)
        {
            return;
        }

        if (!teddy.CanGainAffectionFromPlayedAttack())
        {
            return;
        }

        Flash();
        LocalOggOneShotPlayer.Play(AffectionGainSfxPath, -5f);
        await PowerCmdCompat.ModifyAmount(this, 1m, cardDealer, cardPlay.Card);

        if (Amount >= MaxAffectionStacks)
        {
            await teddy.QueueNostalgicEmbraceFromAffection();
            return;
        }

        teddy.RefreshBackgroundMoonTextLoop();
    }

    public static Task ResetAffectionStacks(Creature creature)
    {
        LibraryOfRuinaHappyTeddyAffectionPower? affection = creature.GetPower<LibraryOfRuinaHappyTeddyAffectionPower>();
        if (affection == null || affection.Amount <= 0)
        {
            return Task.CompletedTask;
        }

        affection.SetAmount(0, silent: true);
        return Task.CompletedTask;
    }
}
