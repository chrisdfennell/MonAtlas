# MonAtlas (WPF, .NET 8)
A modern, dark‑themed Pokémon encyclopedia and **counter advisor** for Windows (WPF) using the public [PokeAPI](https://pokeapi.co).

## Features
- 🔎 Name search (client‑filtered from the full list) with sprite thumbnails
- 📄 Pokémon detail: name, sprite, types, base stats
- 🎯 **Counter Advisor**: ranks attacking types by effectiveness vs the selected Pokémon's type(s)
- 💡 Example Pokémon for each super‑effective attacking type (fetched from `/type/{name}`)
- ⚡ MVVM‑ish structure, async `HttpClient`, simple in‑memory cache

> Note: PokeAPI has no direct substring search by name; this demo fetches the full list once and filters locally.

## Run It
1. Open `MonAtlas.sln` in **Visual Studio 2022** (17.x) with **.NET 8** installed.
2. Set `MonAtlas` as the startup project.
3. Press **F5**. Type a name (e.g., `char`, `mimi`, `mira`) and click **Search**.
4. Click a result to view details and counter suggestions.

## Tech
- WPF (.NET 8), `System.Text.Json`
- Small RelayCommand and converter (no external MVVM framework required)
- Hardcoded type chart for instant effectiveness calculations
- PokeAPI endpoints used:
  - `GET /api/v2/pokemon?limit=1302`
  - `GET /api/v2/pokemon/{nameOrId}`
  - `GET /api/v2/type/{name}`

## Roadmap
- Offline cache to disk
- Team builder & simulated DPS (STAB + sample movesets)
- Abilities/items synergy
- Filtering by type/egg group/generation
- Compare two Pokémon side-by-side
- Keyboard navigation + better accessibility

## License
This starter is MIT for your convenience. Pokémon content & names are © Nintendo/Game Freak/The Pokémon Company; PokeAPI data is community‑provided.