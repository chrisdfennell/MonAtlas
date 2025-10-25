using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MonAtlas.Models
{
    public class PokemonSpeciesLite
    {
        [JsonPropertyName("evolution_chain")] public NamedApiResource EvolutionChain { get; set; } = new();
        [JsonPropertyName("gender_rate")] public int GenderRate { get; set; }
        [JsonPropertyName("habitat")] public NamedApiResource? Habitat { get; set; }
        [JsonPropertyName("growth_rate")] public NamedApiResource GrowthRate { get; set; } = new();
    }

    public class EvolutionChainResponse
    {
        [JsonPropertyName("chain")] public ChainLink Chain { get; set; } = new();
    }

    public class ChainLink
    {
        [JsonPropertyName("species")] public NamedApiResource Species { get; set; } = new();
        [JsonPropertyName("evolves_to")] public List<ChainLink> EvolvesTo { get; set; } = new();
        [JsonPropertyName("evolution_details")] public List<EvolutionDetail> EvolutionDetails { get; set; } = new();
    }

    public class EvolutionDetail
    {
        [JsonPropertyName("min_level")] public int? MinLevel { get; set; }
        [JsonPropertyName("item")] public NamedApiResource? Item { get; set; }
        [JsonPropertyName("trigger")] public NamedApiResource? Trigger { get; set; }
    }
}