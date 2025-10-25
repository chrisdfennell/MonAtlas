using MonAtlas.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MonAtlas.Services
{
    public record CounterSuggestion(string AttackingType, double Multiplier, List<string> ExamplePokemon);

    public static class CounterService
    {
        // Rank single attacking types vs the defender type combo
        public static Dictionary<string, double> RankAttackTypesAgainst(params string[] defenderTypes)
        {
            var mults = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var atk in Types.All)
            {
                double total = 1.0;
                foreach (var def in defenderTypes.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    int a = Types.IndexOf(atk);
                    int d = Types.IndexOf(def);
                    if (a < 0 || d < 0) continue;
                    total *= Types.Mult[a, d];
                }
                mults[atk] = total;
            }
            return mults.OrderByDescending(kv => kv.Value)
                        .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        // NEW: find Pokemon whose own types contain >= minMatchTypes types that are super-effective (>= 2x) vs defender
        public static async Task<List<MultiTypeCounter>> FindMultiTypeCountersAsync(
            PokeApiClient api, string[] defenderTypes, int minMatchTypes = 2, int maxResults = 24)
        {
            var ranking = RankAttackTypesAgainst(defenderTypes);
            var effectiveTypes = ranking.Where(kv => kv.Value >= 2.0).Select(kv => kv.Key).ToList();
            if (effectiveTypes.Count == 0) return new List<MultiTypeCounter>();

            // For each effective attacking type, fetch Pokemon of that type
            var buckets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var atk in effectiveTypes)
            {
                try
                {
                    var names = await api.GetPokemonNamesForTypeAsync(atk, 250);
                    buckets[atk] = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    // continue on failure
                }
            }

            // Count how many attacking-type buckets each Pokemon appears in
            var counts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (atk, set) in buckets)
            {
                foreach (var name in set)
                {
                    if (!counts.TryGetValue(name, out var s))
                    {
                        s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        counts[name] = s;
                    }
                    s.Add(atk);
                }
            }

            // Pick candidates with >= minMatchTypes matches
            var candidates = counts
                .Where(kv => kv.Value.Count >= minMatchTypes)
                .OrderByDescending(kv => kv.Value.Count)
                .ThenBy(kv => kv.Key)
                .Take(100)
                .ToList();

            // Fetch details for top few to display their types
            var topNames = candidates.Select(kv => kv.Key).Take(maxResults);
            var details = await api.GetPokemonDetailsAsync(topNames, maxResults);
            var map = details.ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

            var result = new List<MultiTypeCounter>();
            foreach (var c in candidates.Take(maxResults))
            {
                map.TryGetValue(c.Key, out var det);
                result.Add(new MultiTypeCounter
                {
                    Name = c.Key,
                    MatchingAttackingTypes = c.Value.OrderBy(v => v).ToList(),
                    MatchingCount = c.Value.Count,
                    Types = det?.Types.Select(t => t.Type.Name).ToList() ?? new List<string>(),
                    SpriteUrl = det?.SpriteUrl ?? ""
                });
            }

            return result;
        }
    }

    public class MultiTypeCounter
    {
        public string Name { get; set; } = "";
        public int MatchingCount { get; set; }
        public List<string> MatchingAttackingTypes { get; set; } = new List<string>();
        public List<string> Types { get; set; } = new List<string>();
        public string SpriteUrl { get; set; } = "";
        public string MatchingSummary => string.Join(", ", MatchingAttackingTypes.Select(s => s.ToUpper()));
        public string TypesSummary => Types.Count == 0 ? "-" : string.Join("/", Types.Select(s => s.ToUpper()));
    }
}