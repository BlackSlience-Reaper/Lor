using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.guests.BrotherhoodOfIron;

public abstract class BrotherhoodOfIronMonster : MonsterModel
{
    protected const int DazedCount = 1;
    protected const float AttackAnimDelaySeconds = 0.27f;

    internal abstract SpriteVisualProfile SpriteProfile { get; }

    public override IEnumerable<string> AssetPaths =>
        SpriteProfile.AssetPaths;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected async Task PerformAttack(
        int damage,
        int hits,
        IReadOnlyList<Creature> targets)
    {
        if (!CanContinueMove())
        {
            return;
        }

        using (new TargetedAttackLungeScope(this, targets))
        {
            var attack = DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithHitCount(hits)
                .WithAttackerAnim("Attack", AttackAnimDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash");

            if (hits > 1)
            {
                attack.OnlyPlayAnimOnce();
            }

            await attack.Execute(null);
        }
    }

    protected Task GainMoveBlock(int amount)
    {
        if (!CanContinueMove())
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(Creature, amount, ValueProp.Move, null);
    }

    protected Task GainStrength(int amount)
    {
        if (!CanContinueMove())
        {
            return Task.CompletedTask;
        }

        return PowerCmdCompat.Apply<StrengthPower>(Creature, amount, Creature, null);
    }

    protected Task AddDazedToDiscard(IEnumerable<Creature> targets)
    {
        if (!CanContinueMove())
        {
            return Task.CompletedTask;
        }

        return CardPileCmdCompat.AddToCombatAndPreview<Dazed>(
            targets,
            PileType.Discard,
            DazedCount,
            addedByPlayer: false);
    }

    protected Task TriggerCast()
    {
        if (!CanContinueMove())
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
    }

    private bool CanContinueMove() =>
        Creature is { IsAlive: true, CombatState: not null };
}
