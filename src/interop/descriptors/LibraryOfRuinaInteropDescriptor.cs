using System;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.interop.descriptors;

public sealed class LibraryOfRuinaInteropDescriptor
{
    public string PublicId { get; }

    public LibraryOfRuinaInteropCategory Category { get; }

    public ModelId? RawModelId { get; }

    public Type RuntimeType { get; }

    public string TextKeyRoot { get; }

    public bool IsExperimental { get; }

    public bool IsDebug { get; }

    public IntentType? IntentKind { get; }

    public bool HasRawModelId => RawModelId != null;

    public LibraryOfRuinaInteropDescriptor(
        string publicId,
        LibraryOfRuinaInteropCategory category,
        ModelId? rawModelId,
        Type runtimeType,
        string textKeyRoot,
        bool isExperimental,
        bool isDebug,
        IntentType? intentKind)
    {
        PublicId = publicId;
        Category = category;
        RawModelId = rawModelId;
        RuntimeType = runtimeType;
        TextKeyRoot = textKeyRoot;
        IsExperimental = isExperimental;
        IsDebug = isDebug;
        IntentKind = intentKind;
    }
}
