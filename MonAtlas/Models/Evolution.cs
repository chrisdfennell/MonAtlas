using System.Text.Json.Serialization;

namespace MonAtlas.Models
{
    public class EvolutionChainResponse
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("chain")] public ChainLink Chain { get; set; } = new();
    }

    public class ChainLink
    {
        [JsonPropertyName("species")] public NamedApiResource Species { get; set; } = new();
        [JsonPropertyName("evolves_to")] public List<ChainLink> EvolvesTo { get; set; } = new();
    }
}