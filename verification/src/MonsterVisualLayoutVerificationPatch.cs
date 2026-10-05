using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 怪物外观目录的逐行转储，用于重构前后对照（A/B）。
/// 只经过重构前后都存在的入口：<see cref="MonsterVisualCatalog"/> 的按 ID 查询、<c>Validate</c>、<c>Create</c>，
/// 以及 <see cref="WrappedMonsterVisualFactory.ShouldWrap"/>。对 ModelDb 里的每个怪物模型（<see cref="ModelDb.All"/>，比 <see cref="ModelDb.Monsters"/> 全）记录
/// 是否登记；登记了的再记录布局全部字段、精灵配置（按引用认出是哪个外观类的 <c>Profile</c>）、静态贴图，
/// 并用可变副本实际生成外观节点，逐个节点记录类型、名字、所有者、坐标、缩放、偏移、翻转、层级、贴图与动画。
/// 几个按怪物状态选贴图的自定义工厂，再按反射改写状态字段各生成一次。
/// 每行一条 <c>ROW</c>，两次构建的 ROW 行应逐行相同；套件只在转储本身崩溃时失败，结果对照在套件外做。
/// </summary>
internal static class MonsterVisualLayoutVerificationPatch
{
    private const string VerifyArg = "lor-verify-monster-visual-layout";
    private const string LogPrefix = "[LibraryOfRuina.MonsterVisualLayout.Verify] ";
    private const string MissingId = "LOR_VERIFY_NOT_A_MONSTER";

    private static readonly BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly BindingFlags DeclaredStatic =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Regex GeneratedNodeName = new(@"@(\w+)@\d+", RegexOptions.CultureInvariant);

    /// <summary>自定义工厂按怪物状态选贴图：状态字段名与要试的取值。</summary>
    private static readonly (string MonsterType, string Field, object[] Values)[] StateVariants =
    [
        ("LibraryOfRuina.content.abnormalities.BurrowingHeaven.BurrowingHeaven", "_isAwake", [false, true]),
        ("LibraryOfRuina.content.abnormalities.BurrowingHeaven.HeavenThorn", "_isAwake", [false, true]),
        ("LibraryOfRuina.content.abnormalities.Ozma.OzmaJack", "<IsAwake>k__BackingField", [false, true]),
        ("LibraryOfRuina.content.liberation.History.HistoryFloorPhaseBoss", "_phase", [1, 2, 3, 4, 5]),
        ("LibraryOfRuina.content.liberation.Art.ArtFloorDaCapoPerformer", "_variant", [1, 2, 3, 4]),
    ];

    private static bool _started;
    private static int _rows;
    private static readonly StringBuilder Digest = new();
    private static Dictionary<SpriteVisualProfile, string>? _profileOwners;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static Task RunAsync()
    {
        try
        {
            RecordCatalog();
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Digest.ToString())))[..16];
            Log.Info(LogPrefix + "MONSTER_VISUAL_LAYOUT_OK rows=" + _rows + " digest=" + digest);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "MONSTER_VISUAL_LAYOUT_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }

        return Task.CompletedTask;
    }

    private static void Row(string text)
    {
        _rows++;
        Digest.Append(text).Append('\n');
        Log.Info(LogPrefix + "ROW " + text);
    }

    private static void RecordCatalog()
    {
        Row("validate " + Safe(() =>
        {
            MonsterVisualCatalog.Validate();
            return "ok";
        }));

        string[] registered = MonsterVisualCatalog.RegisteredIds
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        Row("registered count=" + registered.Length
            + " distinct=" + registered.Distinct(StringComparer.Ordinal).Count()
            + " hash=" + Hash(string.Join(",", registered)));
        foreach (string id in registered)
        {
            Row("registered " + id);
        }

        RecordLookups(MissingId);

        var monsterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (MonsterModel monster in VerificationApi.AllModels.OfType<MonsterModel>()
                     .OrderBy(static m => m.Id.Entry, StringComparer.Ordinal)
                     .ThenBy(static m => m.GetType().FullName, StringComparer.Ordinal))
        {
            string id = monster.Id.Entry;
            monsterIds.Add(id);
            bool contains = MonsterVisualCatalog.Contains(id);
            bool wrap = WrappedMonsterVisualFactory.ShouldWrap(monster);
            Row("monster " + id + " type=" + monster.GetType().FullName + " contains=" + contains + " wrap=" + wrap);
            if (!contains)
            {
                continue;
            }

            RecordLookups(id);
            RecordCreated(id, "default", monster, null);
            foreach ((string typeName, string field, object[] values) in StateVariants)
            {
                if (monster.GetType().FullName != typeName)
                {
                    continue;
                }

                foreach (object value in values)
                {
                    RecordCreated(id, field + "=" + value, monster, (field, value));
                }
            }
        }

        foreach (string id in registered.Where(id => !monsterIds.Contains(id)))
        {
            Row("orphan " + id);
        }
    }

    private static void RecordLookups(string id)
    {
        Row("lookup " + id
            + " contains=" + MonsterVisualCatalog.Contains(id)
            + " layout=" + Safe(() => Describe(MonsterVisualCatalog.GetLayout(id)))
            + " profile=" + Safe(() => DescribeProfile(MonsterVisualCatalog.GetRequiredProfile(id))));
    }

    private static void RecordCreated(
        string id,
        string variant,
        MonsterModel canonical,
        (string Field, object Value)? state)
    {
        MonsterModel monster;
        try
        {
            monster = (MonsterModel)canonical.ToMutable();
            if (state is { } s)
            {
                FieldInfo field = FindField(monster.GetType(), s.Field)
                    ?? throw new MissingFieldException(monster.GetType().FullName, s.Field);
                field.SetValue(
                    monster,
                    field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, s.Value) : s.Value);
            }
        }
        catch (Exception ex)
        {
            Row("create " + id + " [" + variant + "] mutable-error=" + ex.GetType().Name + ":" + ex.Message);
            return;
        }

        NCreatureVisuals? visuals = null;
        try
        {
            visuals = MonsterVisualCatalog.Create(monster);
        }
        catch (Exception ex)
        {
            Row("create " + id + " [" + variant + "] error=" + ex.GetType().Name + ":" + ex.Message);
            return;
        }

        try
        {
            Row("create " + id + " [" + variant + "] root=" + visuals.GetType().FullName
                + " state=" + DescribeDeclaredState(visuals));
            DumpNode(id, variant, visuals, visuals, "");
        }
        finally
        {
            visuals.Free();
        }
    }

    private static void DumpNode(string id, string variant, Node root, Node node, string path)
    {
        string self = path.Length == 0 ? "." : path;
        var line = new StringBuilder();
        line.Append("node ").Append(id).Append(" [").Append(variant).Append("] ").Append(self)
            .Append(" type=").Append(node.GetType().FullName)
            .Append(" class=").Append(node.GetClass())
            .Append(" name=").Append(Normalize(node.Name))
            .Append(" unique=").Append(node.UniqueNameInOwner)
            .Append(" owner=").Append(node.Owner == null ? "null" : node.Owner == root ? "root" : Normalize(node.Owner.Name));
        if (node is CanvasItem item)
        {
            line.Append(" visible=").Append(item.Visible)
                .Append(" modulate=").Append(Fmt(item.Modulate))
                .Append(" selfModulate=").Append(Fmt(item.SelfModulate))
                .Append(" z=").Append(item.ZIndex).Append(item.ZAsRelative ? "r" : "a")
                .Append(" ysort=").Append(item.YSortEnabled)
                .Append(" showBehind=").Append(item.ShowBehindParent)
                .Append(" topLevel=").Append(item.TopLevel)
                .Append(" material=").Append(item.Material?.ResourcePath ?? (item.Material == null ? "null" : "inline:" + item.Material.GetClass()));
        }

        if (node is Node2D n2d)
        {
            line.Append(" pos=").Append(Fmt(n2d.Position))
                .Append(" scale=").Append(Fmt(n2d.Scale))
                .Append(" rot=").Append(Fmt(n2d.Rotation))
                .Append(" skew=").Append(Fmt(n2d.Skew));
        }

        if (node is Sprite2D sprite)
        {
            line.Append(" tex=").Append(sprite.Texture?.ResourcePath ?? "null")
                .Append(" centered=").Append(sprite.Centered)
                .Append(" offset=").Append(Fmt(sprite.Offset))
                .Append(" flip=").Append(sprite.FlipH).Append('/').Append(sprite.FlipV)
                .Append(" frames=").Append(sprite.Hframes).Append('x').Append(sprite.Vframes).Append('#').Append(sprite.Frame)
                .Append(" region=").Append(sprite.RegionEnabled).Append(':').Append(Fmt(sprite.RegionRect));
        }

        if (node is Control control)
        {
            line.Append(" layout=").Append(control.LayoutMode)
                .Append(" anchors=").Append(Fmt(control.AnchorLeft)).Append(',').Append(Fmt(control.AnchorTop))
                .Append(',').Append(Fmt(control.AnchorRight)).Append(',').Append(Fmt(control.AnchorBottom))
                .Append(" offsets=").Append(Fmt(control.OffsetLeft)).Append(',').Append(Fmt(control.OffsetTop))
                .Append(',').Append(Fmt(control.OffsetRight)).Append(',').Append(Fmt(control.OffsetBottom))
                .Append(" grow=").Append(control.GrowHorizontal).Append('/').Append(control.GrowVertical)
                .Append(" mouse=").Append(control.MouseFilter)
                .Append(" cpos=").Append(Fmt(control.Position))
                .Append(" csize=").Append(Fmt(control.Size))
                .Append(" cscale=").Append(Fmt(control.Scale));
        }

        if (node is AnimationPlayer player)
        {
            line.Append(" anims=[").Append(string.Join(",", player.GetAnimationList().OrderBy(static a => a, StringComparer.Ordinal)))
                .Append("] autoplay=").Append(player.Autoplay)
                .Append(" current=").Append(player.CurrentAnimation)
                .Append(" libs=[").Append(string.Join(",", player.GetAnimationLibraryList().Select(static l => l.ToString()).OrderBy(static l => l, StringComparer.Ordinal)))
                .Append(']');
        }

        if (node is CreatureStateDisplayOffset offset)
        {
            line.Append(" lift=").Append(Fmt(offset.LiftY))
                .Append(" additional=").Append(Fmt(offset.AdditionalOffset));
        }

        if (node.GetScript().VariantType != Variant.Type.Nil && node.GetScript().AsGodotObject() is Resource script)
        {
            line.Append(" script=").Append(script.ResourcePath);
        }

        Row(line.ToString());

        int index = 0;
        foreach (Node child in node.GetChildren(includeInternal: true))
        {
            string childPath = (path.Length == 0 ? "" : path + "/") + index.ToString(CultureInfo.InvariantCulture)
                + ":" + Normalize(child.Name);
            DumpNode(id, variant, root, child, childPath);
            index++;
        }
    }

    private static string DescribeDeclaredState(NCreatureVisuals visuals)
    {
        var parts = new List<string>();
        for (Type? type = visuals.GetType();
             type != null && type != typeof(NCreatureVisuals);
             type = type.BaseType)
        {
            foreach (FieldInfo field in type
                         .GetFields(AnyInstance | BindingFlags.DeclaredOnly)
                         .OrderBy(static f => f.Name, StringComparer.Ordinal))
            {
                Type ft = field.FieldType;
                if (!(ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft == typeof(Vector2)
                      || ft == typeof(Vector2?) || ft == typeof(decimal)))
                {
                    continue;
                }

                parts.Add(type.Name + "." + field.Name + "=" + FmtObject(field.GetValue(visuals)));
            }
        }

        if (visuals is SpriteAttackCreatureVisuals sprite)
        {
            parts.Add("spriteProfile=" + Safe(() => DescribeProfile(sprite.SpriteProfile!)));
        }

        return "{" + string.Join(";", parts) + "}";
    }

    private static string Describe(CreatureVisualLayout layout) =>
        "sprite=" + Fmt(layout.SpritePos)
        + " scale=" + Fmt(layout.SpriteScale)
        + " bounds=" + Fmt(layout.BoundsLeft) + "," + Fmt(layout.BoundsTop) + "," + Fmt(layout.BoundsRight) + "," + Fmt(layout.BoundsBottom)
        + " center=" + Fmt(layout.CenterPos)
        + " intent=" + Fmt(layout.IntentPos)
        + " talk=" + (layout.TalkPos is { } talk ? Fmt(talk) : "null")
        + " stolenPos=" + (layout.StolenCardPos is { } sp ? Fmt(sp) : "null")
        + " stolenScale=" + (layout.StolenCardScale is { } ss ? Fmt(ss) : "null")
        + " lift=" + Fmt(layout.StateDisplayLiftY);

    private static string DescribeProfile(SpriteVisualProfile profile) =>
        "owner=" + ProfileOwner(profile)
        + " idle=" + profile.DefaultIdleTexturePath
        + " assets=" + Hash(string.Join("|", profile.AssetPaths)) + "/" + profile.AssetPaths.Count;

    private static string ProfileOwner(SpriteVisualProfile profile)
    {
        _profileOwners ??= BuildProfileOwners();
        return _profileOwners.TryGetValue(profile, out string? owner) ? owner : "unknown";
    }

    private static Dictionary<SpriteVisualProfile, string> BuildProfileOwners()
    {
        var owners = new Dictionary<SpriteVisualProfile, string>(ReferenceEqualityComparer.Instance);
        foreach (Type type in typeof(LibraryOfRuinaInitializer).Assembly.GetTypes()
                     .OrderBy(static t => t.FullName, StringComparer.Ordinal))
        {
            foreach (FieldInfo field in type.GetFields(DeclaredStatic)
                         .Where(f => f.FieldType == typeof(SpriteVisualProfile) && !type.ContainsGenericParameters))
            {
                if (field.GetValue(null) is SpriteVisualProfile profile && !owners.ContainsKey(profile))
                {
                    owners[profile] = type.FullName + "." + field.Name;
                }
            }
        }

        return owners;
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            Exception inner = ex is TargetInvocationException { InnerException: { } e } ? e : ex;
            return "<" + inner.GetType().Name + ":" + inner.Message + ">";
        }
    }

    private static string Normalize(string name) => GeneratedNodeName.Replace(name, "@$1@N");

    private static string Fmt(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Fmt(Vector2 value) => "(" + Fmt(value.X) + "," + Fmt(value.Y) + ")";

    private static string Fmt(Rect2 value) => Fmt(value.Position) + "+" + Fmt(value.Size);

    private static string Fmt(Color value) =>
        "(" + Fmt(value.R) + "," + Fmt(value.G) + "," + Fmt(value.B) + "," + Fmt(value.A) + ")";

    private static string FmtObject(object? value) => value switch
    {
        null => "null",
        float f => Fmt(f),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        Vector2 v => Fmt(v),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];
}
