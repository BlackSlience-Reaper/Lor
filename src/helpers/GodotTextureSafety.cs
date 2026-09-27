using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace LibraryOfRuina.helpers;

internal static class GodotTextureSafety
{
    private sealed class TextureSourcePath(string path)
    {
        public string Path { get; } = path;
    }

    private static readonly ConditionalWeakTable<Texture2D, TextureSourcePath> SourcePaths = new();

    public static bool IsValid([NotNullWhen(true)] Texture2D? texture)
    {
        try
        {
            if (texture == null || !GodotObject.IsInstanceValid(texture))
            {
                return false;
            }

            // IsInstanceValid can remain true after the native Resource is disposed.
            _ = texture.GetRid();
            _ = texture.GetWidth();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public static bool TrySetTexture(TextureRect target, Texture2D? texture)
    {
        if (!TryResolveTexture(texture, out Texture2D? resolved))
        {
            return false;
        }

        try
        {
            target.Texture = resolved;
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public static bool TrySetTexture(Sprite2D target, Texture2D? texture)
    {
        if (!TryResolveTexture(texture, out Texture2D? resolved))
        {
            return false;
        }

        try
        {
            target.Texture = resolved;
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public static void RegisterSourcePath(Texture2D? texture, string path)
    {
        if (texture == null || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        SourcePaths.Remove(texture);
        SourcePaths.Add(texture, new TextureSourcePath(path));
    }

    public static bool TryResolveTexture(Texture2D? texture, [NotNullWhen(true)] out Texture2D? resolved)
    {
        if (IsValid(texture))
        {
            resolved = texture;
            return true;
        }

        if (texture == null || !SourcePaths.TryGetValue(texture, out TextureSourcePath? sourcePath))
        {
            resolved = null;
            return false;
        }

        try
        {
            Texture2D reloaded = PreloadManager.Cache.GetTexture2D(sourcePath.Path);
            if (!IsValid(reloaded))
            {
                resolved = null;
                return false;
            }

            RegisterSourcePath(reloaded, sourcePath.Path);
            resolved = reloaded;
            return true;
        }
        catch
        {
            resolved = null;
            return false;
        }
    }
}
