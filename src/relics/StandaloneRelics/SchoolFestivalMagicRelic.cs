using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class SchoolFestivalMagicRelic : YanamiRelicModel
{
    private const int TurnInterval = 6;
    private const int StunTurns = 1;
    private bool _triggerTurn;

    protected override string IconBaseName => "school_festival_magic_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool ShowCounter => true;

    public override int DisplayAmount => _triggerTurn ? TurnInterval : TurnsSeenAcrossCombats;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TurnInterval", TurnInterval),
        new DynamicVar("StunTurns", StunTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Stun)
    ];

    [SavedProperty]
    public int TurnsSeenAcrossCombats { get; private set; }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }

        bool shouldTrigger = TurnsSeenAcrossCombats >= TurnInterval - 1;
        TurnsSeenAcrossCombats = shouldTrigger ? 0 : TurnsSeenAcrossCombats + 1;
        _triggerTurn = shouldTrigger;
        UpdateStatusAndCounter();

        if (!shouldTrigger)
        {
            return;
        }

        if (player.Creature?.CombatState is not { } combatState)
        {
            return;
        }

        var enemies = AllyTurnRegistry.FilterPlayerEnemyTargets(combatState.Enemies)
            .Where(static enemy => enemy.IsAlive)
            .ToList();
        if (enemies.Count == 0)
        {
            return;
        }

        Flash();
        foreach (var enemy in enemies)
        {
            await CreatureCmd.Stun(enemy);
        }
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
