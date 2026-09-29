using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.SongMachine;

public enum SongMachinePageMode
{
    None = 0,
    Music = 1,
    Melody = 2,
    Addiction = 3
}

public sealed class SongMachinePageRelic : ModalPageRelic<SongMachinePageMode>
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

    protected override SongMachinePageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    // 预选写入模式时还会通知一次图标变化，获得时选择则不会。
    protected override void ApplyPreselectedMode(SongMachinePageMode mode) => AssignPreselectedModeOnly(mode);

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

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<SongMachineMusicChoiceCard>(Owner),
            Owner.RunState.CreateCard<SongMachineMelodyChoiceCard>(Owner),
            Owner.RunState.CreateCard<SongMachineAddictionChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnFallback() => _healedCombatIds.Clear();

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = RelicStatus.Normal;
    }
}

