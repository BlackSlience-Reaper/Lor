using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.cards.RoadHome;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.ScaredyCat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.RoadHome;

public sealed class RoadHomePageRelic : LibraryRelicModel
{
    public const int CourageMaxHpPercent = 25;
    public const int CourageTotalUses = 3;
    public const int CompanionGrowthStrength = 3;
    public const int CompanionGrowthDexterity = 2;
    internal const int CompanionGrowthMaxHp = 11;
    private const float CompanionSpawndeltaY = 50f;
    private const float CompanionSpawndeltaX = -200f;
    public const int HomeArtifact = 1;
    public const int HomeBuffer = 1;
    public new const string IconPath = "res://images/relics/road_home_page_relic.png";

    private int _courageMaxHpGainedThisCombat;
    private bool _companionDiedThisCombat;

    protected override string IconBaseName => "road_home_page_relic";

    public override string PackedIconPath => IconPath;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool AddsPet => Mode == RoadHomePageMode.CompanionRoad;

    public override bool SpawnsPets => Mode == RoadHomePageMode.CompanionRoad;

    public override bool HasRightClick => Mode == RoadHomePageMode.Courage;

    public override bool ShowCounter =>
        Mode == RoadHomePageMode.Courage && CombatManager.Instance.IsInProgress;

    public override int DisplayAmount =>
        Mode == RoadHomePageMode.Courage
            ? Math.Max(0, CourageUsesRemaining)
            : 0;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<RoadHomePageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ArtifactPower>(),
        HoverTipFactory.FromPower<BufferPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new("Mode", (int)RoadHomePageMode.None),
        new("MaxHpPercent", CourageMaxHpPercent),
        new("CourageUses", CourageTotalUses),
        new("RemainingUses", CourageTotalUses),
        new("CourageExhausted", 0),
        new("BaseHp", ScaredyCatCompanion.BaseHp),
        new("Strength", CompanionGrowthStrength),
        new("Dexterity", CompanionGrowthDexterity),
        new("MaxHp", CompanionGrowthMaxHp),
        new("CompanionStrengthBonus", 0),
        new("CompanionDexterityBonus", 0),
        new("CompanionMaxHpBonus", 0),
        new("CompanionCurrentHp", ScaredyCatCompanion.BaseHp),
        new("CompanionMaxHp", ScaredyCatCompanion.BaseHp),
        new("Artifact", HomeArtifact),
        new("Buffer", HomeBuffer)
    ];

    [SavedProperty]
    public RoadHomePageMode Mode { get; private set; }

    [SavedProperty]
    public int CourageUsesRemaining { get; private set; } = CourageTotalUses;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool CourageUsedThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CompanionStrengthBonus { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CompanionDexterityBonus { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    internal int CompanionMaxHpBonus { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CompanionCurrentHp { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != RoadHomePageMode.None)
        {
            UpdateModeUiState();
            RefreshInventoryIcon();
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

        SetMode(ResolveModeFromChoiceCard(chosenCard));
        if (Mode == RoadHomePageMode.CompanionRoad && CombatManager.Instance.IsInProgress)
        {
            await SummonCompanionIfNeeded();
        }
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        CourageUsedThisCombat = false;
        _courageMaxHpGainedThisCombat = 0;
        _companionDiedThisCombat = false;

        if (Mode == RoadHomePageMode.CompanionRoad)
        {
            await SummonCompanionIfNeeded();
        }
        else if (Mode == RoadHomePageMode.Home && Owner?.Creature is { IsAlive: true } ownerCreature)
        {
            await PowerCmdCompat.Apply<ArtifactPower>(ownerCreature, HomeArtifact, ownerCreature, null);
            await PowerCmdCompat.Apply<BufferPower>(ownerCreature, HomeBuffer, ownerCreature, null);
        }

        UpdateModeUiState();
    }

    public override bool CanHandleRightClickLocal(LibraryRightClickContext context) =>
        CanUseCourageRightClick();

    public override bool CanExecuteRightClick(LibraryRightClickExecutionContext context) =>
        CanUseCourageRightClick();

    public override async Task OnRightClick(LibraryRightClickExecutionContext context)
    {
        if (!CanUseCourageRightClick() || Owner?.Creature == null)
        {
            return;
        }

        int gain = Math.Max(1, (int)Math.Ceiling(Owner.Creature.MaxHp * CourageMaxHpPercent / 100m));
        Flash();
        CourageUsedThisCombat = true;
        CourageUsesRemaining = Math.Max(0, CourageUsesRemaining - 1);
        _courageMaxHpGainedThisCombat += gain;
        await CreatureCmd.GainMaxHp(Owner.Creature, gain);
        UpdateModeUiState();
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        Creature? companion = Mode == RoadHomePageMode.CompanionRoad ? FindOwnedCompanion(room) : null;
        bool companionSurvived = companion is { IsAlive: true } && !_companionDiedThisCombat;
        Log.Info("[LibraryOfRuina.RoadHome] AfterCombatEnd: Mode=" + Mode
            + " companionSurvived=" + companionSurvived
            + " combatWon=" + (room.CombatState?.Players.All(p => p.Creature.IsAlive) ?? false));

        if (Mode == RoadHomePageMode.Courage && _courageMaxHpGainedThisCombat > 0 && Owner?.Creature != null)
        {
            await CreatureCmd.LoseMaxHp(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature,
                _courageMaxHpGainedThisCombat,
                isFromCard: false);
        }

        if (companionSurvived)
        {
            CompanionCurrentHp = companion!.CurrentHp;
        }

        if (companionSurvived && CombatWasWon(room))
        {
            CompanionStrengthBonus += CompanionGrowthStrength;
            CompanionDexterityBonus += CompanionGrowthDexterity;
            CompanionMaxHpBonus += CompanionGrowthMaxHp;
            Log.Info("[LibraryOfRuina.RoadHome] Companion growth: Str=" + CompanionStrengthBonus
                + " Dex=" + CompanionDexterityBonus
                + " MaxHp=" + CompanionMaxHpBonus
                + " CurrentHp=" + CompanionCurrentHp
                + " (combatWon, companionSurvived=true)");
            Flash();
        }

        CourageUsedThisCombat = false;
        _courageMaxHpGainedThisCombat = 0;
        UpdateModeUiState();
    }

    private static bool CombatWasWon(CombatRoom room)
    {
        return room.CombatState?.Players.Any(p => p.Creature.IsAlive) ?? false;
    }

    private bool CanUseCourageRightClick()
    {
        return Mode == RoadHomePageMode.Courage
            && Owner?.Creature is { IsAlive: true, CombatState: not null }
            && CombatManager.Instance.IsInProgress
            && CourageUsesRemaining > 0
            && !CourageUsedThisCombat;
    }

    private async Task SummonCompanionIfNeeded()
    {
        if (Owner?.Creature?.CombatState == null || !Owner.Creature.IsAlive)
        {
            return;
        }

        var combatState = Owner.Creature.CombatState;
        bool alreadyPresent = combatState.Creatures.Any(creature =>
            creature.IsAlive
            && creature.Monster is ScaredyCatCompanion companion
            && companion.IsOwnedBy(Owner));
        if (!alreadyPresent)
        {
            var companionMonster = (ScaredyCatCompanion)ModelDb.Monster<ScaredyCatCompanion>().ToMutable();
            companionMonster.ConfigureFromPageOwner(
                Owner,
                CompanionStrengthBonus,
                CompanionDexterityBonus,
                GetCompanionMaxHp(),
                GetCompanionCurrentHp());
            Creature companion = combatState.CreateCreature(companionMonster, CombatSide.Player, null);
            await CreatureCmd.Add(companion);
            await companionMonster.InitializeSummonedCompanion();
            if (companion.Monster?.NextMove.Id == "UNSET_MOVE")
            {
                companion.PrepareForNextTurn(combatState.Enemies);
                if (NCombatRoom.Instance?.GetCreatureNode(companion) is { } creatureNode)
                {
                    await creatureNode.RefreshIntents();
                }
            }
            PositionCompanionNode(companion);
        }
    }

    private void PositionCompanionNode(Creature companion)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(companion) is not { } node)
        {
            return;
        }

        var playerNode = NCombatRoom.Instance.GetCreatureNode(Owner.Creature);
        if (playerNode == null)
        {
            return;
        }

        node.GlobalPosition = new Vector2(
            playerNode.Body.GlobalPosition.X + CompanionSpawndeltaX,
            playerNode.Body.GlobalPosition.Y + CompanionSpawndeltaY);
    }

    internal void RecordCompanionCurrentHp(int currentHp)
    {
        if (Mode != RoadHomePageMode.CompanionRoad || _companionDiedThisCombat)
        {
            return;
        }

        CompanionCurrentHp = Math.Clamp(currentHp, 1, GetCompanionMaxHp());
        UpdateModeUiState();
    }

    internal void RecordCompanionDeath(int maxHpAtDeath)
    {
        if (Mode != RoadHomePageMode.CompanionRoad || _companionDiedThisCombat)
        {
            return;
        }

        int oldMaxHp = Math.Max(1, maxHpAtDeath);
        int newMaxHp = Math.Max(1, (int)Math.Ceiling(oldMaxHp * 0.5m));
        CompanionMaxHpBonus = newMaxHp - ScaredyCatCompanion.BaseHp;
        CompanionCurrentHp = newMaxHp;
        _companionDiedThisCombat = true;
        Log.Info("[LibraryOfRuina.RoadHome] Companion died in combat: oldMaxHp=" + oldMaxHp
            + " newMaxHp=" + newMaxHp
            + " nextCombatHp=" + CompanionCurrentHp);
        UpdateModeUiState();
        Flash();
    }

    private int GetCompanionMaxHp() =>
        Math.Max(1, ScaredyCatCompanion.BaseHp + CompanionMaxHpBonus);

    private int GetCompanionCurrentHp()
    {
        int maxHp = GetCompanionMaxHp();
        return CompanionCurrentHp <= 0 ? maxHp : Math.Clamp(CompanionCurrentHp, 1, maxHp);
    }

    private Creature? FindOwnedCompanion(CombatRoom room)
    {
        if (room.CombatState == null)
        {
            Log.Info("[LibraryOfRuina.RoadHome] FindOwnedCompanion: CombatState is null");
            return null;
        }

        if (Owner == null)
        {
            Log.Info("[LibraryOfRuina.RoadHome] FindOwnedCompanion: Owner is null");
            return null;
        }

        foreach (Creature creature in room.CombatState.Creatures)
        {
            if (creature.Monster is not ScaredyCatCompanion companion)
            {
                continue;
            }

            bool isOwned = companion.IsOwnedBy(Owner);
            Log.Info("[LibraryOfRuina.RoadHome] FindOwnedCompanion: creature=" + creature.Name
                + " IsAlive=" + creature.IsAlive
                + " CurrentHp=" + creature.CurrentHp
                + " IsDead=" + creature.IsDead
                + " Side=" + creature.Side
                + " relicOwner.NetId=" + Owner.NetId
                + " isOwned=" + isOwned);

            if (isOwned)
            {
                return creature;
            }
        }

        Log.Info("[LibraryOfRuina.RoadHome] FindOwnedCompanion: no owned companion; totalCreatures="
            + room.CombatState.Creatures.Count());
        return null;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<RoadHomeCourageChoiceCard>(Owner),
            Owner.RunState.CreateCard<RoadHomeCompanionRoadChoiceCard>(Owner),
            Owner.RunState.CreateCard<RoadHomeHomeChoiceCard>(Owner)
        ];
    }

    private static RoadHomePageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            RoadHomeCourageChoiceCard => RoadHomePageMode.Courage,
            RoadHomeCompanionRoadChoiceCard => RoadHomePageMode.CompanionRoad,
            RoadHomeHomeChoiceCard => RoadHomePageMode.Home,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(RoadHomePageMode mode)
    {
        return mode is RoadHomePageMode.None
            or RoadHomePageMode.Courage
            or RoadHomePageMode.CompanionRoad
            or RoadHomePageMode.Home;
    }

    private static bool IsConcreteMode(RoadHomePageMode mode)
    {
        return mode is RoadHomePageMode.Courage
            or RoadHomePageMode.CompanionRoad
            or RoadHomePageMode.Home;
    }

    private void SetMode(RoadHomePageMode mode)
    {
        Mode = mode;
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
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
        RoadHomePageMode oldMode = Mode;
        Mode = RoadHomePageMode.Courage;
        CourageUsedThisCombat = false;
        _courageMaxHpGainedThisCombat = 0;
        Log.Warn("[LibraryOfRuina.PageRelic] RoadHomePageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Courage.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        DynamicVars["RemainingUses"].BaseValue = Math.Max(0, CourageUsesRemaining);
        DynamicVars["CourageExhausted"].BaseValue = CourageUsesRemaining <= 0 ? 1 : 0;
        DynamicVars["CompanionStrengthBonus"].BaseValue = CompanionStrengthBonus;
        DynamicVars["CompanionDexterityBonus"].BaseValue = CompanionDexterityBonus;
        DynamicVars["CompanionMaxHpBonus"].BaseValue = CompanionMaxHpBonus;
        DynamicVars["CompanionCurrentHp"].BaseValue = GetCompanionCurrentHp();
        DynamicVars["CompanionMaxHp"].BaseValue = GetCompanionMaxHp();
        Status = Mode == RoadHomePageMode.Courage
            && (CourageUsesRemaining <= 0 || (CombatManager.Instance.IsInProgress && CourageUsedThisCombat))
                ? RelicStatus.Disabled
                : CombatManager.Instance.IsInProgress && IsConcreteMode(Mode)
                    ? RelicStatus.Active
                    : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private void RefreshInventoryIcon()
    {
        NRelicInventory? inventory = NRun.Instance?.GlobalUi?.RelicInventory;
        if (inventory == null)
        {
            return;
        }

        foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
        {
            if (!ReferenceEquals(holder.Relic.Model, this))
            {
                continue;
            }

            holder.Relic.Icon.Texture = Icon;
            holder.Relic.Outline.Texture = IconOutline;
            break;
        }
    }
}

public enum RoadHomePageMode
{
    None = 0,
    Courage = 1,
    CompanionRoad = 2,
    Home = 3
}
