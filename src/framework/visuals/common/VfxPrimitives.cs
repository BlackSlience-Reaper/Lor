using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.framework.visuals.common;

/// <summary>
/// 程序化特效的通用图形原语：精灵、GPU 粒子、贝塞尔插值、贴图加载，以及绑定节点生命周期的等待。
/// <para>
/// 目前只有哲学层解放战的招式特效（<c>PhilosophyFloorLiberationVfx</c>）使用；其他特效里的同类副本还没有迁过来，
/// 迁移前要逐个核对参数默认值是否一致。<see cref="LoadTexture"/> 直接调用 <c>ResourceLoader.Load</c>，
/// 与 <c>helpers/GodotTextureSafety</c> 的行为不同，不能互相替换。
/// </para>
/// </summary>
internal static class VfxPrimitives
{
    internal static GpuParticles2D CreateContinuousParticles(
        Node2D parent,
        string name,
        Texture2D texture,
        Vector2 position,
        Color color,
        Material material,
        int zIndex,
        int amount,
        double lifetime,
        float scaleMin,
        float scaleMax)
    {
        var processMaterial = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point,
            Direction = new Vector3(0f, -1f, 0f),
            Spread = 180f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0f,
            InitialVelocityMax = 0f,
            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            Color = color
        };
        var particles = new GpuParticles2D
        {
            Name = name,
            Position = position,
            Texture = texture,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = false,
            Emitting = false,
            Explosiveness = 0f,
            Randomness = 0.35f,
            LocalCoords = false,
            ProcessMaterial = processMaterial,
            Material = material,
            ZIndex = zIndex,
            VisibilityRect = new Rect2(-1200f, -800f, 2400f, 1600f)
        };
        parent.AddChildSafely(particles);
        return particles;
    }

    internal static GpuParticles2D CreateBurstParticles(
        Node2D parent,
        string name,
        Texture2D texture,
        Vector2 position,
        Vector2 direction,
        Color color,
        Material material,
        int zIndex,
        int amount,
        double lifetime,
        float speedMin,
        float speedMax,
        float scaleMin,
        float scaleMax,
        float spread,
        float gravityY)
    {
        Vector2 normalizedDirection = direction.LengthSquared() <= 0.0001f
            ? Vector2.Up
            : direction.Normalized();
        var processMaterial = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point,
            Direction = new Vector3(
                normalizedDirection.X,
                normalizedDirection.Y,
                0f),
            Spread = spread,
            Gravity = new Vector3(0f, gravityY, 0f),
            InitialVelocityMin = speedMin,
            InitialVelocityMax = speedMax,
            DampingMin = 18f,
            DampingMax = 54f,
            ScaleMin = scaleMin,
            ScaleMax = scaleMax,
            AngleMin = -180f,
            AngleMax = 180f,
            Color = color
        };
        var particles = new GpuParticles2D
        {
            Name = name,
            Position = position,
            Texture = texture,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = true,
            Explosiveness = 0.92f,
            Randomness = 0.48f,
            LocalCoords = true,
            ProcessMaterial = processMaterial,
            Material = material,
            ZIndex = zIndex,
            VisibilityRect = new Rect2(-1200f, -800f, 2400f, 1600f)
        };
        parent.AddChildSafely(particles);
        particles.Restart();
        particles.Emitting = true;
        return particles;
    }

    internal static Task AwaitParticleTail(
        Node root,
        GpuParticles2D particles) =>
        GodotObject.IsInstanceValid(particles)
            ? Wait(root, particles.Lifetime + 0.08)
            : Task.CompletedTask;

    internal static Vector2 CubicBezier(
        Vector2 start,
        Vector2 firstControl,
        Vector2 secondControl,
        Vector2 end,
        float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * inverse * start
            + 3f * inverse * inverse * t * firstControl
            + 3f * inverse * t * t * secondControl
            + t * t * t * end;
    }

    internal static Sprite2D CreateSprite(
        string name,
        Texture2D texture,
        Vector2 position,
        Vector2 scale,
        Color modulate,
        Material material,
        int zIndex) => new()
    {
        Name = name,
        Texture = texture,
        Position = position,
        Scale = scale,
        Modulate = modulate,
        Material = material,
        ZIndex = zIndex
    };

    internal static Texture2D? LoadTexture(string path) =>
        ResourceLoader.Load<Texture2D>(
            path);

    internal static async Task<bool> Wait(Node node, double seconds)
    {
        if (!IsNodeActive(node))
        {
            return false;
        }

        SceneTreeTimer timer = node.GetTree().CreateTimer(seconds);
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void CompleteOnTimeout() => completion.TrySetResult(true);
        void CompleteOnTreeExit() => completion.TrySetResult(false);

        timer.Timeout += CompleteOnTimeout;
        node.TreeExiting += CompleteOnTreeExit;
        try
        {
            return await completion.Task && IsNodeActive(node);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(timer))
            {
                timer.Timeout -= CompleteOnTimeout;
            }
            if (GodotObject.IsInstanceValid(node))
            {
                node.TreeExiting -= CompleteOnTreeExit;
            }
        }
    }

    internal static bool IsNodeActive(Node node) =>
        GodotObject.IsInstanceValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion();

    internal static void QueueFree(Node node)
    {
        if (GodotObject.IsInstanceValid(node))
        {
            node.QueueFree();
        }
    }
}
