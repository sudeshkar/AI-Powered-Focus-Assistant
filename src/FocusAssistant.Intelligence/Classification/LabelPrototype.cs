using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FocusAssistant.Intelligence.Classification
{
    /// <summary>
    /// One example activity the classifier compares against.
    /// </summary>
    /// <remarks>
    /// Prototypes rather than a trained head: there is no labelled dataset of this user's
    /// windows, and there never will be one before they start using the app. Comparing
    /// against a handful of written descriptions needs no training, is inspectable, and can
    /// be edited by the user in Settings - which is also the honest way to fix a
    /// misclassification, rather than pretending a model learned something.
    /// </remarks>
    public sealed class LabelPrototype
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "Other";

        [JsonPropertyName("polarity")]
        public string Polarity { get; set; } = "Productive";

        [JsonIgnore]
        public bool IsProductive => Polarity.Equals("Productive", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Populated at warm-up; never serialised.</summary>
        [JsonIgnore]
        public float[]? Embedding { get; set; }
    }

    /// <summary>Shape of Assets/focus_labels.json.</summary>
    public sealed class LabelPrototypeFile
    {
        [JsonPropertyName("labels")]
        public List<LabelPrototype> Labels { get; set; } = [];
    }
}
