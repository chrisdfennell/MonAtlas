using System.Collections.ObjectModel;

namespace MonAtlas.Models
{
    public sealed class EvoStageVM
    {
        // Column of forms (1+ rows if branching)
        public ObservableCollection<EvoFormVM> Forms { get; } = new();

        // Legacy: labels aligned to the whole column (no longer used by the centered UI, but safe to keep)
        public ObservableCollection<string> ConnectorTexts { get; } = new();

        public bool IsLast { get; set; }
    }

    public sealed class EvoFormVM
    {
        public string Name { get; set; } = "";
        public string SpriteUrl { get; set; } = "";

        // NEW: label for the edge leading into THIS form (used to center the chip next to this row)
        public string? ConnectorLabel { get; set; }
    }
}
