using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonAtlas.Models;
using MonAtlas.Services;

namespace MonAtlas.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PokeApiClient _api = new();

        // Results and UI data
        public ObservableCollection<PokemonListItem> Results { get; } = new();
        public ObservableCollection<CounterRow> Counters { get; } = new();

        // Keep old simple chain of names if you still show it elsewhere
        public ObservableCollection<string> EvolutionChain { get; } = new();

        // New rich chain for Evolution tab
        public ObservableCollection<EvoStageVM> EvolutionStages { get; } = new();

        public ObservableCollection<MultiTypeCounter> MultiTypeCounters { get; } = new();
        public ObservableCollection<PokemonListItem> Suggestions { get; } = new();

        // Team (up to 6)
        public ObservableCollection<TeamSlot> Team { get; } = new();

        // Autocomplete popup
        private bool _isSuggestOpen;
        public bool IsSuggestOpen
        {
            get => _isSuggestOpen;
            set { if (_isSuggestOpen == value) return; _isSuggestOpen = value; OnPropertyChanged(); }
        }

        // Search query
        private string _query = "";
        public string Query
        {
            get => _query;
            set
            {
                if (_query == value) return;
                _query = value;
                OnPropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
                _ = DebounceSuggestAsync();
            }
        }

        // Selected pokemon and species
        private PokemonDetail _selected;
        public PokemonDetail Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                OnPropertyChanged();
                UpdateCounters();
                _ = UpdateMultiTypeCountersAsync();
                InitBuilderFromSelected();
            }
        }

        private PokemonSpecies _species;
        public PokemonSpecies Species
        {
            get => _species;
            set
            {
                _species = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FlavorText));
                OnPropertyChanged(nameof(EggGroupsText));
                OnPropertyChanged(nameof(GenderText));
                OnPropertyChanged(nameof(GrowthRateName));
                OnPropertyChanged(nameof(HabitatName));
            }
        }

        // Busy flag
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
            }
        }

        // Species helpers
        public string FlavorText
        {
            get
            {
                var en = Species?.FlavorTextEntries
                    ?.FirstOrDefault(f => string.Equals(f.Language.Name, "en", StringComparison.OrdinalIgnoreCase));
                return en == null ? "" : en.Text.Replace('\n', ' ').Replace('\f', ' ').Trim();
            }
        }
        public string EggGroupsText =>
            Species == null || Species.EggGroups.Count == 0 ? "-" :
            string.Join(", ", Species.EggGroups.Select(e => Capitalize(e.Name)));
        public string GenderText
        {
            get
            {
                if (Species == null) return "-";
                if (Species.GenderRate < 0) return "Genderless";
                double female = Species.GenderRate * 12.5;
                double male = 100 - female;
                return $"{male:0}% male / {female:0}% female";
            }
        }
        public string GrowthRateName => Species?.GrowthRate?.Name is string s && s.Length > 0 ? Capitalize(s) : "-";
        public string HabitatName => Species?.Habitat?.Name is string s && s.Length > 0 ? Capitalize(s) : "-";

        // Commands
        public RelayCommand SearchCommand { get; }
        public RelayCommand ClearCommand { get; }

        // Builder commands
        public RelayCommand CopyCurrentShowdownCommand { get; }
        public RelayCommand AddCurrentToTeamCommand { get; }
        public RelayCommand CopyTeamShowdownCommand { get; }
        public RelayCommand RemoveLastTeamSlotCommand { get; }

        // Debounce for autocomplete
        private CancellationTokenSource _typeCts;

        // ===== Showdown Builder State =====
        // Options
        public ObservableCollection<string> AvailableAbilities { get; } = new();
        public ObservableCollection<string> AvailableMoves { get; } = new();
        public static readonly string[] Natures = new[]
        {
            "Adamant","Bashful","Bold","Brave","Calm","Careful","Docile","Gentle","Hardy",
            "Hasty","Impish","Jolly","Lax","Lonely","Mild","Modest","Naive","Naughty",
            "Quiet","Quirky","Rash","Relaxed","Sassy","Serious","Timid"
        };

        // User selections
        private string _nickname = "";
        public string Nickname { get => _nickname; set { _nickname = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        private string _item = "";
        public string Item { get => _item; set { _item = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        private string _ability = "";
        public string Ability { get => _ability; set { _ability = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        private string _nature = "Adamant";
        public string Nature { get => _nature; set { _nature = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        private int _level = 50;
        public int Level { get => _level; set { _level = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }

        // EVs and IVs
        public int EV_HP { get => _ev_hp; set { _ev_hp = Clamp(value, 0, 252); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int EV_Atk { get => _ev_atk; set { _ev_atk = Clamp(value, 0, 252); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int EV_Def { get => _ev_def; set { _ev_def = Clamp(value, 0, 252); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int EV_SpA { get => _ev_spa; set { _ev_spa = Clamp(value, 0, 252); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int EV_SpD { get => _ev_spd; set { _ev_spd = Clamp(value, 0, 252); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int EV_Spe { get => _ev_spe; set { _ev_spe = Clamp(value, 0, 252); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        private int _ev_hp, _ev_atk, _ev_def, _ev_spa, _ev_spd, _ev_spe;

        public int IV_HP { get => _iv_hp; set { _iv_hp = Clamp(value, 0, 31); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int IV_Atk { get => _iv_atk; set { _iv_atk = Clamp(value, 0, 31); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int IV_Def { get => _iv_def; set { _iv_def = Clamp(value, 0, 31); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int IV_SpA { get => _iv_spa; set { _iv_spa = Clamp(value, 0, 31); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int IV_SpD { get => _iv_spd; set { _iv_spd = Clamp(value, 0, 31); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public int IV_Spe { get => _iv_spe; set { _iv_spe = Clamp(value, 0, 31); OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        private int _iv_hp = 31, _iv_atk = 31, _iv_def = 31, _iv_spa = 31, _iv_spd = 31, _iv_spe = 31;

        // Moves
        private string _move1 = "", _move2 = "", _move3 = "", _move4 = "";
        public string Move1 { get => _move1; set { _move1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public string Move2 { get => _move2; set { _move2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public string Move3 { get => _move3; set { _move3 = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }
        public string Move4 { get => _move4; set { _move4 = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowdownPreview)); } }

        // Live preview
        public string ShowdownPreview => BuildShowdownTextForCurrent();

        public MainViewModel()
        {
            SearchCommand = new RelayCommand(
                async () => await SearchAsync(),
                () => !IsBusy && !string.IsNullOrWhiteSpace(Query));

            ClearCommand = new RelayCommand(() =>
            {
                Query = "";
                Results.Clear();
                Selected = null;
                Species = null;
                Counters.Clear();
                EvolutionChain.Clear();
                EvolutionStages.Clear();
                MultiTypeCounters.Clear();
                Suggestions.Clear();
                IsSuggestOpen = false;
                Team.Clear();
                SearchCommand.RaiseCanExecuteChanged();
            });

            CopyCurrentShowdownCommand = new RelayCommand(() =>
            {
                var text = BuildShowdownTextForCurrent();
                System.Windows.Clipboard.SetText(text);
            });

            AddCurrentToTeamCommand = new RelayCommand(() =>
            {
                if (Selected == null) return;
                if (Team.Count >= 6) return;
                Team.Add(BuildTeamSlotFromCurrent());
            });

            CopyTeamShowdownCommand = new RelayCommand(() =>
            {
                var sb = new StringBuilder();
                foreach (var s in Team)
                {
                    sb.AppendLine(BuildShowdownText(s));
                    sb.AppendLine();
                }
                System.Windows.Clipboard.SetText(sb.ToString().Trim());
            });

            RemoveLastTeamSlotCommand = new RelayCommand(() =>
            {
                if (Team.Count > 0) Team.RemoveAt(Team.Count - 1);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Search
        private async Task SearchAsync()
        {
            try
            {
                IsBusy = true;
                IsSuggestOpen = false;
                Results.Clear();
                var list = await _api.SearchAsync(Query, 100);
                foreach (var p in list) Results.Add(p);
            }
            finally { IsBusy = false; }
        }

        // Autocomplete debounce
        private async Task DebounceSuggestAsync(int delayMs = 250)
        {
            _typeCts?.Cancel();
            _typeCts = new CancellationTokenSource();
            var token = _typeCts.Token;

            try
            {
                await Task.Delay(delayMs, token);
                if (string.IsNullOrWhiteSpace(Query))
                {
                    Suggestions.Clear();
                    IsSuggestOpen = false;
                    return;
                }
                var list = await _api.SearchAsync(Query, limit: 20);
                Suggestions.Clear();
                foreach (var p in list) Suggestions.Add(p);
                IsSuggestOpen = Suggestions.Count > 0;
            }
            catch (TaskCanceledException) { }
            catch { Suggestions.Clear(); IsSuggestOpen = false; }
        }

        public async Task SelectByListItemAsync(PokemonListItem item)
        {
            IsBusy = true;
            try
            {
                Selected = await _api.GetPokemonAsync(item.Id);

                if (!string.IsNullOrWhiteSpace(Selected?.Species?.Url))
                    Species = await _api.GetSpeciesByUrlAsync(Selected.Species.Url);
                else if (!string.IsNullOrWhiteSpace(Selected?.Species?.Name))
                    Species = await _api.GetSpeciesAsync(Selected.Species.Name);
                else
                    Species = null;

                EvolutionChain.Clear();
                if (Species != null)
                {
                    try
                    {
                        var evoNames = await _api.GetEvolutionChainAsyncFromSpecies(Species);
                        foreach (var n in evoNames.Select(Capitalize)) EvolutionChain.Add(n);
                    }
                    catch { }
                }

                // Build rich evolution stages for the Evolution tab using the SPECIES URL
                if (!string.IsNullOrWhiteSpace(Selected?.Species?.Url))
                    await BuildEvolutionStagesAsync(Selected.Species.Url);
            }
            finally { IsBusy = false; }
        }

        private async Task UpdateMultiTypeCountersAsync()
        {
            MultiTypeCounters.Clear();
            var defTypes = Selected?.Types?.Select(t => t.Type.Name).ToArray() ?? Array.Empty<string>();
            if (defTypes.Length == 0) return;

            var list = await CounterService.FindMultiTypeCountersAsync(_api, defTypes, minMatchTypes: 2, maxResults: 24);
            foreach (var c in list) MultiTypeCounters.Add(c);
        }

        private void UpdateCounters()
        {
            Counters.Clear();
            var defTypes = Selected?.Types?.Select(t => t.Type.Name).ToArray() ?? Array.Empty<string>();
            if (defTypes.Length == 0) return;

            var ranking = CounterService.RankAttackTypesAgainst(defTypes);
            var ordered = ranking.Where(kv => kv.Value >= 2.0)
                                 .Concat(ranking.Where(kv => kv.Value < 2.0))
                                 .Take(12)
                                 .ToList();

            foreach (var kv in ordered)
                Counters.Add(new CounterRow { AttackingType = kv.Key, Multiplier = kv.Value });
        }

        private static string Capitalize(string s) =>
            string.IsNullOrWhiteSpace(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        private static int Clamp(int val, int min, int max) => val < min ? min : (val > max ? max : val);

        // ===== Builder helpers =====

        private void InitBuilderFromSelected()
        {
            AvailableAbilities.Clear();
            AvailableMoves.Clear();

            if (Selected == null) return;

            foreach (var ab in Selected.Abilities.Select(a => a.Ability.Name).Distinct(StringComparer.OrdinalIgnoreCase))
                AvailableAbilities.Add(ab);

            foreach (var mv in Selected.Moves.Select(m => m.Move.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n))
                AvailableMoves.Add(mv);

            Ability = AvailableAbilities.FirstOrDefault() ?? "";
            Nature = GuessNatureFromStats();
            Level = 50;

            // Simple default EVs: 252 in best attack, 252 Spe, 4 HP
            EV_HP = 4; EV_Atk = 0; EV_SpA = 0; EV_Spe = 252; EV_Def = 0; EV_SpD = 0;
            var atk = Selected.Stats.FirstOrDefault(s => s.Stat.Name == "attack")?.BaseStat ?? 0;
            var spa = Selected.Stats.FirstOrDefault(s => s.Stat.Name == "special-attack")?.BaseStat ?? 0;
            if (atk >= spa) EV_Atk = 252; else EV_SpA = 252;

            var first4 = AvailableMoves.Take(4).ToList();
            Move1 = first4.ElementAtOrDefault(0) ?? "";
            Move2 = first4.ElementAtOrDefault(1) ?? "";
            Move3 = first4.ElementAtOrDefault(2) ?? "";
            Move4 = first4.ElementAtOrDefault(3) ?? "";
            Nickname = Capitalize(Selected.Name);
            Item = "";
            OnPropertyChanged(nameof(ShowdownPreview));
        }

        private string GuessNatureFromStats()
        {
            if (Selected == null) return "Adamant";
            var atk = Selected.Stats.FirstOrDefault(s => s.Stat.Name == "attack")?.BaseStat ?? 0;
            var spa = Selected.Stats.FirstOrDefault(s => s.Stat.Name == "special-attack")?.BaseStat ?? 0;
            var spe = Selected.Stats.FirstOrDefault(s => s.Stat.Name == "speed")?.BaseStat ?? 0;

            if (spe >= Math.Max(atk, spa))
                return "Jolly";
            return atk >= spa ? "Adamant" : "Modest";
        }

        private TeamSlot BuildTeamSlotFromCurrent()
        {
            return new TeamSlot
            {
                Species = Selected?.Name ?? "",
                Nickname = Nickname,
                Item = Item,
                Ability = Ability,
                Nature = Nature,
                Level = Level,
                EV_HP = EV_HP,
                EV_Atk = EV_Atk,
                EV_Def = EV_Def,
                EV_SpA = EV_SpA,
                EV_SpD = EV_SpD,
                EV_Spe = EV_Spe,
                IV_HP = IV_HP,
                IV_Atk = IV_Atk,
                IV_Def = IV_Def,
                IV_SpA = IV_SpA,
                IV_SpD = IV_SpD,
                IV_Spe = IV_Spe,
                Move1 = Move1,
                Move2 = Move2,
                Move3 = Move3,
                Move4 = Move4
            };
        }

        private string BuildShowdownTextForCurrent()
        {
            var slot = BuildTeamSlotFromCurrent();
            return BuildShowdownText(slot);
        }

        private static string BuildShowdownText(TeamSlot s)
        {
            var sb = new StringBuilder();

            var name = string.IsNullOrWhiteSpace(s.Nickname) ? Capitalize(s.Species) : s.Nickname;
            if (!string.IsNullOrWhiteSpace(s.Item))
                sb.AppendLine($"{name} ({Capitalize(s.Species)}) @ {s.Item}");
            else
                sb.AppendLine($"{name} ({Capitalize(s.Species)})");

            if (!string.IsNullOrWhiteSpace(s.Ability)) sb.AppendLine($"Ability: {Capitalize(s.Ability)}");
            if (s.Level > 0 && s.Level != 100) sb.AppendLine($"Level: {s.Level}");

            var evParts = new[]
            {
                s.EV_HP  > 0 ? $"{s.EV_HP} HP"   : null,
                s.EV_Atk > 0 ? $"{s.EV_Atk} Atk" : null,
                s.EV_Def > 0 ? $"{s.EV_Def} Def" : null,
                s.EV_SpA > 0 ? $"{s.EV_SpA} SpA" : null,
                s.EV_SpD > 0 ? $"{s.EV_SpD} SpD" : null,
                s.EV_Spe > 0 ? $"{s.EV_Spe} Spe" : null
            }.Where(p => p != null).ToArray();
            if (evParts.Length > 0) sb.AppendLine($"EVs: {string.Join(" / ", evParts)}");

            if (!string.IsNullOrWhiteSpace(s.Nature)) sb.AppendLine($"{s.Nature} Nature");

            if (new[] { s.IV_HP, s.IV_Atk, s.IV_Def, s.IV_SpA, s.IV_SpD, s.IV_Spe }.Any(v => v != 31))
            {
                var ivParts = new[]
                {
                    s.IV_HP  != 31 ? $"{s.IV_HP} HP"   : null,
                    s.IV_Atk != 31 ? $"{s.IV_Atk} Atk" : null,
                    s.IV_Def != 31 ? $"{s.IV_Def} Def" : null,
                    s.IV_SpA != 31 ? $"{s.IV_SpA} SpA" : null,
                    s.IV_SpD != 31 ? $"{s.IV_SpD} SpD" : null,
                    s.IV_Spe != 31 ? $"{s.IV_Spe} Spe" : null
                }.Where(p => p != null).ToArray();
                if (ivParts.Length > 0) sb.AppendLine($"IVs: {string.Join(" / ", ivParts)}");
            }

            void addMove(string m) { if (!string.IsNullOrWhiteSpace(m)) sb.AppendLine($"- {Capitalize(m)}"); }
            addMove(s.Move1); addMove(s.Move2); addMove(s.Move3); addMove(s.Move4);

            return sb.ToString().TrimEnd();
        }

        // === Evolution tab support ===

        // === Evolution tab support ===

        private static int IdFromUrl(string url)
        {
            return int.TryParse(url.TrimEnd('/').Split('/').Last(), out var id) ? id : 0;
        }

        private static string SpriteUrlForId(int id)
        {
            return $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
        }

        // NOTE: use species URL (works for megas/forms too)
        // Call with: await BuildEvolutionStagesAsync(Selected?.Species?.Url);
        public async Task BuildEvolutionStagesAsync(string speciesUrl)
        {
            EvolutionStages.Clear();
            if (string.IsNullOrWhiteSpace(speciesUrl)) return;

            // 1) species -> evolution chain URL
            var speciesLite = await _api.GetSpeciesLiteByUrlAsync(speciesUrl);
            if (speciesLite?.EvolutionChain?.Url is null) return;

            // 2) fetch chain
            var chain = await _api.GetEvolutionChainByUrlAsync(speciesLite.EvolutionChain.Url);
            if (chain?.Chain == null) return;

            // 3) build stages
            var stages = new List<EvoStageVM>();
            BuildStagesRecursive(chain.Chain, stages);

            // === Append Mega / G-Max forms as an extra stage, if present on final species ===
            if (stages.Count > 0 && stages[stages.Count - 1].Forms.Count > 0)
            {
                // The last “normal” species name (e.g., "charizard")
                var lastBaseName = stages[stages.Count - 1].Forms[0].Name;

                // Get that species (full) to read all varieties
                var finalSpecies = await _api.GetSpeciesAsync(lastBaseName);
                var varieties = finalSpecies?.Varieties;

                if (varieties != null && varieties.Count > 0)
                {
                    var specialForms = varieties
                        .Where(v => v.Pokemon?.Name != null &&
                                    (v.Pokemon.Name.Contains("mega", StringComparison.OrdinalIgnoreCase) ||
                                     v.Pokemon.Name.Contains("gmax", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (specialForms.Count > 0)
                    {
                        // Decide connector label for the base->special stage
                        bool hasMega = specialForms.Any(v => v.Pokemon.Name.IndexOf("mega", StringComparison.OrdinalIgnoreCase) >= 0);
                        bool hasGmax = specialForms.Any(v => v.Pokemon.Name.IndexOf("gmax", StringComparison.OrdinalIgnoreCase) >= 0);
                        var connectorLabel = hasMega && hasGmax ? "Mega / G-Max" : (hasMega ? "Mega" : "G-Max");

                        // Put the connector text on the PREVIOUS (base) stage
                        stages[stages.Count - 1].ConnectorText = connectorLabel;

                        // Create the new stage with all special forms rendered in the same column
                        var specialStage = new EvoStageVM { Forms = new List<EvoFormVM>(), ConnectorText = "" };

                        foreach (var v in specialForms)
                        {
                            var mid = IdFromUrl(v.Pokemon.Url); // 10034, 10035, etc.
                            specialStage.Forms.Add(new EvoFormVM
                            {
                                Name = v.Pokemon.Name,
                                SpriteUrl = SpriteUrlForId(mid)   // sprites exist for the >10000 IDs
                            });
                        }

                        stages.Add(specialStage);
                    }
                }
            }

            // 4) mark last
            for (int i = 0; i < stages.Count; i++)
                stages[i].IsLast = (i == stages.Count - 1);

            foreach (var s in stages)
                EvolutionStages.Add(s);
        }

        private void BuildStagesRecursive(ChainLink link, List<EvoStageVM> stages)
        {
            // Add the current stage (once for the root, then only children are added as "next")
            if (stages.Count == 0)
            {
                var rootId = IdFromUrl(link.Species.Url);
                stages.Add(new EvoStageVM
                {
                    Forms = new List<EvoFormVM>
            {
                new EvoFormVM { Name = link.Species.Name, SpriteUrl = SpriteUrlForId(rootId) }
            },
                    // ConnectorText for the root will be set from its first child below
                    ConnectorText = ""
                });
            }

            if (link.EvolvesTo != null && link.EvolvesTo.Count > 0)
            {
                // Connector text (Lv 16, stone, trade...) belongs to the PREVIOUS stage
                var connectorForPrevious = BuildConnectorText(link.EvolvesTo[0].EvolutionDetails);
                stages[stages.Count - 1].ConnectorText = connectorForPrevious;

                // Next stage (can be branching -> multiple forms in same column)
                var next = new EvoStageVM { Forms = new List<EvoFormVM>(), ConnectorText = "" };

                foreach (var child in link.EvolvesTo)
                {
                    var cid = IdFromUrl(child.Species.Url);
                    next.Forms.Add(new EvoFormVM { Name = child.Species.Name, SpriteUrl = SpriteUrlForId(cid) });
                }

                stages.Add(next);

                // Continue linearly down the first branch
                BuildStagesRecursive(link.EvolvesTo[0], stages);
            }
        }

        private static string Nice(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            // "water-stone" -> "Water Stone"
            var s = raw.Replace('-', ' ');
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private static string BuildConnectorText(IEnumerable<EvolutionDetail>? details)
        {
            if (details == null) return "";

            // Use the first detail (most evolutions have a single entry; if many exist, pick the one
            // with the clearest signal: item > level > trade > trigger)
            EvolutionDetail? best = null;
            int bestScore = int.MinValue;

            foreach (var d in details)
            {
                int score = 0;
                if (!string.IsNullOrWhiteSpace(d.Item?.Name)) score += 100;      // prefer stones / use-item
                if (d.MinLevel.HasValue) score += 80;
                if (!string.IsNullOrWhiteSpace(d.TradeSpecies?.Name)) score += 60;
                if (!string.IsNullOrWhiteSpace(d.Trigger?.Name)) score += 20;
                if (!string.IsNullOrWhiteSpace(d.TimeOfDay)) score += 10;
                if (!string.IsNullOrWhiteSpace(d.Location?.Name)) score += 10;
                if (score > bestScore) { best = d; bestScore = score; }
            }

            if (best == null) return "";

            var parts = new List<string>();

            // 1) Use-item (stones, etc.)
            if (!string.IsNullOrWhiteSpace(best.Item?.Name))
            {
                parts.Add(Nice(best.Item!.Name)); // "Leaf Stone", "Water Stone", "Dawn Stone", etc.
            }
            // 2) Level
            else if (best.MinLevel.HasValue)
            {
                parts.Add("Lv " + best.MinLevel.Value);
            }
            // 3) Trade variants
            else if (!string.IsNullOrWhiteSpace(best.TradeSpecies?.Name))
            {
                parts.Add("Trade for " + Nice(best.TradeSpecies.Name));
            }
            else if (string.Equals(best.Trigger?.Name, "trade", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Trade");
            }
            else if (!string.IsNullOrWhiteSpace(best.Trigger?.Name))
            {
                // fallback for other triggers (e.g., "level-up", "use-item")
                parts.Add(Nice(best.Trigger!.Name));
            }

            // Contextual extras (time, location, known move type, happiness, etc.)
            if (!string.IsNullOrWhiteSpace(best.TimeOfDay))
                parts.Add("(" + Nice(best.TimeOfDay) + ")");

            if (!string.IsNullOrWhiteSpace(best.Location?.Name))
                parts.Add("@" + Nice(best.Location.Name));

            if (!string.IsNullOrWhiteSpace(best.KnownMoveType?.Name))
                parts.Add("with " + Nice(best.KnownMoveType.Name) + " move");

            if (best.MinHappiness.HasValue)
                parts.Add("Happiness ≥ " + best.MinHappiness.Value);

            if (best.MinBeauty.HasValue)
                parts.Add("Beauty ≥ " + best.MinBeauty.Value);

            if (best.MinAffection.HasValue)
                parts.Add("Affection ≥ " + best.MinAffection.Value);

            if (best.NeedsOverworldRain == true)
                parts.Add("while raining");

            if (best.TurnUpsideDown == true)
                parts.Add("hold console upside-down");

            if (best.RelativePhysicalStats.HasValue)
            {
                // -1: Atk < Def, 0: Atk = Def, 1: Atk > Def
                var rps = best.RelativePhysicalStats.Value;
                parts.Add(rps switch
                {
                    -1 => "Atk < Def",
                    0 => "Atk = Def",
                    1 => "Atk > Def",
                    _ => "stat check"
                });
            }

            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }

    public class CounterRow
    {
        public string AttackingType { get; set; } = "";
        public double Multiplier { get; set; }
        public string Label => $"{AttackingType.ToUpper()}  x{Multiplier:0.##}";
    }

    // One Showdown set
    public class TeamSlot
    {
        public string Species { get; set; } = "";
        public string Nickname { get; set; } = "";
        public string Item { get; set; } = "";
        public string Ability { get; set; } = "";
        public string Nature { get; set; } = "Adamant";
        public int Level { get; set; } = 50;

        public int EV_HP, EV_Atk, EV_Def, EV_SpA, EV_SpD, EV_Spe;
        public int IV_HP = 31, IV_Atk = 31, IV_Def = 31, IV_SpA = 31, IV_SpD = 31, IV_Spe = 31;

        public string Move1 { get; set; } = "";
        public string Move2 { get; set; } = "";
        public string Move3 { get; set; } = "";
        public string Move4 { get; set; } = "";
    }
}