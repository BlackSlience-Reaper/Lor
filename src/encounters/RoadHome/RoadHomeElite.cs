using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.cards.RoadHome;
using LibraryOfRuina.monsters.RoadHome;
using LibraryOfRuina.monsters.ScaredyCat;
using LibraryOfRuina.powers.RoadHome;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.RoadHome;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.RoadHome;

public sealed class RoadHomeElite : EncounterModel
{
    private const string EndedByHouseDeathSaveKey = "endedByHouseDeath";
    private const string CompletingSuccessfulCleanupSaveKey = "completingSuccessfulCleanup";

    public const string HouseSlot = "road_home_house_left";
    public const string RoadHomeSlot = "road_home_right";
    public const string ScaredyCatSlot = "scaredy_cat_center";
    public const string EncounterScenePath = "res://scenes/encounters/road_home_elite.tscn";

    public bool EndedByHouseDeath { get; private set; }

    public bool CompletingSuccessfulCleanup { get; private set; }

    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override bool ShouldGiveRewards => !EndedByHouseDeath;

    public override IReadOnlyList<string> Slots => [HouseSlot, ScaredyCatSlot, RoadHomeSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<RoadHomeHouse>(),
        ModelDb.Monster<monsters.RoadHome.RoadHome>(),
        ModelDb.Monster<ScaredyCat>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        RoadHomeEncounterHelper.SharedAssetPaths
            .Concat(monsters.RoadHome.RoadHome.StaticAssetPaths)
            .Concat(ScaredyCat.StaticAssetPaths)
            .Concat(RoadHomeHouse.StaticAssetPaths)
            .Concat(ScaredyCatCompanion.StaticAssetPaths)
            .Append(EncounterScenePath)
            .Distinct();

    public override float GetCameraScaling() => 0.86f;

    public override Vector2 GetCameraOffset() => new(-70f, 60f);

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        EndedByHouseDeath = false;
        CompletingSuccessfulCleanup = false;
        return
        [
            (ModelDb.Monster<RoadHomeHouse>().ToMutable(), HouseSlot),
            (ModelDb.Monster<ScaredyCat>().ToMutable(), ScaredyCatSlot),
            (ModelDb.Monster<monsters.RoadHome.RoadHome>().ToMutable(), RoadHomeSlot)
        ];
    }

    public void MarkEndedByHouseDeath()
    {
        EndedByHouseDeath = true;
    }

    public void MarkSuccessfulCompletion()
    {
        EndedByHouseDeath = false;
        CompletingSuccessfulCleanup = true;
    }

    public override Dictionary<string, string> SaveCustomState()
    {
        return new Dictionary<string, string>
        {
            [EndedByHouseDeathSaveKey] = EndedByHouseDeath.ToString(),
            [CompletingSuccessfulCleanupSaveKey] = CompletingSuccessfulCleanup.ToString()
        };
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        var bag = new EncounterStateBag(state);
        EndedByHouseDeath = bag.ReadBool(EndedByHouseDeathSaveKey);
        CompletingSuccessfulCleanup = bag.ReadBool(CompletingSuccessfulCleanupSaveKey);
    }
}

internal static class RoadHomeEncounterHelper
{
    public const string TextureRoot = "res://images/monsters/road_home/";
    public const string CatTextureRoot = "res://images/monsters/scaredy_cat/";
    public const string SfxRoot = "res://audio/sfx/road_home/";

    public const string BadWizardPassiveIconPath = "res://images/powers/road_home_bad_wizard_passive_power.png";
    public const string FriendPassiveIconPath = "res://images/powers/road_home_friend_passive_power.png";
    public const string CatCourageIconPath = "res://images/powers/scaredy_cat_courage_power.png";
    public const string CatCowardIconPath = "res://images/powers/scaredy_cat_coward_power.png";
    public const string CompanionCowardIconPath = "res://images/powers/scaredy_cat_companion_coward_power.png";
    public const string HouseProtectionIconPath = "res://images/powers/road_home_house_protection_power.png";

    private static readonly string[] PowerIconPaths =
    [
        BadWizardPassiveIconPath,
        FriendPassiveIconPath,
        CatCourageIconPath,
        CatCowardIconPath,
        CompanionCowardIconPath,
        HouseProtectionIconPath
    ];

    public static readonly string[] SharedAssetPaths =
    [
        "res://images/backgrounds/road_home_elite/background.png",
        "res://scenes/backgrounds/road_home_elite/road_home_elite_background.tscn",
        "res://scenes/backgrounds/road_home_elite/layers/road_home_elite_bg_00_a.tscn",
        ..PowerIconPaths,
        RoadHomePageRelic.IconPath,
        ..RoadHomePageChoiceCardBase.PortraitResourcePaths,
        monsters.RoadHome.RoadHome.AttackSfxPath,
        monsters.RoadHome.RoadHome.HouseAttackSfxPath,
        monsters.RoadHome.RoadHome.HouseExplosionSfxPath,
        monsters.RoadHome.RoadHome.NormalAttackSfxPath,
        monsters.RoadHome.RoadHome.YellowBrickRoadSfxPath,
        ScaredyCat.LionAttackSfxPath,
        ScaredyCat.LionPotionSfxPath,
        ScaredyCat.LionChangeSfxPath
    ];

    public static bool IsRoadHomeElite(CombatStateLike? combatState) =>
        combatState?.RunState.CurrentRoom is CombatRoom { Encounter: RoadHomeElite };

    public static Creature? FindRoadHome(CombatStateLike? combatState) =>
        combatState?.Creatures.FirstOrDefault(static creature => creature.IsAlive && creature.Monster is monsters.RoadHome.RoadHome);

    public static Creature? FindCat(CombatStateLike? combatState) =>
        combatState?.Creatures.FirstOrDefault(static creature => creature.IsAlive && creature.Monster is ScaredyCat);

    public static Creature? FindHouse(CombatStateLike? combatState) =>
        combatState?.Creatures.FirstOrDefault(static creature => creature.IsAlive && creature.Monster is RoadHomeHouse);

    public static RoadHomeElite? GetEncounter(CombatStateLike? combatState) =>
        combatState?.RunState.CurrentRoom is CombatRoom { Encounter: RoadHomeElite encounter }
            ? encounter
            : null;

    public static async Task OnRoadHomeTookDamage(Creature roadHome, int hpDamage)
    {
        if (hpDamage <= 0 || roadHome.CombatState == null)
        {
            return;
        }

        if (FindCat(roadHome.CombatState)?.Monster is ScaredyCat cat)
        {
            await cat.ApplyCourage();
        }
    }

    public static async Task OnRoadHomeDefeated(PlayerChoiceContext choiceContext, Creature roadHome)
    {
        CombatStateLike? combatState = roadHome.CombatState;
        if (combatState == null)
        {
            return;
        }

        RoadHomeElite? encounter = GetEncounter(combatState);
        if (encounter?.EndedByHouseDeath == true)
        {
            return;
        }

        if (FindCat(combatState)?.Monster is ScaredyCat cat)
        {
            await cat.EnterAfterRoadHomeDefeatedState(choiceContext);
            return;
        }

        AddPageRewards(roadHome);
        encounter?.MarkSuccessfulCompletion();
    }

    public static async Task OnCatDefeated(PlayerChoiceContext choiceContext, Creature cat)
    {
        CombatStateLike? combatState = cat.CombatState;
        if (combatState == null || FindRoadHome(combatState) != null)
        {
            return;
        }

        RoadHomeElite? encounter = GetEncounter(combatState);
        if (encounter?.EndedByHouseDeath == true)
        {
            return;
        }

        AddPageRewards(cat);
        encounter?.MarkSuccessfulCompletion();

        // Creature? house = FindHouse(combatState);
        // if (house != null && house.IsAlive)
        // {
        //     await CreatureCmd.Kill(house, force: true);
        // }

        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    public static async Task OnHouseDefeated(PlayerChoiceContext choiceContext, Creature house, bool wasRemovalPrevented)
    {
        if (wasRemovalPrevented || house.CombatState == null)
        {
            return;
        }

        CombatStateLike combatState = house.CombatState;
        RoadHomeElite? encounter = GetEncounter(combatState);
        if (encounter == null || encounter.CompletingSuccessfulCleanup)
        {
            return;
        }

        if (!house.HasPower<RoadHomeHouseProtectionPower>()
            || (FindRoadHome(combatState) == null && FindCat(combatState) == null))
        {
            return;
        }

        encounter.MarkEndedByHouseDeath();
        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (enemy.IsDead)
            {
                continue;
            }

            await CreatureCmd.Kill(enemy, force: true);
        }

        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    private static void AddPageRewards(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || room.Encounter is not RoadHomeElite)
        {
            return;
        }

        string titleLocKey = $"{ModelDb.GetId<RoadHomePageRelic>().Entry}.title";
        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<RoadHomePageRelic>(room, titleLocKey);
    }
}
