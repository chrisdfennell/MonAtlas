using System.Collections.Generic;

namespace MonAtlas.Models
{
    public sealed class PokemonSpeciesVariety
    {
        public bool IsDefault { get; set; }
        public NamedApiResource Pokemon { get; set; } = new(); // has Name + Url
    }

    // extend your existing model with Varieties
    public sealed partial class PokemonSpecies
    {
        public List<PokemonSpeciesVariety> Varieties { get; set; } = new();
    }
}