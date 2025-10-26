using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MonAtlas.Services;
using MonAtlas.Models;

namespace MonAtlas.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PokeApiClient _api = new();
        public string DexSummary { get; set; }

        // ========= POKÉDEX BROWSER (new) =========
        public ObservableCollection<DexVersion> DexVersions { get; } = new();
        private DexVersion _selectedDexVersion;
        public DexVersion SelectedDexVersion
        {
            get => _selectedDexVersion;
            set { _selectedDexVersion = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); }
        }

        // Full list cached once; filtered list bound to the UI
        private PokemonListItem[] _allPokemon = Array.Empty<PokemonListItem>();
        public ObservableCollection<PokemonListItem> FilteredPokemon { get; } = new();

        // ========= Results / Details / Tabs (existing) =========
        public ObservableCollection<PokemonListItem> Results { get; } = new();
        public ObservableCollection<CounterRow> Counters { get; } = new();

        // Legacy simple chain
        public ObservableCollection<string> EvolutionChain { get; } = new();

        // Rich evolution chain for the Evolution tab
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

        // Search query (header search)
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
                OnPropertyChanged(nameof(BuilderSpriteUrl));
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

        // Busy flag (used by header text and command CanExecute)
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                SearchCommand?.RaiseCanExecuteChanged();
            }
        }

        // Status text (for long loading messages)
        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChanged();
            }
        }

        // ===== Species helpers =====
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

        // ===== Commands =====
        public RelayCommand SearchCommand { get; }
        public RelayCommand ClearCommand { get; }

        // Builder commands
        public RelayCommand CopyCurrentShowdownCommand { get; }
        public RelayCommand AddCurrentToTeamCommand { get; }
        public RelayCommand CopyTeamShowdownCommand { get; }
        public RelayCommand RemoveLastTeamSlotCommand { get; }
        public RelayCommand ImportShowdownFromClipboardCommand { get; }

        // Debounce for autocomplete
        private CancellationTokenSource _typeCts;

        // ===== Showdown Builder State =====
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

        // Shiny
        private bool _shiny;
        public bool Shiny
        {
            get => _shiny;
            set
            {
                _shiny = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowdownPreview));
                OnPropertyChanged(nameof(BuilderSpriteUrl));
            }
        }

        // === Sprite URL for Builder preview ===
        public string BuilderSpriteUrl
        {
            get
            {
                var id = Selected?.Id ?? 0;
                if (id <= 0) return "";
                return Shiny
                    ? $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/shiny/{id}.png"
                    : $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
            }
        }

        // Live preview
        public string ShowdownPreview => BuildShowdownTextForCurrent();

        // ===== ctor =====
        public MainViewModel()
        {
            _ = InitializeAsync(); // now loads dexes + pokedex list as well

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

                // Also clear Pokédex filters/results
                SearchText = "";
                FilteredPokemon.Clear();

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

            // Import from clipboard
            ImportShowdownFromClipboardCommand = new RelayCommand(async () =>
            {
                try
                {
                    var text = System.Windows.Clipboard.GetText();
                    if (string.IsNullOrWhiteSpace(text)) return;
                    await ImportShowdownAsync(text);
                }
                catch { /* ignore */ }
            });
        }

        // ===== Notify =====
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ===== Search (header) =====
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

        // ===== Unified initialization =====
        public async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;

                // Load dex data (existing)
                var progress = new Progress<string>(msg => StatusText = msg);
                await DexLoader.LoadAllDexesAsync(progress);

                // Load Pokédex versions
                LoadDexVersions();

                // Load full Pokémon index for the Pokédex tab
                await LoadAllPokemonAsync();

                // Seed filtered view immediately
                ApplyFilters();
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        // ===== Pokédex helpers =====
        private void LoadDexVersions()
        {
            DexVersions.Clear();

            // Core Generations
            DexVersions.Add(new DexVersion { Id = "national", Name = "National Dex" });
            DexVersions.Add(new DexVersion { Id = "kanto", Name = "Kanto (Gen 1)" });
            DexVersions.Add(new DexVersion { Id = "johto", Name = "Johto (Gen 2)" });
            DexVersions.Add(new DexVersion { Id = "hoenn", Name = "Hoenn (Gen 3)" });
            DexVersions.Add(new DexVersion { Id = "sinnoh", Name = "Sinnoh (Gen 4)" });
            DexVersions.Add(new DexVersion { Id = "unova", Name = "Unova (Gen 5)" });
            DexVersions.Add(new DexVersion { Id = "kalos", Name = "Kalos (Gen 6)" });
            DexVersions.Add(new DexVersion { Id = "za", Name = "Pokémon Legends: Z-A (Kalos*)" });
            DexVersions.Add(new DexVersion { Id = "alola", Name = "Alola (Gen 7)" });
            DexVersions.Add(new DexVersion { Id = "galar", Name = "Galar (Gen 8)" });
            DexVersions.Add(new DexVersion { Id = "hisui", Name = "Hisui (Gen 8.5)" });
            DexVersions.Add(new DexVersion { Id = "legends_arceus", Name = "Pokémon Legends: Arceus (PLA)" });
            DexVersions.Add(new DexVersion { Id = "paldea", Name = "Paldea (Gen 9)" });

            // DLC / Side regions
            DexVersions.Add(new DexVersion { Id = "kitakami", Name = "Kitakami (Teal Mask)" });
            DexVersions.Add(new DexVersion { Id = "blueberry", Name = "Blueberry (Indigo Disk)" });

            // Spin-off / Extended
            DexVersions.Add(new DexVersion { Id = "go", Name = "Pokémon GO (All Released*)" });

            SelectedDexVersion = DexVersions.First();
        }

        private async Task LoadAllPokemonAsync()
        {
            try
            {
                IsBusy = true;

                // Wide coverage seeds: a–z + 0–9. This hits almost every name.
                var seeds = Enumerable.Range('a', 26).Select(c => ((char)c).ToString())
                            .Concat(Enumerable.Range(0, 10).Select(d => d.ToString()))
                            .ToArray();

                var map = new Dictionary<int, PokemonListItem>();
                foreach (var q in seeds)
                {
                    try
                    {
                        var chunk = await _api.SearchAsync(q, 200);
                        if (chunk == null) continue;
                        foreach (var p in chunk) map[p.Id] = p; // de-dup by id
                    }
                    catch { /* ignore single seed failure */ }
                }

                _allPokemon = map.Values.OrderBy(p => p.Id).ToArray();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilters()
        {
            var text = (SearchText ?? string.Empty).Trim().ToLowerInvariant();
            var query = _allPokemon.AsEnumerable();

            if (!string.IsNullOrEmpty(text))
                query = query.Where(p =>
                    (p.Name ?? string.Empty).ToLowerInvariant().Contains(text) ||
                    p.Id.ToString().Contains(text));

            if (SelectedDexVersion is { Id: not null } sel && sel.Id != "national" && sel.Id != "go")
            {
                (int start, int end) = sel.Id switch
                {
                    "kanto" => (1, 151),
                    "johto" => (152, 251),
                    "hoenn" => (252, 386),
                    "sinnoh" => (387, 493),
                    "unova" => (494, 649),
                    "kalos" => (650, 721),
                    "za" => (650, 721),   // Placeholder until Z-A dex confirmed
                    "alola" => (722, 809),
                    "galar" => (810, 898),
                    "hisui" => (899, 905),
                    "legends_arceus" => (899, 905),   // same range, separate label
                    "paldea" => (906, 1025),
                    "kitakami" => (906, 1025),
                    "blueberry" => (906, 1025),
                    _ => (1, int.MaxValue)
                };

                query = query.Where(p => p.Id >= start && p.Id <= end);
            }

            var results = query.Take(500).ToArray();
            FilteredPokemon.Clear();
            foreach (var p in results) FilteredPokemon.Add(p);
        }



        // ===== Autocomplete (header) =====
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

                foreach (var p in list)
                {
                    var pokemonName = p.Name;
                    var dexes = DexLoader.GetPokedexesFor(pokemonName);
                    if (dexes.Count > 0)
                        p.DexSummary = string.Join(" • ", dexes);

                    Suggestions.Add(p);
                }

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

            // Defaults
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
            Shiny = false;
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
                Shiny = Shiny,
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

            // Shiny flag
            sb.AppendLine($"Shiny: {(s.Shiny ? "Yes" : "No")}");

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

        // ===== Evolution tab support =====
        private static int IdFromUrl(string url)
        {
            return int.TryParse(url.TrimEnd('/').Split('/').Last(), out var id) ? id : 0;
        }

        private static string SpriteUrlForId(int id)
        {
            return $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";
        }

        private static readonly Dictionary<string, string> MegaStoneMap = new()
        {
            { "venusaur-mega", "Venusaurite" },
            { "charizard-mega-x", "Charizardite X" },
            { "charizard-mega-y", "Charizardite Y" },
            { "blastoise-mega", "Blastoisinite" },
            { "alakazam-mega", "Alakazite" },
            { "gengar-mega", "Gengarite" },
            { "kangaskhan-mega", "Kangaskhanite" },
            { "pinsir-mega", "Pinsirite" },
            { "gyarados-mega", "Gyaradosite" },
            { "aerodactyl-mega", "Aerodactylite" },
            { "ampharos-mega", "Ampharosite" },
            { "scizor-mega", "Scizorite" },
            { "heracross-mega", "Heracronite" },
            { "houndoom-mega", "Houndoominite" },
            { "tyranitar-mega", "Tyranitarite" },
            { "blaziken-mega", "Blazikenite" },
            { "gardevoir-mega", "Gardevoirite" },
            { "mawile-mega", "Mawilite" },
            { "aggron-mega", "Aggronite" },
            { "medicham-mega", "Medichamite" },
            { "manectric-mega", "Manectite" },
            { "banette-mega", "Banettite" },
            { "absol-mega", "Absolite" },
            { "latias-mega", "Latiasite" },
            { "latios-mega", "Latiosite" },
            { "garchomp-mega", "Garchompite" },
            { "lucario-mega", "Lucarionite" },
            { "abomasnow-mega", "Abomasite" },
            { "gallade-mega", "Galladite" },
            { "audino-mega", "Audinite" },
            { "diancie-mega", "Diancite" }
        };

        public async Task BuildEvolutionStagesAsync(string speciesUrl)
        {
            EvolutionStages.Clear();
            if (string.IsNullOrWhiteSpace(speciesUrl)) return;

            var species = await _api.GetSpeciesByUrlAsync(speciesUrl);
            if (species?.EvolutionChain?.Url == null) return;

            var chainRes = await _api.GetEvolutionChainByUrlAsync(species.EvolutionChain.Url);
            if (chainRes?.Chain == null) return;

            var stages = new List<EvoStageVM>();
            BuildStagesRecursive(chainRes.Chain, stages);

            // Append Mega/G-Max forms as rightmost column with per-form labels
            try
            {
                var specialForms = species.Varieties
                    .Where(v => v?.Pokemon?.Name != null &&
                                (v.Pokemon.Name.Contains("mega", StringComparison.OrdinalIgnoreCase) ||
                                 v.Pokemon.Name.Contains("gmax", StringComparison.OrdinalIgnoreCase)))
                    .Select(v => v.Pokemon)
                    .OrderBy(p => p.Name)
                    .ToList();

                if (specialForms.Count > 0)
                {
                    var megaStage = new EvoStageVM();

                    foreach (var form in specialForms)
                    {
                        var cid = IdFromUrl(form.Url);
                        var key = form.Name.ToLowerInvariant();

                        string label =
                            MegaStoneMap.TryGetValue(key, out var stone) ? stone :
                            key.Contains("gmax") ? "Max Soup" :
                            Nice(form.Name);

                        megaStage.Forms.Add(new EvoFormVM
                        {
                            Name = form.Name,
                            SpriteUrl = SpriteUrlForId(cid),
                            ConnectorLabel = label
                        });
                    }

                    stages.Add(megaStage);
                }
            }
            catch
            {
                // ignore schema differences
            }

            for (int i = 0; i < stages.Count; i++)
                stages[i].IsLast = (i == stages.Count - 1);

            foreach (var s in stages) EvolutionStages.Add(s);
        }

        private void BuildStagesRecursive(ChainLink node, List<EvoStageVM> dst)
        {
            if (dst.Count == 0)
            {
                var rootId = IdFromUrl(node.Species.Url);
                var rootStage = new EvoStageVM();
                rootStage.Forms.Add(new EvoFormVM
                {
                    Name = node.Species.Name,
                    SpriteUrl = SpriteUrlForId(rootId)
                });
                dst.Add(rootStage);
            }

            if (node.EvolvesTo == null || node.EvolvesTo.Count == 0)
                return;

            var next = new EvoStageVM();

            foreach (var child in node.EvolvesTo)
            {
                var cid = IdFromUrl(child.Species.Url);
                var label = (child.EvolutionDetails != null && child.EvolutionDetails.Count > 0)
                    ? BuildConnectorText(child.EvolutionDetails)
                    : "";

                next.Forms.Add(new EvoFormVM
                {
                    Name = child.Species.Name,
                    SpriteUrl = SpriteUrlForId(cid),
                    ConnectorLabel = label
                });
            }

            dst.Add(next);
            BuildStagesRecursive(node.EvolvesTo[0], dst);
        }

        private static string Nice(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var s = raw.Replace('-', ' ');
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private static string BuildConnectorText(IEnumerable<EvolutionDetail>? details)
        {
            if (details == null) return "";

            EvolutionDetail? best = null;
            int bestScore = int.MinValue;

            foreach (var d in details)
            {
                int score = 0;
                if (!string.IsNullOrWhiteSpace(d.Item?.Name)) score += 100;
                if (d.MinLevel.HasValue) score += 80;
                if (!string.IsNullOrWhiteSpace(d.TradeSpecies?.Name)) score += 60;
                if (!string.IsNullOrWhiteSpace(d.Trigger?.Name)) score += 20;
                if (!string.IsNullOrWhiteSpace(d.TimeOfDay)) score += 10;
                if (!string.IsNullOrWhiteSpace(d.Location?.Name)) score += 10;
                if (score > bestScore) { best = d; bestScore = score; }
            }

            if (best == null) return "";

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(best.Item?.Name))
                parts.Add(Nice(best.Item.Name));
            else if (best.MinLevel.HasValue)
                parts.Add("Lv " + best.MinLevel.Value);
            else if (!string.IsNullOrWhiteSpace(best.TradeSpecies?.Name))
                parts.Add("Trade for " + Nice(best.TradeSpecies.Name));
            else if (string.Equals(best.Trigger?.Name, "trade", StringComparison.OrdinalIgnoreCase))
                parts.Add("Trade");
            else if (!string.IsNullOrWhiteSpace(best.Trigger?.Name))
                parts.Add(Nice(best.Trigger!.Name));

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
                parts.Add(best.RelativePhysicalStats.Value switch
                {
                    -1 => "Atk < Def",
                    0 => "Atk = Def",
                    1 => "Atk > Def",
                    _ => "stat check"
                });
            }

            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        // ======== Showdown IMPORT ========
        private static readonly Dictionary<string, string> _statMap = new(StringComparer.OrdinalIgnoreCase)
        {
            {"hp","HP"},
            {"atk","Atk"},
            {"def","Def"},
            {"spa","SpA"}, {"spatk","SpA"}, {"sp.atk","SpA"},
            {"spd","SpD"}, {"spdef","SpD"}, {"sp.def","SpD"},
            {"spe","Spe"}, {"speed","Spe"}
        };

        private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;

        public async Task ImportShowdownAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
                            .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

            string? species = null, nickname = null, item = "";
            string ability = "", nature = "";
            int level = 50;
            bool shiny = false;
            int? ev_hp = null, ev_atk = null, ev_def = null, ev_spa = null, ev_spd = null, ev_spe = null;
            int? iv_hp = null, iv_atk = null, iv_def = null, iv_spa = null, iv_spd = null, iv_spe = null;
            var moves = new List<string>();

            string NiceName(string s) => s?.Trim() ?? "";

            void ParseEVIVLine(string line, bool isEV)
            {
                var m = Regex.Match(line, @"^(EVs|IVs)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                if (!m.Success) return;
                var rhs = m.Groups[2].Value;
                foreach (var part in rhs.Split('/').Select(p => p.Trim()))
                {
                    var mm = Regex.Match(part, @"(?<val>\d+)\s+(?<stat>.+)$");
                    if (!mm.Success) continue;
                    var val = ParseInt(mm.Groups["val"].Value);
                    var statRaw = mm.Groups["stat"].Value
                        .Replace(".", "", StringComparison.Ordinal)
                        .Replace("-", "", StringComparison.Ordinal)
                        .Replace(" ", "", StringComparison.Ordinal);
                    if (!_statMap.TryGetValue(statRaw, out var key)) continue;

                    if (isEV)
                    {
                        switch (key)
                        {
                            case "HP": ev_hp = val; break;
                            case "Atk": ev_atk = val; break;
                            case "Def": ev_def = val; break;
                            case "SpA": ev_spa = val; break;
                            case "SpD": ev_spd = val; break;
                            case "Spe": ev_spe = val; break;
                        }
                    }
                    else
                    {
                        switch (key)
                        {
                            case "HP": iv_hp = val; break;
                            case "Atk": iv_atk = val; break;
                            case "Def": iv_def = val; break;
                            case "SpA": iv_spa = val; break;
                            case "SpD": iv_spd = val; break;
                            case "Spe": iv_spe = val; break;
                        }
                    }
                }
            }

            // Header: "Nickname (Species) @ Item" or "Species @ Item" or "Species"
            var header = lines.FirstOrDefault();
            if (!string.IsNullOrEmpty(header))
            {
                var atParts = header.Split(new[] { " @ " }, StringSplitOptions.None);
                var left = atParts[0].Trim();
                if (atParts.Length > 1) item = atParts[1].Trim();

                var m = Regex.Match(left, @"^(?<nick>.+?)\s*\((?<species>[^)]+)\)$");
                if (m.Success)
                {
                    nickname = NiceName(m.Groups["nick"].Value);
                    species = NiceName(m.Groups["species"].Value);
                }
                else
                {
                    species = NiceName(left);
                }
            }

            foreach (var line in lines.Skip(1))
            {
                if (line.StartsWith("Ability:", StringComparison.OrdinalIgnoreCase))
                    ability = NiceName(line[(line.IndexOf(':') + 1)..]);
                else if (line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase))
                    level = ParseInt(line[(line.IndexOf(':') + 1)..]);
                else if (line.StartsWith("Shiny:", StringComparison.OrdinalIgnoreCase))
                    shiny = line.IndexOf("yes", StringComparison.OrdinalIgnoreCase) >= 0;
                else if (line.StartsWith("EVs:", StringComparison.OrdinalIgnoreCase))
                    ParseEVIVLine(line, isEV: true);
                else if (line.StartsWith("IVs:", StringComparison.OrdinalIgnoreCase))
                    ParseEVIVLine(line, isEV: false);
                else if (Regex.IsMatch(line, @"\bNature\b", RegexOptions.IgnoreCase))
                    nature = NiceName(line.Replace("Nature", "", StringComparison.OrdinalIgnoreCase).Trim());
                else if (line.StartsWith("-", StringComparison.Ordinal))
                {
                    var move = line.TrimStart('-', ' ').Trim();
                    if (move.Length > 0) moves.Add(move);
                }
            }

            if (!string.IsNullOrWhiteSpace(species))
            {
                try
                {
                    var mon = await _api.GetPokemonAsync(species.ToLowerInvariant());
                    Selected = mon;

                    Nickname = string.IsNullOrWhiteSpace(nickname) ? Capitalize(Selected.Name) : nickname;
                    Item = item ?? "";
                    Ability = !string.IsNullOrWhiteSpace(ability) ? ability : Ability;
                    Nature = !string.IsNullOrWhiteSpace(nature) ? nature : Nature;
                    Level = level > 0 ? level : Level;
                    Shiny = shiny;

                    if (ev_hp.HasValue) EV_HP = ev_hp.Value;
                    if (ev_atk.HasValue) EV_Atk = ev_atk.Value;
                    if (ev_def.HasValue) EV_Def = ev_def.Value;
                    if (ev_spa.HasValue) EV_SpA = ev_spa.Value;
                    if (ev_spd.HasValue) EV_SpD = ev_spd.Value;
                    if (ev_spe.HasValue) EV_Spe = ev_spe.Value;

                    if (iv_hp.HasValue) IV_HP = iv_hp.Value;
                    if (iv_atk.HasValue) IV_Atk = iv_atk.Value;
                    if (iv_def.HasValue) IV_Def = iv_def.Value;
                    if (iv_spa.HasValue) IV_SpA = iv_spa.Value;
                    if (iv_spd.HasValue) IV_SpD = iv_spd.Value;
                    if (iv_spe.HasValue) IV_Spe = iv_spe.Value;

                    var mvs = moves.Take(4).ToList();
                    Move1 = mvs.ElementAtOrDefault(0) ?? "";
                    Move2 = mvs.ElementAtOrDefault(1) ?? "";
                    Move3 = mvs.ElementAtOrDefault(2) ?? "";
                    Move4 = mvs.ElementAtOrDefault(3) ?? "";

                    OnPropertyChanged(nameof(ShowdownPreview));
                }
                catch
                {
                    // species not found or API error – ignore
                }
            }
        }
    }

    public class DexVersion
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }
}
