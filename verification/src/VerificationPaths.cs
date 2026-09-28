using System;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 资源契约套件要直接读仓库文件和原始美术来比对哈希，路径由环境变量提供。
/// </summary>
internal static class VerificationPaths
{
    /// <summary>本仓库根目录（<c>LOR_PROJECT_ROOT</c>）。依赖它的套件在未设置时直接失败。</summary>
    public static string ProjectRoot =>
        Environment.GetEnvironmentVariable("LOR_PROJECT_ROOT")
        ?? throw new InvalidOperationException("Set LOR_PROJECT_ROOT to the LibraryOfRuina repository root.");

    /// <summary>原始美术目录（<c>LOR_ART_SOURCE_ROOT</c>），只有作者本机有；未设置时跳过对应检查。</summary>
    public static string? ArtSourceRoot => Environment.GetEnvironmentVariable("LOR_ART_SOURCE_ROOT");
}
