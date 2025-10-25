using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MonAtlas.Models;

namespace MonAtlas.Services
{
    public class PokeApiClient
    {
        private const string BaseUrl = "https://pokeapi.co/api/v2";
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, object> _cache = new();


        public PokeApiClient(HttpClient http = null)
        {
            _http = http ?? new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MonAtlas/1.0");
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        // Lightweight wrappers so VM code can fetch by URL without exposing GetJsonAsync<T>.
        public System.Threading.Tasks.Task<PokemonSpeciesLite?> GetSpeciesLiteByUrlAsync(string url)
        {
            return GetJsonAsync<PokemonSpeciesLite>(url);
        }

        public System.Threading.Tasks.Task<EvolutionChainResponse?> GetEvolutionChainByUrlAsync(string url)
        {
            return GetJsonAsync<EvolutionChainResponse>(url);
        }


        private async Task<T> GetJsonAsync<T>(string url)
        {
            if (_cache.TryGetValue(url, out var cached) && cached is T t) return t;

            using var res = await _http.GetAsync(url);

            if (!res.IsSuccessStatusCode)
            {
                // Log exact URL to help diagnose 404s or typos
                var msg = $"GET {url} -> {(int)res.StatusCode} {res.ReasonPhrase}";
                Debug.WriteLine(msg);
                throw new HttpRequestException(msg);
            }

            await using var stream = await res.Content.ReadAsStreamAsync();
            var data = await JsonSerializer.DeserializeAsync<T>(stream, _json)
                       ?? throw new InvalidOperationException("Empty JSON body.");
            _cache[url] = data;
            return data;
        }

        // ---------- Pokemon search (name contains) ----------
        public async Task<List<PokemonListItem>> SearchAsync(string query, int limit = 50)
        {
            query = (query ?? "").Trim().ToLower();
            var results = new List<PokemonListItem>();

            // Try bulk list first (fast and reliable)
            var bulkUrl = $"{BaseUrl}/pokemon?limit=100000&offset=0";
            try
            {
                var page = await GetJsonAsync<JsonElement>(bulkUrl);
                foreach (var item in page.GetProperty("results").EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString() ?? "";
                    if (name.Contains(query))
                    {
                        results.Add(new PokemonListItem
                        {
                            Name = name,
                            Url = item.GetProperty("url").GetString() ?? ""
                        });
                        if (results.Count >= limit) break;
                    }
                }
                return results;
            }
            catch (HttpRequestException ex)
            {
                // If the bulk URL ever fails (network, CDN, etc.), fall back to paging.
                Debug.WriteLine("Bulk list failed: " + ex.Message + " Falling back to paged requests.");
            }

            // Fallback: page in chunks of 200 (only used if bulk failed)
            int offset = 0;
            const int pageSize = 200;
            while (results.Count < limit)
            {
                var url = $"{BaseUrl}/pokemon?limit={pageSize}&offset={offset}";
                var page = await GetJsonAsync<JsonElement>(url);
                var arr = page.GetProperty("results").EnumerateArray().ToList();
                if (arr.Count == 0) break;

                foreach (var item in arr)
                {
                    var name = item.GetProperty("name").GetString() ?? "";
                    if (name.Contains(query))
                    {
                        results.Add(new PokemonListItem
                        {
                            Name = name,
                            Url = item.GetProperty("url").GetString() ?? ""
                        });
                        if (results.Count >= limit) break;
                    }
                }

                if (arr.Count < pageSize) break;
                offset += pageSize;
            }

            return results;
        }

        // ---------- Pokemon details ----------
        public Task<PokemonDetail> GetPokemonAsync(int id) =>
            GetJsonAsync<PokemonDetail>($"{BaseUrl}/pokemon/{id}");

        public Task<PokemonDetail> GetPokemonAsync(string nameOrId) =>
            GetJsonAsync<PokemonDetail>($"{BaseUrl}/pokemon/{nameOrId.ToLower()}");

        // ---------- Species / evolution ----------
        public Task<PokemonSpecies> GetSpeciesAsync(string nameOrId) =>
            GetJsonAsync<PokemonSpecies>($"{BaseUrl}/pokemon-species/{nameOrId.ToLower()}");

        public Task<PokemonSpecies> GetSpeciesByUrlAsync(string url) =>
            GetJsonAsync<PokemonSpecies>(url);


        public async Task<List<string>> GetEvolutionChainAsyncFromSpecies(PokemonSpecies species)
        {
            // species.EvolutionChain.Url is a full URL provided by the API
            var evo = await GetJsonAsync<EvolutionChainResponse>(species.EvolutionChain.Url);
            var names = new List<string>();

            void Walk(ChainLink node)
            {
                names.Add(node.Species.Name);
                foreach (var n in node.EvolvesTo) Walk(n);
            }

            Walk(evo.Chain);
            return names;
        }

        // ---------- Type helpers ----------
        public async Task<List<string>> GetPokemonNamesForTypeAsync(string typeName, int take = 10)
        {
            var tr = await GetJsonAsync<TypeResponse>($"{BaseUrl}/type/{typeName.ToLower()}");
            return tr.Pokemon.Select(p => p.Pokemon.Name).Take(take).ToList();
        }

        public async Task<List<PokemonDetail>> GetPokemonDetailsAsync(IEnumerable<string> namesOrIds, int max = 20)
        {
            var list = new List<PokemonDetail>();
            foreach (var n in namesOrIds.Take(max))
            {
                try { list.Add(await GetPokemonAsync(n)); }
                catch (Exception ex)
                {
                    Debug.WriteLine("Detail fetch failed for " + n + ": " + ex.Message);
                }
            }
            return list;
        }
    }
}