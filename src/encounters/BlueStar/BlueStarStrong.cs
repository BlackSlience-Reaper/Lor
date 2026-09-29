using System;
using System.Linq;
using Godot;
using LibraryOfRuina.monsters.BlueStar;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.BlueStar;

public sealed class BlueStarStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "BlueStarBGM",
        GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string AltarSlot = "blue_star_altar";
    public const string LeftFollowerSlot = "blue_star_follower_left";
    public const string MiddleFollowerSlot = "blue_star_follower_middle";
    public const string RightFollowerSlot = "blue_star_follower_right";

    public static readonly IReadOnlyList<string> FollowerSlots =
    [
        LeftFollowerSlot,
        MiddleFollowerSlot,
        RightFollowerSlot
    ];

    private const float CameraScaling = 0.82f;
    private static readonly Vector2 CameraOffset =
        Vector2.Down * 50f + Vector2.Left * 100f;

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots =>
    [
        AltarSlot,
        LeftFollowerSlot,
        MiddleFollowerSlot,
        RightFollowerSlot
    ];

    public override float GetCameraScaling() => CameraScaling;

    public override Vector2 GetCameraOffset() => CameraOffset;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<BlueStarAltar>(),
        ModelDb.Monster<BlueStarFollower>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        BlueStarAltar.AssetPathsStatic
            .Concat(BlueStarFollower.AssetPathsStatic)
            .Concat(
            [
                BlueStarAltar.BgmPath,
                "res://images/backgrounds/blue_star_strong/blue_star_strong_background.png",
                "res://scenes/backgrounds/blue_star_strong/blue_star_strong_background.tscn",
                "res://scenes/backgrounds/blue_star_strong/layers/blue_star_strong_bg_00_a.tscn"
            ])
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<BlueStarAltar>().ToMutable(), AltarSlot),
        (ModelDb.Monster<BlueStarFollower>().ToMutable(), LeftFollowerSlot),
        (ModelDb.Monster<BlueStarFollower>().ToMutable(), MiddleFollowerSlot),
        (ModelDb.Monster<BlueStarFollower>().ToMutable(), RightFollowerSlot)
    ];
}

internal static class BlueStarEncounterHelper
{
    internal static bool IsBlueStarEncounter(CombatStateLike? combatState) =>
        combatState?.RunState.CurrentRoom is CombatRoom
        {
            Encounter: BlueStarStrong
        };

    internal static BlueStarAltar? FindAltar(CombatStateLike? combatState) =>
        combatState?.Creatures
            .Select(static creature => creature.Monster)
            .OfType<BlueStarAltar>()
            .FirstOrDefault(altar => altar.Creature.IsAlive);

    internal static IReadOnlyList<Creature> LivingFollowers(
        CombatStateLike? combatState) =>
        combatState?.Creatures
            .Where(static creature =>
                creature.IsAlive && creature.Monster is BlueStarFollower)
            .OrderBy(static creature => FollowerSlotOrder(creature.SlotName))
            .ToArray()
        ?? [];

    internal static string? FirstMissingFollowerSlot(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return null;
        }

        HashSet<string> occupied = combatState.Creatures
            .Where(static creature =>
                creature.IsAlive && creature.Monster is BlueStarFollower)
            .Select(static creature => creature.SlotName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        return BlueStarStrong.FollowerSlots.FirstOrDefault(
            slot => !occupied.Contains(slot));
    }

    internal static bool IsNovaRound(CombatStateLike? combatState) =>
        combatState != null && IsNovaRound(combatState.RoundNumber);

    internal static bool IsNovaRound(int roundNumber) =>
        roundNumber > 0
        && roundNumber % BlueStarAltar.NovaInterval == 0;

    internal static BlueStarFollowerRole ResolveFollowerRole(string? slotName) =>
        slotName switch
        {
            BlueStarStrong.MiddleFollowerSlot => BlueStarFollowerRole.Middle,
            BlueStarStrong.RightFollowerSlot => BlueStarFollowerRole.Right,
            _ => BlueStarFollowerRole.Left
        };

    internal static int FollowerSlotOrder(string? slotName) =>
        ResolveFollowerRole(slotName) switch
        {
            BlueStarFollowerRole.Left => 0,
            BlueStarFollowerRole.Middle => 1,
            _ => 2
        };
}

public enum BlueStarFollowerRole
{
    Left,
    Middle,
    Right
}
