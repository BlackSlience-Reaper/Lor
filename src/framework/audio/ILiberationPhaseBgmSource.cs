namespace LibraryOfRuina.framework.audio;

internal interface ILiberationPhaseBgmSource
{
    int CurrentPhase { get; }

    void RefreshLiberationPhaseBgm();
}
