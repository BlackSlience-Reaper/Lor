using System;
using System.Linq;
using LibraryOfRuina.acts;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.DespairKnight;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.KingOfGreed;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.encounters.PhilosophyFloorLiberation;
using LibraryOfRuina.encounters.QueenOfHatred;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.encounters.WrathServant;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.encounters;

/// <summary>
/// 一个楼层解放的路由规则：所在的图书馆幕、固定的解放遭遇、在第几幕（从 0 计）作为章节 Boss、
/// 高进阶双 Boss 时的第二 Boss，以及它当章节 Boss 时对异想体遭遇抽取权重的倍率。
/// 普通类，不是模型，不产生模型 ID；描述对象只在 <see cref="LiberationFloors"/> 里静态登记。
/// </summary>
internal sealed class LiberationFloorDescriptor
{
    private readonly Func<EncounterModel> _encounter;
    private readonly Func<EncounterModel>? _doubleBossSecondEncounter;
    private readonly (Type EncounterType, double Multiplier)[] _priorityWeightMultipliers;

    internal LiberationFloorDescriptor(
        string floor,
        Type actType,
        Type encounterType,
        Func<EncounterModel> encounter,
        int bossActIndex,
        Func<EncounterModel>? doubleBossSecondEncounter = null,
        (Type EncounterType, double Multiplier)[]? priorityWeightMultipliers = null)
    {
        Floor = floor;
        ActType = actType;
        EncounterType = encounterType;
        _encounter = encounter;
        BossActIndex = bossActIndex;
        _doubleBossSecondEncounter = doubleBossSecondEncounter;
        _priorityWeightMultipliers = priorityWeightMultipliers ?? [];
    }

    public string Floor { get; }

    /// <summary>
    /// 该层对应的图书馆幕类型。幕自己的 <c>FixedBoss</c> 仍是固定 Boss 的来源，
    /// 这里与之一一对应，由验证套件核对。
    /// </summary>
    public Type ActType { get; }

    public Type EncounterType { get; }

    public EncounterModel Encounter => _encounter();

    /// <summary>
    /// 只有该幕位于这个幕序号时，才按解放 Boss 固定 Boss；放在别的幕序号上走普通 Boss 重排。
    /// </summary>
    public int BossActIndex { get; }

    /// <summary>
    /// 是否登记进 Boss 池（原 <see cref="LiberationBossRegistry"/> 的逐层开关）。进程内全局、默认开启，
    /// 不存档、不进联机指纹，仓库内只有验证套件会改它；两端取值不同会让 Boss 路由分叉。
    /// </summary>
    public bool Registered { get; set; } = true;

    /// <summary>
    /// 进阶达到双 Boss、且该幕位于双 Boss 幕序号时的第二 Boss；没有则为 null。
    /// </summary>
    public EncounterModel? DoubleBossSecondEncounter => _doubleBossSecondEncounter?.Invoke();

    public bool Matches(EncounterModel? encounter) =>
        encounter != null && EncounterType.IsInstanceOfType(encounter);

    public bool IsFloorAct(ActModel? act) =>
        act != null && ActType.IsInstanceOfType(act);

    /// <summary>
    /// 该层为章节 Boss 时异想体遭遇的抽取权重倍率；按登记顺序取第一个匹配的遭遇类型，没有则为 1。
    /// </summary>
    public double PriorityWeightMultiplierFor(EncounterModel encounter)
    {
        foreach ((Type encounterType, double multiplier) in _priorityWeightMultipliers)
        {
            if (encounterType.IsInstanceOfType(encounter))
            {
                return multiplier;
            }
        }

        return 1.0;
    }
}

/// <summary>
/// 八个楼层解放的描述对象登记表。
/// </summary>
internal static class LiberationFloors
{
    // 自然层解放为章节 Boss 时，贪婪国王、憎恶皇后、绝望骑士、愤怒侍从的遭遇抽取权重提高。
    private const double NaturalFloorKingOfGreedWeightMultiplier = 2;
    private const double NaturalFloorQueenOfHatredWeightMultiplier = 1.50;
    private const double NaturalFloorDespairKnightWeightMultiplier = 1.50;
    private const double NaturalFloorWrathServantWeightMultiplier = 1.50;

    public static readonly LiberationFloorDescriptor History = new(
        "History",
        typeof(Malkuth),
        typeof(HistoryFloorLiberationEncounter),
        static () => ModelDb.Encounter<HistoryFloorLiberationEncounter>(),
        bossActIndex: 0);

    public static readonly LiberationFloorDescriptor Technology = new(
        "Technology",
        typeof(Yesod),
        typeof(TechnologyFloorLiberationEncounter),
        static () => ModelDb.Encounter<TechnologyFloorLiberationEncounter>(),
        bossActIndex: 0);

    public static readonly LiberationFloorDescriptor Literature = new(
        "Literature",
        typeof(Hod),
        typeof(LiteratureFloorLiberationEncounter),
        static () => ModelDb.Encounter<LiteratureFloorLiberationEncounter>(),
        bossActIndex: 0);

    public static readonly LiberationFloorDescriptor Art = new(
        "Art",
        typeof(NetZech),
        typeof(ArtFloorLiberationEncounter),
        static () => ModelDb.Encounter<ArtFloorLiberationEncounter>(),
        bossActIndex: 1);

    public static readonly LiberationFloorDescriptor Language = new(
        "Language",
        typeof(Gebura),
        typeof(LanguageFloorLiberationEncounter),
        static () => ModelDb.Encounter<LanguageFloorLiberationEncounter>(),
        bossActIndex: 1);

    public static readonly LiberationFloorDescriptor Natural = new(
        "Natural",
        typeof(Tiphereth),
        typeof(NaturalFloorLiberationEncounter),
        static () => ModelDb.Encounter<NaturalFloorLiberationEncounter>(),
        bossActIndex: 1,
        priorityWeightMultipliers:
        [
            (typeof(KingOfGreedElite), NaturalFloorKingOfGreedWeightMultiplier),
            (typeof(QueenOfHatredStrong), NaturalFloorQueenOfHatredWeightMultiplier),
            (typeof(DespairKnightStrong), NaturalFloorDespairKnightWeightMultiplier),
            (typeof(WrathServantStrong), NaturalFloorWrathServantWeightMultiplier)
        ]);

    // 第三幕两层互为对方的双 Boss 第二场：社会层幕（Chesed）先打社会层再打哲学层，哲学层幕（Binah）相反。
    public static readonly LiberationFloorDescriptor Social = new(
        "Social",
        typeof(Chesed),
        typeof(SocialFloorLiberationEncounter),
        static () => ModelDb.Encounter<SocialFloorLiberationEncounter>(),
        bossActIndex: 2,
        doubleBossSecondEncounter: static () => ModelDb.Encounter<PhilosophyFloorLiberationEncounter>());

    public static readonly LiberationFloorDescriptor Philosophy = new(
        "Philosophy",
        typeof(Binah),
        typeof(PhilosophyFloorLiberationEncounter),
        static () => ModelDb.Encounter<PhilosophyFloorLiberationEncounter>(),
        bossActIndex: 2,
        doubleBossSecondEncounter: static () => ModelDb.Encounter<SocialFloorLiberationEncounter>());

    public static readonly LiberationFloorDescriptor[] All =
    [
        History,
        Technology,
        Literature,
        Art,
        Language,
        Natural,
        Social,
        Philosophy
    ];

    /// <summary>
    /// 第三幕双 Boss 的候选顺序。顺序参与固定种子的洗牌，改动会改变选出的 Boss 对。
    /// </summary>
    public static readonly LiberationFloorDescriptor[] ThirdActDoubleBossCandidates =
    [
        Philosophy,
        Social
    ];

    public static LiberationFloorDescriptor? ForAct(ActModel? act) =>
        act == null ? null : All.FirstOrDefault(descriptor => descriptor.IsFloorAct(act));

    public static LiberationFloorDescriptor? ForEncounter(EncounterModel? encounter) =>
        encounter == null ? null : All.FirstOrDefault(descriptor => descriptor.Matches(encounter));

    public static LiberationFloorDescriptor? ForEncounterType(Type encounterType) =>
        All.FirstOrDefault(descriptor => descriptor.EncounterType == encounterType);
}
