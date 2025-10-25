using System.Collections.Generic;

namespace MonAtlas.Models
{
    // Represents a stage in the evolution chain (like Charmander → Charmeleon → Charizard)
    public sealed class EvoStageVM
    {
        public List<EvoFormVM> Forms { get; set; } = new();

        // one label per form (aligned by index)
        public List<string> ConnectorTexts { get; set; } = new();

        // backward-compat for XAML that still binds to ConnectorText (single value)
        public string ConnectorText
        {
            get => ConnectorTexts.FirstOrDefault() ?? "";
            set
            {
                ConnectorTexts.Clear();
                if (!string.IsNullOrWhiteSpace(value)) ConnectorTexts.Add(value);
            }
        }

        public bool IsLast { get; set; }
    }

    public sealed class EvoFormVM
    {
        public string Name { get; set; } = "";
        public string SpriteUrl { get; set; } = "";
    }
}