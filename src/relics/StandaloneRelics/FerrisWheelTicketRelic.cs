using System;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.powers;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class FerrisWheelTicketRelic : YanamiRelicModel
{
    private const int PreserveLimitPerCombat = 3;

    private bool _isResolvingPendingDamage;
    private int _pendingPreservedDamage;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _isResolvingPendingDamage = false;
        _pendingPreservedDamage = 0;
    }

    protected override string IconBaseName => "ferris_wheel_ticket_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool ShowCounter => CombatManager.Instance.IsInProgress;

    public override int DisplayAmount => Math.Max(0, PreserveLimitPerCombat - PreservesUsedThisCombat);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MaxPreserves", PreserveLimitPerCombat)
    ];

    [SavedProperty]
    public int PreservesUsedThisCombat { get; private set; }

    [SavedProperty]
    public int PendingDamage { get; private set; }

    [SavedProperty]
    public bool ResolveAtNextPlayerTurnEnd { get; private set; }

    public bool CanPreserveIncomingDamage(Creature target, decimal amount)
    {
        if (_isResolvingPendingDamage || amount <= 0m || target != Owner?.Creature)
        {
            return false;
        }

        return PreservesUsedThisCombat < PreserveLimitPerCombat;
    }

    public override Task BeforeCombatStart()
    {
        ResetCombatState();
        return PreservedDamagePower.SyncFor(Owner);
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        _pendingPreservedDamage = 0;
        if (!CanPreserveIncomingDamage(target, amount))
        {
            return amount;
        }

        _pendingPreservedDamage = Math.Max(1, (int)Math.Ceiling(amount));
        return 0m;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        int preservedDamage = _pendingPreservedDamage;
        _pendingPreservedDamage = 0;
        if (preservedDamage <= 0)
        {
            return Task.CompletedTask;
        }

        // 伤害预览也会调用 Modify；仅在实际结算的后置钩子中消耗次数。
        PreservesUsedThisCombat++;
        PendingDamage += preservedDamage;
        ResolveAtNextPlayerTurnEnd = true;
        UpdateStatusAndCounter();
        return PreservedDamagePower.SyncFor(Owner);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !ResolveAtNextPlayerTurnEnd || PendingDamage <= 0)
        {
            return;
        }

        int damageToResolve = PendingDamage;
        PendingDamage = 0;
        ResolveAtNextPlayerTurnEnd = false;
        UpdateStatusAndCounter();

        _isResolvingPendingDamage = true;
        try
        {
            await PreservedDamagePower.SyncFor(Owner);

            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner.Creature,
                damageToResolve,
                ValueProp.Unpowered,
                dealer: null,
                cardSource: null);
        }
        finally
        {
            _isResolvingPendingDamage = false;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState();
        return PreservedDamagePower.SyncFor(Owner);
    }

    private void ResetCombatState()
    {
        PreservesUsedThisCombat = 0;
        PendingDamage = 0;
        ResolveAtNextPlayerTurnEnd = false;
        _isResolvingPendingDamage = false;
        _pendingPreservedDamage = 0;
        Status = RelicStatus.Active;
        InvokeDisplayAmountChanged();
    }

    private void UpdateStatusAndCounter()
    {
        Status = (PreservesUsedThisCombat >= PreserveLimitPerCombat) ? RelicStatus.Disabled : RelicStatus.Active;
        InvokeDisplayAmountChanged();
    }
}
