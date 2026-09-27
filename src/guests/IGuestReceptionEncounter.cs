using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.guests;

/// <summary>
/// Marks a normal Library of Ruina guest reception.  Keep the encounter's
/// model files under src/guests.  Special guest stages deliberately do not
/// implement this marker.
/// </summary>
public interface IGuestReceptionEncounter
{
    /// <summary>
    /// Extra run-state gate for a regular guest.  Act and tier eligibility are
    /// enforced by the current ActModel pool before this method is called.
    /// </summary>
    bool CanAppearForBookShadow(IRunState runState)
    {
        _ = runState;
        return true;
    }
}
