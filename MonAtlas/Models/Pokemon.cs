using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MonAtlas.Models
{
    public class PokemonListItem
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";

        public int Id => int.TryParse(Url.TrimEnd('/').Split('/').Last(), out var id) ? id : 0;
        public string DisplayName => string.IsNullOrEmpty(Name) ? "" : char.ToUpper(Name[0]) + Name[1..];
        public string SpriteUrl => $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{Id}.png";
    }

    public class NamedApiResource
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }

    public class PokemonTypeSlot
    {
        [JsonPropertyName("slot")] public int Slot { get; set; }
        [JsonPropertyName("type")] public NamedApiResource Type { get; set; } = new();
    }

    public class PokemonStat
    {
        [JsonPropertyName("base_stat")] public int BaseStat { get; set; }
        [JsonPropertyName("stat")] public NamedApiResource Stat { get; set; } = new();
    }

    public class PokemonAbility
    {
        [JsonPropertyName("ability")] public NamedApiResource Ability { get; set; } = new();
        [JsonPropertyName("is_hidden")] public bool IsHidden { get; set; }
    }

    public class PokemonHeldItem
    {
        [JsonPropertyName("item")] public NamedApiResource Item { get; set; } = new();
    }

    public class PokemonDetail
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";

        [JsonPropertyName("species")] public NamedApiResource Species { get; set; } = new();

        [JsonPropertyName("types")] public List<PokemonTypeSlot> Types { get; set; } = new();
        [JsonPropertyName("stats")] public List<PokemonStat> Stats { get; set; } = new();
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("weight")] public int Weight { get; set; }
        [JsonPropertyName("base_experience")] public int BaseExperience { get; set; }
        [JsonPropertyName("abilities")] public List<PokemonAbility> Abilities { get; set; } = new();
        [JsonPropertyName("held_items")] public List<PokemonHeldItem> HeldItems { get; set; } = new();
        [JsonPropertyName("forms")] public List<NamedApiResource> Forms { get; set; } = new();

        public string SpriteUrl => $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{Id}.png";
        public string DisplayName => string.IsNullOrEmpty(Name) ? "" : char.ToUpper(Name[0]) + Name[1..];
        [JsonPropertyName("moves")] public List<PokemonMove> Moves { get; set; } = new();
    }

    public class PokemonMove
    {
        [JsonPropertyName("move")] public NamedApiResource Move { get; set; } = new();
        // version_group_details omitted for now
    }


    public class TypeResponse
    {
        [JsonPropertyName("pokemon")] public List<TypePokemonEntry> Pokemon { get; set; } = new();
    }

    public class TypePokemonEntry
    {
        [JsonPropertyName("pokemon")] public NamedApiResource Pokemon { get; set; } = new();
    }
}