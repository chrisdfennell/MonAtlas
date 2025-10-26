using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonAtlas.Services
{
    public class DexEntry
    {
        public string PokemonName { get; set; } = "";
        public string PokedexName { get; set; } = "";
        public int EntryNumber { get; set; }
    }

    public static class DexLoader
    {
        private static readonly HttpClient http = new();
        public static List<DexEntry> AllDexEntries { get; private set; } = new();

        public static async Task LoadAllDexesAsync(IProgress<string>? progress = null)
        {
            progress?.Report("Fetching Pokédex list...");
            var root = await http.GetStringAsync("https://pokeapi.co/api/v2/pokedex?limit=200");
            using var doc = JsonDocument.Parse(root);

            var dexUrls = new List<(string name, string url)>();
            foreach (var entry in doc.RootElement.GetProperty("results").EnumerateArray())
            {
                string name = entry.GetProperty("name").GetString() ?? "";
                string url = entry.GetProperty("url").GetString() ?? "";
                dexUrls.Add((name, url));
            }

            var allEntries = new List<DexEntry>();
            int count = 0;
            foreach (var dex in dexUrls)
            {
                count++;
                progress?.Report($"[{count}/{dexUrls.Count}] {dex.name}…");
                try
                {
                    var json = await http.GetStringAsync(dex.url);
                    using var d = JsonDocument.Parse(json);
                    var pokemonEntries = d.RootElement.GetProperty("pokemon_entries");

                    foreach (var poke in pokemonEntries.EnumerateArray())
                    {
                        int num = poke.GetProperty("entry_number").GetInt32();
                        string name = poke.GetProperty("pokemon_species").GetProperty("name").GetString() ?? "";

                        allEntries.Add(new DexEntry
                        {
                            PokemonName = name,
                            PokedexName = dex.name,
                            EntryNumber = num
                        });
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Failed {dex.name}: {ex.Message}");
                }
            }

            AllDexEntries = allEntries;
            progress?.Report($"Loaded {allEntries.Count} total entries across all Pokédexes.");
        }

        public static List<string> GetPokedexesFor(string pokemonName)
        {
            var list = new List<string>();
            foreach (var e in AllDexEntries)
            {
                if (string.Equals(e.PokemonName, pokemonName, StringComparison.OrdinalIgnoreCase))
                    list.Add($"{Capitalize(e.PokedexName)} #{e.EntryNumber}");
            }
            return list;
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }
}