using System;
using System.ComponentModel.DataAnnotations;

namespace FocusAssistant.Core.Models
{
    /// <summary>
    /// A correction the user made: "this is/isn't work", applied from then on.
    /// </summary>
    /// <remarks>
    /// The entire learning loop, and deliberately the dullest possible mechanism - a rule
    /// the user wrote, not a model update nobody can see or undo. Matching is on app name
    /// alone, not app+title: someone who says an application is work almost always means
    /// the whole application, and a title-scoped override would silently fail to cover the
    /// next window in the same app, which reads as the correction not having worked.
    /// </remarks>
    public class UserOverride
    {
        [Key]
        public int oID { get; set; }

        public string AppName { get; set; } = string.Empty;

        public bool IsProductive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
