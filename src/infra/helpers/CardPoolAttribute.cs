using System;
using LibraryOfRuina.core;

namespace LibraryOfRuina.infra.helpers;

/// <summary>
/// 标记一个卡牌类应自动注册到指定卡池。
/// 配合 <see cref="LibraryOfRuinaInitializer.RegisterRuntimeCardPools"/> 的反射自动发现使用。
///
/// 使用示例：
/// <code>
/// [CardPool(typeof(StatusCardPool))]
/// public sealed class SoulSnareStatusCard : CardModel { }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CardPoolAttribute : Attribute
{
    /// <summary>
    /// 目标卡池的类型（如 typeof(StatusCardPool)、typeof(TokenCardPool)）。
    /// </summary>
    public Type PoolType { get; }

    public CardPoolAttribute(Type poolType)
    {
        PoolType = poolType;
    }
}
