using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.KingOfGreed;
using LibraryOfRuina.visuals.KingOfGreed;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.KingOfGreed;

public sealed class ShiningHappiness : LorMonsterModel
{
    public const string IdleTexturePath = GoldenAmber.Root + "shining_happiness.png";
    public const string SummonSfxPath = GoldenAmber.SfxRoot + "summon_shining_happiness.ogg";
    internal const int KingStrongStacks = 2;
    internal const int KingEnduranceStacks = 5;
    internal const int KingBuffTurns = 1;
    internal const int DeathShardCount = 1;

    private const string GiftMoveId = "HAPPINESS_FRAGMENT";

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 60, 55);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 65, 60);

    public override int DefaultChaoResistance => 30;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override IEnumerable<string> AssetPaths =>
        ShiningHappinessCreatureVisuals
        .Profile.AssetPaths
        .Append(
            SummonSfxPath
        )
        .Concat(ModelDb.Card<ShiningHappinessCard>().AllPortraitPaths)
        .Concat(ModelDb.Card<HappinessShard>().AllPortraitPaths)
        .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
        .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, null, null, silent: true);
        LibraryOfRuinaShiningHappinessPower? power =
            await PowerCmdCompat.Apply<LibraryOfRuinaShiningHappinessPower>(
                Creature,
                1m,
                Creature,
                null,
                silent: true);
        if (power != null)
        {
            await power.ApplyAuraContribution();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var gift = new MoveState(
            GiftMoveId,
            GiftMove,
            new DetailedStatusCardIntent<ShiningHappinessCard>(
                CardCount(),
                PileType.Draw,
                DetailedIntentScopeText.Target));
        gift.FollowUpState = gift;

        return new MonsterMoveStateMachine([gift], gift);
    }

    private int CardCount() =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 1, 2);

    private async Task GiftMove(IReadOnlyList<Creature> targets)
    {
        await GrantShiningHappinessCardsToAllPlayers(CardCount());
    }

    private async Task GrantShiningHappinessCardsToAllPlayers(int count)
    {
        IReadOnlyList<Creature> players = Creature.CombatState?.Players
            .Select(player => player.Creature)
            .Where(creature => creature.IsAlive)
            .ToArray() ?? [];

        if (players.Count == 0 || count <= 0)
        {
            return;
        }

        await CardPileCmdCompat.AddToCombatAndPreview<ShiningHappinessCard>(
            players,
            PileType.Draw,
            count,
            addedByPlayer: false);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState moveState)
            {
                foreach (AbstractIntent intent in moveState.Intents)
                {
                    yield return intent;
                }
            }
        }
    }
}
