using System.Linq;
using LibraryOfRuina.combat;
using LibraryOfRuina.monsters;
using LibraryOfRuina.powers.LittleRedMercenary;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.reverberation;

/// <summary>不速之客共用友方司书站位及回合 Provider 接入契约。</summary>
public abstract class ReverberationEncounterModel : EncounterModel
{
    public const string LibrarianSlot = "librarian_friend";

    protected abstract IReadOnlyList<string> EnemySlots { get; }

    public sealed override IReadOnlyList<string> Slots => [.. EnemySlots, LibrarianSlot];

    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    public virtual Creature? FindLibrarian(CombatStateLike state) =>
        state.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.SlotName == LibrarianSlot);
}

/// <summary>有真实司书模型后注册具体子类；空槽本身不生成单位或占用回合。</summary>
public abstract class ReverberationLibrarianTurnProvider<TMonster> : IAllyTurnProvider<TMonster>
    where TMonster : LorMonsterModel
{
    public string AllyId => typeof(TMonster).Name;

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public bool CanTransferBlock => true;

    public bool IsActiveEncounter(CombatStateLike state) =>
        state.Encounter is ReverberationEncounterModel && FindAlly(state) != null;

    public Creature? FindAlly(CombatStateLike state) =>
        (state.Encounter as ReverberationEncounterModel)?.FindLibrarian(state)
            is { Monster: TMonster } librarian ? librarian : null;

    public void OnCombatReset(Creature? creature) => TransferredBlockPower.Clear(creature);
}
