using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class GhostClubMemberRelic : YanamiRelicModel
{
    private const int TurnInterval = 4;
    private bool _triggerTurn;

    protected override string IconBaseName => "ghost_unwilling_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool ShowCounter => true;

    public override int DisplayAmount => _triggerTurn ? TurnInterval : TurnsSeenThisCombat;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TurnInterval", TurnInterval),
        new PowerVar<IntangiblePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<IntangiblePower>()
    ];

    [SavedProperty]
    public int TurnsSeenThisCombat { get; private set; }

    public override Task BeforeCombatStart()
    {
        _triggerTurn = false;
        UpdateStatusAndCounter();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }

        bool shouldTrigger = TurnsSeenThisCombat >= TurnInterval - 1;
        TurnsSeenThisCombat = shouldTrigger ? 0 : TurnsSeenThisCombat + 1;
        _triggerTurn = shouldTrigger;
        UpdateStatusAndCounter();

        if (!shouldTrigger)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<IntangiblePower>(player.Creature, 1m, player.Creature, null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggerTurn = false;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    private void UpdateStatusAndCounter()
    {
        Status = CombatManager.Instance.IsInProgress && _triggerTurn
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}
