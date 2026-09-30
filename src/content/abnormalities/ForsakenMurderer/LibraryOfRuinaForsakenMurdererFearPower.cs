using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.ForsakenMurderer;

public sealed class LibraryOfRuinaForsakenMurdererFearPower : LibraryOfRuinaPowerModel
{
    private const string FearSfxPath = "res://audio/sfx/forsaken_murderer/forsaken_murderer_fear.ogg";
    private static readonly float FearSfxVolumeDb = -3f + Mathf.LinearToDb(0.8f);
    private const int StrengthDownAmount = 6;

    protected override string LegacyPowerId => "FORSAKEN_MURDERER_FEAR_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Weak", StrengthDownAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryWeakPower>()
    ];

    public override async Task AfterBlockBroken(
        PlayerChoiceContext choiceContext,
        Creature target,
        Creature? breaker)
    {
        if (target != Owner)
        {
            return;
        }

        Flash();
        LocalOggOneShotPlayer.Play(FearSfxPath, FearSfxVolumeDb);
        await LibraryPowerCmd.Apply<LibraryWeakPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            StrengthDownAmount,
            0,
            IsPermanent: false,
            Owner,
            null);
        SyncFearPresentationToStrengthDown();
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        Owner.PowerApplied += OnOwnerPowerChanged;
        Owner.PowerRemoved += OnOwnerPowerChanged;
        SyncFearPresentationToStrengthDown();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power.Owner == Owner && power is LibraryWeakPower)
        {
            SyncFearPresentationToStrengthDown();
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEndLate(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side)
        {
            SyncFearPresentationToStrengthDown();
        }

        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        oldOwner.PowerApplied -= OnOwnerPowerChanged;
        oldOwner.PowerRemoved -= OnOwnerPowerChanged;
        SetFearPresentationVisible(false, oldOwner);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Owner.PowerApplied -= OnOwnerPowerChanged;
        Owner.PowerRemoved -= OnOwnerPowerChanged;
        SetFearPresentationVisible(false);
        return Task.CompletedTask;
    }

    private void OnOwnerPowerChanged(PowerModel power)
    {
        if (power is LibraryWeakPower)
        {
            SyncFearPresentationToStrengthDown();
        }
    }

    private void SyncFearPresentationToStrengthDown()
    {
        SetFearPresentationVisible(Owner.GetPower<LibraryWeakPower>() != null);
    }

    private void SetFearPresentationVisible(bool visible, Creature? owner = null)
    {
        Creature target = owner ?? Owner;

        ForsakenMurdererFearBackgroundOverlay.SetOverlayVisible(visible);

        if (!target.IsDead && target.Monster is ForsakenMurderer murderer)
        {
            murderer.RefreshBackgroundMoonTextLoop();
        }
    }
}
