using System.Text.Json.Serialization;

namespace MonAtlas.Models
{
    public class PokemonSpecies
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";

        [JsonPropertyName("gender_rate")] public int GenderRate { get; set; } // -1 = genderless; else 0..8 (female ratio out of 8)
        [JsonPropertyName("capture_rate")] public int CaptureRate { get; set; }
        [JsonPropertyName("base_happiness")] public int BaseHappiness { get; set; }
        [JsonPropertyName("growth_rate")] public NamedApiResource GrowthRate { get; set; } = new();
        [JsonPropertyName("habitat")] public NamedApiResource? Habitat { get; set; }
        [JsonPropertyName("egg_groups")] public List<NamedApiResource> EggGroups { get; set; } = new();

        [JsonPropertyName("flavor_text_entries")] public List<FlavorTextEntry> FlavorTextEntries { get; set; } = new();

        [JsonPropertyName("evolution_chain")] public ApiResource EvolutionChain { get; set; } = new();

        [JsonPropertyName("varieties")]
        public List<SpeciesVarietyLite> Varieties { get; set; } = new();
    }

    public class FlavorTextEntry
    {
        [JsonPropertyName("flavor_text")] public string Text { get; set; } = "";
        [JsonPropertyName("language")] public NamedApiResource Language { get; set; } = new();
        [JsonPropertyName("version")] public NamedApiResource Version { get; set; } = new();
    }

    public class ApiResource
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }
}