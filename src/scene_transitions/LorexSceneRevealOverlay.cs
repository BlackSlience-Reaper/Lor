using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.scene_transitions;

public partial class LorexSceneRevealOverlay : Control
{
    internal const int PaperVariantCount = 16;
    private const float RevealDurationSeconds = 2f;
    private const float HoldSeconds = 0f;
    private const float PaperSpawnIntervalSeconds = 0.02f;
    private const int PapersPerSpawn = 12;
    private const int OpeningBurstSpawnCount = 1;
    private const int MaxPaperParticles = 1600;
    private const float PaperTextureSize = 128f;
    private const float PaperLeftSpawnChance = 0.78f;
    private const float PaperSpawnLeftMinDistance = 80f;
    private const float PaperSpawnLeftMaxDistance = 620f;
    private const float PaperSpawnRightMinDistance = 8f;
    private const float PaperSpawnRightMaxDistance = 72f;
    private const float PaperSpawnVerticalPadding = 80f;
    private const float PaperLeftVelocityMin = 240f;
    private const float PaperLeftVelocityMax = 520f;
    private const float PaperRightVelocityMin = -80f;
    private const float PaperRightVelocityMax = 60f;
    private const float PaperVerticalVelocity = 160f;
    private const float PaperMinLife = 0.8f;
    private const float PaperMaxLife = 1.1f;
    private const float ScanGradientWidth = 940f;
    private static readonly Color PaperTint = new(1f, 1f, 1f);

    private readonly Texture2D _oldBackground;
    private readonly TextureRect.StretchModeEnum _backgroundStretchMode;
    private readonly Texture2D[] _papers;
    private readonly Texture2D[] _paperGlows;
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<PaperParticle> _particles = new();

    private TaskCompletionSource? _completion;
    private ImageTexture? _scanTailTexture;
    private float _elapsed;
    private float _paperTimer;
    private bool _playing;

    private LorexSceneRevealOverlay(
        Texture2D oldBackground,
        TextureRect.StretchModeEnum backgroundStretchMode,
        Texture2D[] papers,
        Texture2D[] paperGlows)
    {
        _oldBackground = oldBackground;
        _backgroundStretchMode = backgroundStretchMode;
        _papers = papers;
        _paperGlows = paperGlows;

        Name = "LorexSceneRevealOverlay";
        LayoutMode = 1;
        AnchorsPreset = (int)LayoutPreset.FullRect;
        AnchorRight = 1f;
        AnchorBottom = 1f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public static LorexSceneRevealOverlay? Create(
        Texture2D oldBackground,
        TextureRect.StretchModeEnum backgroundStretchMode)
    {
        Texture2D[] papers = new Texture2D[PaperVariantCount];
        Texture2D[] paperGlows = new Texture2D[PaperVariantCount];
        for (int i = 0; i < PaperVariantCount; i++)
        {
            Texture2D? paper = LoadTexture(LorexSceneTransitionAssetPaths.GetPaperPath(i));
            Texture2D? glow = LoadTexture(LorexSceneTransitionAssetPaths.GetPaperGlowPath(i));
            if (paper == null || glow == null)
            {
                Log.Warn($"[LorexSceneTransition] Missing paper resource index={i}.");
                return null;
            }

            papers[i] = paper;
            paperGlows[i] = glow;
        }

        return new LorexSceneRevealOverlay(oldBackground, backgroundStretchMode, papers, paperGlows);
    }

    public static LorexSceneRevealOverlay? CreatePrimed(
        TextureRect backgroundImage,
        Texture2D oldBackground)
    {
        LorexSceneRevealOverlay? overlay = Create(oldBackground, backgroundImage.StretchMode);
        if (overlay == null)
        {
            return null;
        }

        backgroundImage.AddChildSafely(overlay);
        overlay.PrimeFullCover();
        overlay.MoveToFront();
        return overlay;
    }

    public Task PlayAsync()
    {
        if (_completion != null)
        {
            return _completion.Task;
        }

        _completion = new TaskCompletionSource();
        _playing = true;
        if (Size.X > 0f && Size.Y > 0f)
        {
            for (int i = 0; i < OpeningBurstSpawnCount; i++)
            {
                SpawnPapers();
            }
        }

        SetProcess(true);
        QueueRedraw();
        return _completion.Task;
    }

    public void PrimeFullCover()
    {
        _elapsed = 0f;
        _paperTimer = 0f;
        _particles.Clear();
        QueueRedraw();
    }

    public override void _Ready()
    {
        SetProcess(false);
        _rng.Randomize();
        _scanTailTexture = CreateScanTailTexture();
    }

    public override void _ExitTree()
    {
        _playing = false;
        SetProcess(false);
        _completion?.TrySetResult();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (!_playing)
        {
            return;
        }

        float dt = (float)delta;
        _elapsed += dt;
        _paperTimer -= dt;
        while (_elapsed <= RevealDurationSeconds && _paperTimer <= 0f)
        {
            _paperTimer += PaperSpawnIntervalSeconds;
            SpawnPapers();
        }

        UpdateParticles(dt);
        QueueRedraw();

        if (_elapsed >= RevealDurationSeconds + HoldSeconds && _particles.Count == 0)
        {
            _playing = false;
            SetProcess(false);
            _completion?.TrySetResult();
        }
    }

    public override void _Draw()
    {
        base._Draw();

        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        Rect2 oldRegion = GetOldBackgroundRegion();
        if (oldRegion.Size.X > 0.5f)
        {
            DrawTextureRectRegion(
                _oldBackground,
                oldRegion,
                GetTextureRegionForScreenRegion(_oldBackground, oldRegion),
                Colors.White);
        }

        if (_playing && _elapsed <= RevealDurationSeconds + HoldSeconds)
        {
            DrawScanLine();
        }

        DrawParticles();
    }

    private Rect2 GetOldBackgroundRegion()
    {
        float edgeX = GetRevealEdgeX();
        float width = Mathf.Clamp(edgeX, 0f, Size.X);
        return new Rect2(Vector2.Zero, new Vector2(width, Size.Y));
    }

    private void DrawScanLine()
    {
        float edgeX = GetRevealEdgeX();
        if (edgeX < -ScanGradientWidth || edgeX > Size.X + ScanGradientWidth)
        {
            return;
        }

        DrawScanGradient(edgeX);
    }

    private void DrawScanGradient(float edgeX)
    {
        if (_scanTailTexture == null)
        {
            return;
        }

        DrawTextureRect(
            _scanTailTexture,
            new Rect2(edgeX, 0f, ScanGradientWidth, Size.Y),
            false,
            Colors.White);
    }

    private void DrawParticles()
    {
        foreach (PaperParticle particle in _particles)
        {
            if (!GodotTextureSafety.IsValid(particle.Texture)
                || !GodotTextureSafety.IsValid(particle.GlowTexture))
            {
                continue;
            }

            Vector2 particleSize = new(Math.Max(1f, particle.Width), Math.Max(1f, particle.Height));
            Rect2 rect = new(-particleSize * 0.5f, particleSize);
            Vector2 scale = new(
                particle.FlipX ? -particle.Scale : particle.Scale,
                particle.FlipY ? -particle.Scale : particle.Scale);
            Color baseColor = PaperTint;
            baseColor.A = 0.6f * particle.Brightness;
            Color additiveColor = PaperTint;
            additiveColor.A = particle.AdditiveAlpha * particle.Brightness;
            Color glowColor = PaperTint;
            glowColor.A = particle.GlowAlpha * particle.Brightness;

            DrawSetTransform(particle.Position, Mathf.DegToRad(particle.Rotation), scale);
            DrawTextureRect(particle.Texture, rect, false, baseColor);
            DrawTextureRect(particle.Texture, rect, false, additiveColor);
            DrawTextureRect(particle.GlowTexture, rect, false, glowColor);
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void SpawnPapers()
    {
        float edgeX = GetRevealEdgeX();
        if (_particles.Count >= MaxPaperParticles)
        {
            return;
        }

        float screenScale = GetLorexScreenScale();
        for (int i = 0; i < PapersPerSpawn && _particles.Count < MaxPaperParticles; i++)
        {
            int variant = _rng.RandiRange(0, PaperVariantCount - 1);
            bool spawnOnLeft = _rng.Randf() < PaperLeftSpawnChance;
            float horizontalOffset = spawnOnLeft
                ? -_rng.RandfRange(PaperSpawnLeftMinDistance, PaperSpawnLeftMaxDistance) * screenScale
                : _rng.RandfRange(PaperSpawnRightMinDistance, PaperSpawnRightMaxDistance) * screenScale;
            float horizontalVelocity = spawnOnLeft
                ? -_rng.RandfRange(PaperLeftVelocityMin, PaperLeftVelocityMax) * screenScale
                : _rng.RandfRange(PaperRightVelocityMin, PaperRightVelocityMax) * screenScale;

            _particles.Add(new PaperParticle
            {
                Texture = _papers[variant],
                GlowTexture = _paperGlows[variant],
                Position = new Vector2(
                    edgeX + horizontalOffset,
                    _rng.RandfRange(-PaperSpawnVerticalPadding, Math.Max(1f, Size.Y + PaperSpawnVerticalPadding))),
                Velocity = new Vector2(
                    horizontalVelocity,
                    _rng.RandfRange(-PaperVerticalVelocity, PaperVerticalVelocity) * screenScale),
                Width = _rng.RandfRange(0f, PaperTextureSize) * screenScale,
                Height = _rng.RandfRange(0f, PaperTextureSize) * screenScale,
                WidthVelocity = _rng.RandfRange(-128f, 128f) * screenScale,
                HeightVelocity = _rng.RandfRange(-128f, 128f) * screenScale,
                Rotation = _rng.RandfRange(0f, 360f),
                RotationSpeed = _rng.RandfRange(-30f, 30f),
                MaxScale = _rng.RandfRange(2.2f, 2.8f) * screenScale,
                FlipX = _rng.RandiRange(0, 1) == 0,
                FlipY = _rng.RandiRange(0, 1) == 0,
                Brightness = _rng.RandfRange(0.92f, 1.08f),
                Life = _rng.RandfRange(PaperMinLife, PaperMaxLife)
            });
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            PaperParticle particle = _particles[i];
            particle.Age += dt;
            if (particle.Age >= particle.Life)
            {
                _particles.RemoveAt(i);
                continue;
            }

            float sizeLimit = PaperTextureSize * GetLorexScreenScale();
            if (particle.Width > sizeLimit)
            {
                particle.Width = sizeLimit * 2f - particle.Width;
                particle.FlipX = !particle.FlipX;
            }
            else if (particle.Width < 0f)
            {
                particle.Width = -particle.Width;
                particle.FlipX = !particle.FlipX;
            }

            if (particle.Height > sizeLimit)
            {
                particle.Height = sizeLimit * 2f - particle.Height;
                particle.FlipY = !particle.FlipY;
            }
            else if (particle.Height < 0f)
            {
                particle.Height = -particle.Height;
                particle.FlipY = !particle.FlipY;
            }

            particle.Position += particle.Velocity * dt;
            particle.Width += (particle.FlipX ? particle.WidthVelocity : -particle.WidthVelocity) * dt;
            particle.Height += (particle.FlipY ? particle.HeightVelocity : -particle.HeightVelocity) * dt;
            particle.Rotation += particle.RotationSpeed * dt;

            float third = particle.Life / 3f;
            if (particle.Age < third)
            {
                float ratio = particle.Age / third;
                particle.Scale = ratio * particle.MaxScale;
                particle.AdditiveAlpha = ratio * 0.4f + 0.2f;
                particle.GlowAlpha = ratio * 0.4f;
            }
            else if (particle.Age > third * 2f)
            {
                float ratio = (particle.Life - particle.Age) / third;
                particle.Scale = ratio * particle.MaxScale;
                particle.AdditiveAlpha = ratio * 0.4f + 0.2f;
                particle.GlowAlpha = ratio * 0.4f;
            }
            else
            {
                particle.Scale = particle.MaxScale;
                particle.AdditiveAlpha = 0.6f;
                particle.GlowAlpha = 0.4f;
            }
        }
    }

    private float GetRevealEdgeX()
    {
        float progress = Mathf.Clamp(_elapsed / RevealDurationSeconds, 0f, 1f);
        return Mathf.Lerp(Size.X, 0f, progress);
    }

    private Rect2 GetTextureRegionForScreenRegion(Texture2D texture, Rect2 screenRegion)
    {
        float textureWidth = texture.GetWidth();
        float textureHeight = texture.GetHeight();
        if (_backgroundStretchMode == TextureRect.StretchModeEnum.KeepAspectCovered)
        {
            float scale = Math.Max(SafeDivide(Size.X, textureWidth), SafeDivide(Size.Y, textureHeight));
            if (scale <= 0.001f)
            {
                return new Rect2(Vector2.Zero, new Vector2(textureWidth, textureHeight));
            }

            float visibleWidth = Size.X / scale;
            float visibleHeight = Size.Y / scale;
            float visibleX = (textureWidth - visibleWidth) * 0.5f;
            float visibleY = (textureHeight - visibleHeight) * 0.5f;
            return new Rect2(
                new Vector2(visibleX + screenRegion.Position.X / scale, visibleY + screenRegion.Position.Y / scale),
                new Vector2(screenRegion.Size.X / scale, screenRegion.Size.Y / scale));
        }

        return new Rect2(
            new Vector2(
                SafeDivide(screenRegion.Position.X, Size.X) * textureWidth,
                SafeDivide(screenRegion.Position.Y, Size.Y) * textureHeight),
            new Vector2(
                SafeDivide(screenRegion.Size.X, Size.X) * textureWidth,
                SafeDivide(screenRegion.Size.Y, Size.Y) * textureHeight));
    }

    private static float SafeDivide(float value, float divisor) =>
        Math.Abs(divisor) <= 0.001f ? 0f : value / divisor;

    private float GetLorexScreenScale() =>
        Mathf.Clamp(SafeDivide(Size.Y, 1080f), 0.58f, 1f);

    private static ImageTexture CreateScanTailTexture()
    {
        const int width = 256;
        const int height = 4;
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1);
            float alpha = Mathf.Pow(1f - t, 2.35f);
            Color color = new(1f, 1f, 1f, alpha);
            for (int y = 0; y < height; y++)
            {
                image.SetPixel(x, y, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path))
        {
            return null;
        }

        return ResourceLoader.Load<Texture2D>(path);
    }

    private sealed class PaperParticle
    {
        public required Texture2D Texture { get; init; }

        public required Texture2D GlowTexture { get; init; }

        public Vector2 Position { get; set; }

        public Vector2 Velocity { get; init; }

        public float Width { get; set; }

        public float Height { get; set; }

        public float WidthVelocity { get; init; }

        public float HeightVelocity { get; init; }

        public float Rotation { get; set; }

        public float RotationSpeed { get; init; }

        public float MaxScale { get; init; }

        public bool FlipX { get; set; }

        public bool FlipY { get; set; }

        public float Brightness { get; init; }

        public float Scale { get; set; }

        public float Life { get; init; }

        public float Age { get; set; }

        public float AdditiveAlpha { get; set; } = 0.2f;

        public float GlowAlpha { get; set; }
    }
}
