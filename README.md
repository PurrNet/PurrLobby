# PurrLobby

A lobby and matchmaking front-end for [PurrNet](https://purrnet.dev/).

PurrLobby gives you the part of a multiplayer game that's tedious to build but
every game needs: a menu where players create a lobby, share a code or browse
open ones, chat, ready up, and drop into a match together. The UI is already
built. You wire in a backend and your own game scene, and you're done.

> **Status:** beta (`1.0.0-beta.1`). The API is close to stable but may still
> shift before 1.0. Worth pinning a version if you depend on it.

## What's in the box

- Menu flow: main menu, create lobby, join by code, lobby browser, matchmaking,
  and the in-lobby view with a player list and chat.
- An owner-driven game start: once everyone is ready, a short countdown runs,
  the owner allocates a server, and everyone connects.
- Swappable backends. The UI talks to provider interfaces, not a specific
  service, so you can change where lobbies live without touching the menus.

Backends included:

| Provider | Lobbies | Matchmaking | Game servers |
|----------|:-------:|:-----------:|:------------:|
| PurrNet (PurrServices) | yes | — | yes (PurrTransport) |
| Nakama | yes | yes | yes |
| Edgegap | — | yes | yes |

Edgegap matchmaking uses Edgegap's managed matchmaker, which forms the match
and spins up the game server in one step — pair the Edgegap matchmaker with the
Edgegap game allocator and players go straight from the queue into the match.

A provider only advertises what it actually supports through
`LobbyCapabilities`, and the menu hides buttons for anything missing — so a
backend without a lobby browser simply won't show that button.

## Installing

In Unity, open **Window > Package Manager**, choose **Add package from git
URL**, and paste:

```
https://github.com/PurrNet/PurrLobby.git?path=/Assets/Tool#dev
```

Requires Unity 6000.0 or newer and the PurrNet package. Tested on 6000.3.

## Getting started

1. Open `Assets/Tool/Scenes/MenuScene.unity` for a working example.
2. The `LobbyManager` in the scene references a `GameOrchestrator` asset. That
   asset is just four slots: a session, lobby, matchmaking and game-allocator
   provider. There are preset orchestrators for PurrNet and Nakama under
   `Assets/Tool/Providers/.../Preset`.
3. Point the orchestrator at the providers for the backend you want, set your
   own game scene as the one the allocator loads, and press play.

To customise the look, the views are plain prefabs under
`Assets/Tool/Prefabs/Views` — edit them like any other UI.

## Writing your own backend

Implement the provider base classes in `Runtime/Providers` (`SessionProvider`,
`LobbyProvider`, `MatchmakingProvider`, `GameAllocatorProvider`) and the
`ILobby` / `IPlayer` interfaces in `Runtime/Contracts`. The PurrNet provider
under `Assets/Tool/Providers/PurrNet` is the smallest complete example to copy
from.

## License

See [`Assets/Tool/LICENSE.txt`](Assets/Tool/LICENSE.txt).
