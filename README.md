# PurrLobby

A drop-in lobby and matchmaking front-end for [PurrNet](https://purrnet.dev/).

PurrLobby gives you the multiplayer menu flow most games need: create a lobby,
share a code, browse open lobbies when the selected backend supports it, chat,
ready up, matchmake, and move everyone into a game scene together. The UI is
already built. You choose a backend, assign your game scene, and customize the
prefabs as needed.

## Documentation

Full documentation lives at [purrnet.dev/docs](https://purrnet.dev/docs). This
README is a quick overview; the docs cover setup, providers, and customization
in depth.

## Requirements

- Unity `6000.0` or newer.
- [PurrNet](https://github.com/PurrNet/PurrNet) and
  [PurrUI](https://github.com/PurrNet/PurrUI).
- PurrServices when using the PurrNet Services lobby provider or Edgegap game
  allocator.
- Optional provider packages:
  - [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) for Steam
    providers.
  - [Nakama Unity](https://github.com/heroiclabs/nakama-unity) for Nakama
    providers.
  - [Edgegap Unity plugin](https://github.com/edgegap/edgegap-unity-plugin) for
    Edgegap workflows.

Provider code is compiled out when its SDK is absent, so you only need the
packages for the backends you actually use.

## Installing

Install through the PurrNet package manager: open **Tools > PurrNet > PurrNet
Packages** (`Ctrl+Shift+Alt+P`), find **PurrLobby** in the list, and install.
PurrUI is listed as a dependency and is installed alongside it. The version
dropdown lets you pick a release or dev build.

The package page is at
[purrnet.dev/packages/purrlobby](https://purrnet.dev/packages/purrlobby).

For local development, clone the repository and open it with a supported Unity
version. The checked-in `Packages/manifest.json` points at the dependency
versions used by the sample project.

## Updating

> **Updating replaces the contents of the package.** Any edits you have made to
> the shipped prefabs, scenes, materials, or scripts are overwritten.

Two ways to keep your customizations:

- **Deselect your files during import.** PurrLobby imports into
  `Assets/PurrLobby`, so updating opens Unity's interactive import window.
  Uncheck anything you have modified to keep your version and take the rest of
  the update.
- **Duplicate before you edit.** Copy any prefab or asset you plan to change
  into your own folder outside `Assets/PurrLobby` and point your scenes at the
  copy. Updates then never touch your version.

The second approach is worth doing up front if you expect to restyle the UI
heavily — it keeps your work fully separate from the package.

## What's Included

- Menu flow: main menu, create lobby, join by code, lobby browser, matchmaking,
  and an in-lobby view with a player list and chat.
- Ready-up and owner-driven game start.
- A scene handoff that loads the game scene, connects the network transport,
  and returns to the menu on leave, game over, or connection loss.
- Swappable provider interfaces for sessions, lobbies, matchmaking, and game
  allocation.
- Prefabs and sample scenes under `Assets/PurrLobby`.

## Providers

| Provider | Lobbies | Lobby Browser | Matchmaking | Game Allocation |
|----------|---------|---------------|-------------|-----------------|
| PurrNet Services | yes | yes | via generic lobby matchmaker | PurrTransport |
| Steam | yes | yes | via generic lobby matchmaker | Steam sockets |
| Nakama | create/join by id or code | basic (ids only) | yes | Nakama relayed match |
| Edgegap | no | no | yes | managed server assignment |

Providers advertise optional lobby actions through `LobbyCapabilities`. The
menu hides unsupported buttons automatically, so a backend without lobby
browsing will not show the browser entry point.

**About the Nakama row:** those entries describe this provider, not Nakama.
Nakama is fully capable of everything in the table — rich lobby listings,
private lobbies, random join — through a custom server module written against
its Go, Lua, or TypeScript runtime. This provider deliberately targets a
**stock Nakama server** so it works with no server-side setup and no extra
dependencies. The trade-off is that relayed matches expose no name, metadata,
or max size to the browser, every match is publicly listed, and query filters
are ignored. Add your own server RPC and extend `QueryLobbies` to lift any of
these limits. Matchmaking is unaffected: `NakamaMatchmakingProvider` runs
Nakama's real matchmaker with tickets and min/max party size, no server module
required, and is the supported path for dropping players into a game together.

Edgegap matchmaking forms the match and returns ready-to-use connection info in
one step. Pair `EdgegapMatchmakingProvider` with `EdgegapGameAllocator` so the
matchmaker and allocator agree on transport and port selection.

## Getting Started

1. Open `Assets/PurrLobby/Scenes/MenuScene.unity` for a working example.
2. Select the `LobbyManager` in the scene and assign a `GameOrchestrator`.
   Preset orchestrators live under `Assets/PurrLobby/Providers/.../Preset`.
3. Choose the session, lobby, matchmaking, and game allocator providers for
   your backend.
4. Set the allocator's game scene to your gameplay scene.
5. In the gameplay scene, keep the `NetworkManager` auto-start flags disabled.
   The allocator starts the host or client after loading the scene.

To customize the UI, edit the prefabs under `Assets/PurrLobby/Prefabs/Views`
and the smaller elements under `Assets/PurrLobby/Prefabs/Elements` — after
reading [Updating](#updating).

## Writing A Backend

Implement the provider base classes in `Assets/PurrLobby/Runtime/Providers`:

- `SessionProvider`
- `LobbyProvider`
- `MatchmakingProvider`
- `GameAllocatorProvider`

Lobby objects should implement the contracts in
`Assets/PurrLobby/Runtime/Contracts`, especially `ILobby`, `IPlayer`,
`IMetadata`, and `ILobbyChat`. The PurrNet provider under
`Assets/PurrLobby/Providers/PurrNet` is the smallest complete example; Nakama
is a fuller reference for relayed-match lobby state.

## License

See [`Assets/PurrLobby/LICENSE.txt`](Assets/PurrLobby/LICENSE.txt).
