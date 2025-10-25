using System.Collections.Generic;

namespace MonAtlas.Models
{
    // Represents a stage in the evolution chain (like Charmander → Charmeleon → Charizard)
    public sealed class EvoStageVM
    {
        public IList<EvoFormVM> Forms { get; set; } = new List<EvoFormVM>();  // one or many (for branching)
        public string ConnectorText { get; set; } = string.Empty;             // e.g., "Lv 16", "Fire Stone", etc.
        public bool IsLast { get; set; }                                      // true for the final stage (no arrow)
    }

    // Represents one Pokémon form (for example Mega Charizard X or Y)
    public sealed class EvoFormVM
    {
        public string Name { get; set; } = string.Empty;
        public string SpriteUrl { get; set; } = string.Empty;                 // 96×96 or similar
        // (Optional) public string BadgeIconUrl { get; set; } = string.Empty;  // if you want a small icon per form
    }
}