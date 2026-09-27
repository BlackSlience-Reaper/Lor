using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.SongMachine;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.SongMachine;

public enum SongMachinePageMode
{
    None = 0,
    Music = 1,
    Melody = 2,
    Addiction = 3
}

public sealed class SongMachinePageRelic : RelicModel
{
    internal const int MusicStartStrength = 1;
    internal const int MusicKillHeal = 4;

    internal const int MelodyStartStrength = 1;
    internal const int MelodyDazedCount = 1;

    internal const int AddictionStartStrong = 2;
    internal const int AddictionDexterityLoss = 1;

    private HashSet<uint> _healedCombatIds = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _healedCombatIds = [.. _healedCombatIds];
    }

    protected override string IconBaseName => "song_machine_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<SongMachinePageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)SongMachinePageMode.None),
        new HealVar(MusicKillHeal),
        new PowerVar<StrengthPower>("MusicStrength", MusicStartStrength),
        new PowerVar<StrengthPower>("MelodyStrength", MelodyStartStrength),
        new PowerVar<LibraryStrongPower>("AddictionStrong", AddictionStartStrong),
        new PowerVar<DexterityPower>("AddictionDexterity", AddictionDexterityLoss),
        new DynamicVar("Dazed", MelodyDazedCount),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    [SavedProperty]
    public SongMachinePageMode Mode { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        UpdateModeUiState();
        if (Mode != SongMachinePageMode.None)
        {
            return;
        }

        IReadOnlyList<CardModel> options = CreateModeChoiceCards();
        CardModel? chosenCard = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            Owner,
            canSkip: true);

        if (chosenCard == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        Mode = ResolveModeFromChoiceCard(chosenCard);
        UpdateModeUiState();
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        _healedCombatIds.Clear();
        UpdateModeUiState();

        switch (Mode)
        {
            case SongMachinePageMode.Music:
            {
                List<Creature> aliveEnemies = AllyTurnRegistry
                    .FilterPlayerEnemyTargets(Owner.Creature.CombatState?.Enemies)
                    .Where(enemy => enemy.IsAlive)
                    .ToList();

                if (aliveEnemies.Count > 0)
                {
                    Flash(aliveEnemies);
                    await PowerCmdCompat.Apply<StrengthPower>(aliveEnemies, MusicStartStrength, Owner.Creature, null);
                }

                break;
            }
            case SongMachinePageMode.Melody:
                Flash();
                await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, MelodyStartStrength, Owner.Creature, null);
                await CardPileCmdCompat.AddToCombatWithoutPreview<Dazed>(
                    Owner.Creature,
                    PileType.Discard,
                    MelodyDazedCount,
                    addedByPlayer: false);
                break;
            case SongMachinePageMode.Addiction:
                Flash();
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    Owner.Creature,
                    AddictionStartStrong,
                    turns: -1,
                    Owner.Creature,
                    null);
                await PowerCmdCompat.Apply<DexterityPower>(Owner.Creature, -AddictionDexterityLoss, Owner.Creature, null);
                break;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _healedCombatIds.Clear();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != SongMachinePageMode.Music
            || result.UnblockedDamage <= 0
            || target.IsPlayer
            || dealer == null
            || (dealer != Owner.Creature && dealer.PetOwner != Owner)
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || target.IsAlive)
        {
            return;
        }

        uint? combatId = target.CombatId;
        if (combatId == null || !_healedCombatIds.Add(combatId.Value))
        {
            return;
        }

        Flash([target]);
        await CreatureCmd.Heal(Owner.Creature, MusicKillHeal);
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<SongMachineMusicChoiceCard>(Owner),
            Owner.RunState.CreateCard<SongMachineMelodyChoiceCard>(Owner),
            Owner.RunState.CreateCard<SongMachineAddictionChoiceCard>(Owner)
        ];
    }

    private static SongMachinePageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            SongMachineMusicChoiceCard => SongMachinePageMode.Music,
            SongMachineMelodyChoiceCard => SongMachinePageMode.Melody,
            SongMachineAddictionChoiceCard => SongMachinePageMode.Addiction,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(SongMachinePageMode mode)
    {
        return mode is SongMachinePageMode.None
            or SongMachinePageMode.Music
            or SongMachinePageMode.Melody
            or SongMachinePageMode.Addiction;
    }

    private static bool IsConcreteMode(SongMachinePageMode mode)
    {
        return mode is SongMachinePageMode.Music
            or SongMachinePageMode.Melody
            or SongMachinePageMode.Addiction;
    }

    private void EnsureValidModeOrFallback(string context)
    {
        if (IsConcreteMode(Mode))
        {
            return;
        }

        FallbackToDefaultModeAfterLoad(context);
    }

    private void FallbackToDefaultModeAfterLoad(string context)
    {
        SongMachinePageMode oldMode = Mode;
        Mode = SongMachinePageMode.Music;
        _healedCombatIds.Clear();
        Log.Warn("[LibraryOfRuina.PageRelic] SongMachinePageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Music.");
        UpdateModeUiState();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = RelicStatus.Normal;
    }
}

