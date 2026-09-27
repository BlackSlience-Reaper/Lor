using System;
using MegaCrit.Sts2.Core.Localization;

namespace LibraryOfRuina.features.moontext;

internal sealed class MoonTextSequenceEntry
{
    public MoonTextSequenceEntry(LocString line, float absoluteTriggerTimeSeconds)
    {
        Line = line;
        AbsoluteTriggerTimeSeconds = Math.Max(0f, absoluteTriggerTimeSeconds);
    }

    public LocString Line { get; }

    public float AbsoluteTriggerTimeSeconds { get; }
}
