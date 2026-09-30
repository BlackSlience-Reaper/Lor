using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.PunishingBird;

public sealed partial class PunishingBirdCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(PunishingBird))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -12f), new(2.2f, 2.2f), -190f, -420f, 190f, 16f, new(0f, -190f), new(-90f, -435f))
    {
        StateDisplayLiftY = 32f,
    };

    private static readonly Vector2 BirdPosition = new(-90f, -290f);
    private static readonly Vector2 BirdScale = new(1f, 1f);
    private static readonly Vector2 BranchPosition = new(0f, -155f);
    private static readonly Vector2 BranchScale = new(1.3f, 1.3f);
    private static readonly Vector2 CageSuspendedPosition = new(-90f, -680f);
    private static readonly Vector2 CageAssemblyScale = new(0.95f, 0.95f);
    private static readonly Vector2 CageBodyPosition = new(158.51498f, 42.52468f);
    private static readonly Vector2 CageAssemblyPosition = new(
        CageSuspendedPosition.X - CageBodyPosition.X * CageAssemblyScale.X,
        CageSuspendedPosition.Y - CageBodyPosition.Y * CageAssemblyScale.Y);
    private static readonly Rect2 CageBodyRegion = new(493.0429f, 145.07612f, 290.94415f, 334.89713f);
    private static readonly Rect2 ChainBodyRegion = new(612.0283f, 0f, 53.95874f, 186.92389f);
    private static readonly Rect2 ChainOneRegion = new(528.02673f, 37.06039f, 218.93701f, 254.86348f);
    private static readonly Rect2 ChainTwoRegion = new(222.05132f, 0f, 410.87256f, 158.94363f);
    private static readonly Rect2 ChainThreeRegion = new(638.0761f, 0f, 321.9239f, 191.98709f);
    private static readonly Vector2[][] ChainBreakOrigins =
    [
        [new(225.7f, -184.2f), new(382.3f, -102.4f)],
        [new(47f, -175.7f), new(-137.4f, -148.7f)],
        [new(100f, -124.4f), new(191.3f, -147f)]
    ];
    private static readonly Vector2[] ShardOffsets =
    [
        new(-12f, -6f),
        new(8f, -12f),
        new(-5f, 8f),
        new(14f, 5f),
        new(-15f, 4f),
        new(5f, -8f),
        new(-3f, 12f),
        new(12f, 9f)
    ];
    private static readonly Vector2[] ShardVelocities =
    [
        new(-95f, 110f),
        new(-45f, 150f),
        new(35f, 120f),
        new(90f, 160f),
        new(-75f, 130f),
        new(-20f, 175f),
        new(55f, 145f),
        new(110f, 125f)
    ];
    private static readonly float[] ShardScales =
        [0.40f, 0.48f, 0.36f, 0.44f, 0.46f, 0.38f, 0.50f, 0.42f];
    private static readonly float[] ShardRotations =
        [-1.7f, 1.25f, -0.9f, 1.8f, -1.35f, 0.85f, -1.95f, 1.45f];

    private const float CageCoverY = -305f;
    private const float CageDropDurationSeconds = 1.2f;
    private const float ChainBreakDurationSeconds = 0.78f;

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private Texture2D[] _chainShardTextures = [];
    private Sprite2D[] _chainSprites = [];
    private Node2D? _cageAssembly;
    private Sprite2D? _cage;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        AddCageLayer();
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                PunishingBird.IdleTexturePath)
            .At(BirdPosition.X, BirdPosition.Y)
            .Scale(BirdScale.X, BirdScale.Y);
        profile.Frame(
            "peck",
            PunishingBird.PeckTexturePath);
        profile.Frame(
            "punish",
            PunishingBird.PunishTexturePath);
        profile.Frame(
            "hit",
            PunishingBird.HitTexturePath);
        profile.Frame(
            "branch",
            PunishingBird.BranchTexturePath);
        profile.Frame(
            "cage_body",
            PunishingBird.CageBodyAtlasTexturePath);
        profile.Frame(
            "chain_body",
            PunishingBird.ChainBodyAtlasTexturePath);
        profile.Frame(
            "chain_1",
            PunishingBird.ChainOneAtlasTexturePath);
        profile.Frame(
            "chain_2",
            PunishingBird.ChainTwoAtlasTexturePath);
        profile.Frame(
            "chain_3",
            PunishingBird.ChainThreeAtlasTexturePath);
        profile.Frame(
            "chain_shard_1",
            PunishingBird.ChainShardOneTexturePath);
        profile.Frame(
            "chain_shard_2",
            PunishingBird.ChainShardTwoTexturePath);

        profile.Swap("peck", 0.42f, "Peck", "Attack");
        profile.Swap("punish", 0.72f, "Punish");
        profile.Swap("hit", 0.30f, "Hit");
        return profile;
    }

    private void AddCageLayer()
    {
        Texture2D branch = GetProfileTexture("branch");
        Texture2D cageBodyAtlas = GetProfileTexture("cage_body");
        Texture2D chainBodyAtlas = GetProfileTexture("chain_body");
        Texture2D chainOneAtlas = GetProfileTexture("chain_1");
        Texture2D chainTwoAtlas = GetProfileTexture("chain_2");
        Texture2D chainThreeAtlas = GetProfileTexture("chain_3");
        _chainShardTextures =
        [
            GetProfileTexture("chain_shard_1"),
            GetProfileTexture("chain_shard_2")
        ];

        var layer = new Node2D { Name = "CageLayer" };
        layer.AddChild(new Sprite2D
        {
            Name = "Branch",
            Texture = branch,
            Position = BranchPosition,
            Scale = BranchScale,
            ZIndex = 0
        });

        _cageAssembly = new Node2D
        {
            Name = "CageAssembly",
            Position = CageAssemblyPosition,
            Scale = CageAssemblyScale
        };
        layer.AddChild(_cageAssembly);

        _cage = AddCagePart(_cageAssembly, "Cage", cageBodyAtlas, CageBodyRegion, CageBodyPosition, 4);
        AddCagePart(
            _cageAssembly,
            "ChainBody",
            chainBodyAtlas,
            ChainBodyRegion,
            new Vector2(159.00769f, -176.53806f),
            5);
        Sprite2D chainOne = AddCagePart(
            _cageAssembly,
            "Chain1st",
            chainOneAtlas,
            ChainOneRegion,
            new Vector2(157.49524f, -105.507866f),
            6);
        Sprite2D chainTwo = AddCagePart(
            _cageAssembly,
            "Chain2nd",
            chainTwoAtlas,
            ChainTwoRegion,
            new Vector2(-52.512398f, -190.52818f),
            7);
        Sprite2D chainThree = AddCagePart(
            _cageAssembly,
            "Chain3rd",
            chainThreeAtlas,
            ChainThreeRegion,
            new Vector2(319.03806f, -174.00645f),
            8);
        _chainSprites = [chainThree, chainTwo, chainOne];
        AddChild(layer);
    }

    private static Sprite2D AddCagePart(
        Node parent,
        string name,
        Texture2D atlas,
        Rect2 region,
        Vector2 position,
        int zIndex)
    {
        var sprite = new Sprite2D
        {
            Name = name,
            Texture = new AtlasTexture
            {
                Atlas = atlas,
                Region = region
            },
            Position = position,
            ZIndex = zIndex
        };
        parent.AddChild(sprite);
        return sprite;
    }

    public static async Task PlayChainBreak(Creature creature, int segmentIndex)
    {
        if (CombatQueries.CreatureNodeOf(creature)?.Visuals is not PunishingBirdCreatureVisuals visuals)
        {
            return;
        }

        await visuals.PlayChainBreakInternal(segmentIndex);
    }

    private async Task PlayChainBreakInternal(int segmentIndex)
    {
        if (segmentIndex < 0
            || segmentIndex >= _chainSprites.Length
            || _chainShardTextures.Length == 0
            || _cageAssembly == null
            || !IsInstanceValid(_cageAssembly))
        {
            return;
        }

        Sprite2D chain = _chainSprites[segmentIndex];
        if (!IsInstanceValid(chain) || !chain.Visible)
        {
            return;
        }

        chain.Visible = false;
        var burst = new Node2D { Name = $"ChainBreakBurst{segmentIndex + 1}" };
        _cageAssembly.AddChild(burst);
        Vector2[] origins = ChainBreakOrigins[segmentIndex];

        for (int index = 0; index < ShardOffsets.Length; index++)
        {
            Vector2 origin = origins[index % origins.Length];
            var shard = new Sprite2D
            {
                Name = $"Shard{index + 1}",
                Texture = _chainShardTextures[index % _chainShardTextures.Length],
                Position = origin + ShardOffsets[index],
                Scale = Vector2.One * ShardScales[index],
                Rotation = index % 2 == 0 ? -0.18f : 0.16f,
                ZIndex = 9
            };
            burst.AddChild(shard);

            Tween tween = CreateTween();
            tween.SetParallel();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.In);
            tween.TweenProperty(
                shard,
                "position",
                shard.Position + ShardVelocities[index],
                ChainBreakDurationSeconds);
            tween.TweenProperty(
                shard,
                "rotation",
                shard.Rotation + ShardRotations[index],
                ChainBreakDurationSeconds);
            tween.TweenProperty(
                    shard,
                    "modulate:a",
                    0f,
                    ChainBreakDurationSeconds * 0.45f)
                .SetDelay(ChainBreakDurationSeconds * 0.55f);
        }

        await Wait(this, ChainBreakDurationSeconds);
        if (IsInstanceValid(burst))
        {
            burst.QueueFree();
        }
    }

    public static async Task PlayCageDrop(Creature creature)
    {
        if (CombatQueries.CreatureNodeOf(creature)?.Visuals is not PunishingBirdCreatureVisuals visuals)
        {
            return;
        }

        await visuals.PlayCageDropInternal();
    }

    private async Task PlayCageDropInternal()
    {
        if (_cage == null
            || _cageAssembly == null
            || !IsInstanceValid(_cage)
            || !IsInstanceValid(_cageAssembly))
        {
            return;
        }

        float targetLocalY = (CageCoverY - _cageAssembly.Position.Y) / _cageAssembly.Scale.Y;
        var completion = new TaskCompletionSource();
        void CompleteOnTreeExit() => completion.TrySetResult();

        TreeExiting += CompleteOnTreeExit;
        try
        {
            Tween tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.In);
            tween.TweenProperty(_cage, "position:y", targetLocalY, CageDropDurationSeconds);
            tween.TweenCallback(Callable.From(completion.TrySetResult));
            await completion.Task;
        }
        finally
        {
            if (IsInstanceValid(this))
            {
                TreeExiting -= CompleteOnTreeExit;
            }
        }
    }

    private static Task Wait(Node node, double seconds)
    {
        SceneTreeTimer? timer = node.GetTree()?.CreateTimer(seconds);
        if (timer == null)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource();
        timer.Timeout += () => completion.TrySetResult();
        return completion.Task;
    }
}

public sealed partial class ForestKeeperBirdCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ForestKeeperBirdLeft))]
    [MonsterVisual(typeof(ForestKeeperBirdRight))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.44f, 0.44f), -150f, -320f, 150f, 12f, new(0f, -148f), new(0f, -350f))
    {
        StateDisplayLiftY = 20f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                ForestKeeperBirdBase.IdleTexturePath)
            .Scale(0.594f)
            .Flip();
        profile.Frame("thrust", ForestKeeperBirdBase.ThrustTexturePath)
            .Scale(0.594f);
        profile.Frame("slash", ForestKeeperBirdBase.SlashTexturePath)
            .Scale(0.594f);
        profile.Frame("hit", ForestKeeperBirdBase.HitTexturePath)
            .Scale(0.594f);
        profile.Swap("thrust", 0.36f, "Thrust", "Attack");
        profile.Swap("slash", 0.42f, "Slash");
        profile.Swap("hit", 0.28f, "Hit");
        return profile;
    }
}
