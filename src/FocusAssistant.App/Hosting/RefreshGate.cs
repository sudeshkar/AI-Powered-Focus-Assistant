using System;

namespace FocusAssistant.Hosting
{
    /// <summary>
    /// Throttles how often a view model actually redoes its expensive load.
    /// </summary>
    /// <remarks>
    /// Switching back to a tab visited moments ago used to mean a fresh SQLite query, a
    /// full FocusScorer/timeline recompute, and - on Today and Insights - a brand new
    /// ~15 second language-model generation, every single time, because the view model
    /// itself never survived the navigation (each screen was DI-registered Transient, so
    /// the old instance and whatever it had already computed were simply discarded). This
    /// only has anything to gate because those registrations are now Singleton: the same
    /// instance persists, so remembering "I already did this recently" is possible at all.
    /// <para>
    /// Deliberately time-based rather than wired to change notifications from the session
    /// engine. A push-based cache would invalidate the instant new activity is recorded -
    /// which is most of the time, since the window poll runs every couple of seconds - and
    /// would end up refreshing just as often as no cache at all. A short time window is the
    /// simpler rule that actually matches the thing being optimised: quick back-and-forth
    /// clicks through the nav rail, not staring at one screen waiting for it to update.
    /// </para>
    /// </remarks>
    public sealed class RefreshGate
    {
        private readonly TimeSpan _minInterval;
        private DateTimeOffset? _lastRefreshedAt;

        public RefreshGate(TimeSpan minInterval)
        {
            _minInterval = minInterval;
        }

        /// <summary>True when enough time has passed - or this is the first call - that a real reload is worth its cost.</summary>
        public bool ShouldRefresh(DateTimeOffset now) =>
            _lastRefreshedAt is null || now - _lastRefreshedAt.Value >= _minInterval;

        public void MarkRefreshed(DateTimeOffset now) => _lastRefreshedAt = now;

        /// <summary>
        /// Forces the next <see cref="ShouldRefresh"/> to return true regardless of timing -
        /// for an explicit user action (changing the Insights period, say) where "I just
        /// looked at this" is not true anymore because what "this" means just changed.
        /// </summary>
        public void Invalidate() => _lastRefreshedAt = null;
    }
}
