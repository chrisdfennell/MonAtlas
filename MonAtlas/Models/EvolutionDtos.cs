using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MonAtlas.Models
{
    // Minimal species model: only what we need to discover the evolution chain URL
    public class PokemonSpeciesLite
    {
        [JsonPropertyName("evolution_chain")]
        public NamedApiResource EvolutionChain { get; set; } = new();

        // Add this:
        [JsonPropertyName("varieties")]
        public List<SpeciesVarietyLite> Varieties { get; set; } = new();
    }

    public class SpeciesVarietyLite
    {
        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("pokemon")]
        public NamedApiResource Pokemon { get; set; } = new();
    }

    // Root evolution chain response
    public sealed class EvolutionChainResponse
    {
        [JsonPropertyName("chain")] public ChainLink? Chain { get; set; }
    }

    // One node in the chain
    public sealed class ChainLink
    {
        [JsonPropertyName("species")] public NamedApiResource Species { get; set; } = new();
        [JsonPropertyName("evolves_to")] public List<ChainLink> EvolvesTo { get; set; } = new();
        [JsonPropertyName("evolution_details")] public List<EvolutionDetail> EvolutionDetails { get; set; } = new();
    }

    // Evolution conditions (subset)
    public sealed class EvolutionDetail
    {
        [JsonPropertyName("min_level")] public int? MinLevel { get; set; }
        [JsonPropertyName("item")] public NamedApiResource? Item { get; set; }                // Leaf Stone, Water Stone, etc.
        [JsonPropertyName("trigger")] public NamedApiResource? Trigger { get; set; }          // level-up, trade, use-item
        [JsonPropertyName("held_item")] public NamedApiResource? HeldItem { get; set; }       // sometimes used
        [JsonPropertyName("time_of_day")] public string? TimeOfDay { get; set; }              // day / night
        [JsonPropertyName("location")] public NamedApiResource? Location { get; set; }        // mossy-rock, etc.
        [JsonPropertyName("known_move_type")] public NamedApiResource? KnownMoveType { get; set; } // e.g., Fairy
        [JsonPropertyName("trade_species")] public NamedApiResource? TradeSpecies { get; set; }

        [JsonPropertyName("min_happiness")] public int? MinHappiness { get; set; }
        [JsonPropertyName("min_beauty")] public int? MinBeauty { get; set; }
        [JsonPropertyName("min_affection")] public int? MinAffection { get; set; }
        [JsonPropertyName("needs_overworld_rain")] public bool? NeedsOverworldRain { get; set; }
        [JsonPropertyName("turn_upside_down")] public bool? TurnUpsideDown { get; set; }
        [JsonPropertyName("gender")] public int? Gender { get; set; }
        [JsonPropertyName("relative_physical_stats")] public int? RelativePhysicalStats { get; set; }
    }
}