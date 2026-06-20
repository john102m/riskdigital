# Handset Plan

## Overview

Single-page React app with conditional rendering by game phase. No router — phase determines what's on screen. Same pattern as Flutter handset.

## Screens

### 1. Connect Screen
- Enter player name
- Two buttons: **Create Game** / **Join Game** (with code input)
- Persisted name in localStorage for rejoin

### 2. Lobby Screen
- Shows game code (for others to join)
- Player list with assigned colours
- **Start Game** button (host only, min 3 players)
- Future: Add AI button

### 3. Initial Placement Screen
- List of your territories (with current army count)
- Tap a territory to place one army
- Counter: "X armies remaining to place"
- Greyed out when it's not your turn

### 4. Playing Screen — Reinforce Phase
- Reinforcement count: "Place X troops"
- List of owned territories — tap to place
- **Cards** section: show hand, highlight valid sets, trade button
- Forced trade gate if 5+ cards
- **Done Reinforcing** button

### 5. Playing Screen — Attack Phase
- **Source selector**: list of owned territories with >1 army
- **Target selector**: adjacent enemy territories (filtered by source)
- **Dice picker**: 1/2/3 (max based on source armies)
- **Attack** / **Blitz** buttons
- **Combat result**: attacker/defender dice, losses
- **Move troops in** (after capture): slider (min = dice used, max = source - 1)
- **Done Attacking** button

### 6. Playing Screen — Fortify Phase
- **Source selector**: owned territories with >1 army
- **Destination selector**: adjacent owned territories
- **Troop slider**: 1 to (source - 1)
- **Fortify** / **Skip** buttons

### 7. Passive View (other player's turn)
- Current player name + phase indicator
- Feed of actions: "Player X attacked Y from Z", "Player X captured Y"
- Controls disabled / hidden

### 8. Game Over Screen
- Winner announcement
- Stats (territories conquered, troops lost, etc.)
- Rematch / New Game buttons (host)

## Shared UI Elements

- **Turn indicator** (top): whose turn, which phase, your colour
- **Reconnecting overlay**: shown during SignalR disconnect
- **Error toasts**: validation errors from server

## Component Structure

```
src/
├── App.tsx                    — Phase router (conditional render)
├── hooks/
│   └── useConnection.ts      — SignalR setup, reconnect, state listener
├── components/
│   ├── ConnectScreen.tsx
│   ├── LobbyScreen.tsx
│   ├── PlacementScreen.tsx
│   ├── GameScreen.tsx         — Reinforce/Attack/Fortify (sub-views)
│   ├── GameOverScreen.tsx
│   ├── TerritoryList.tsx      — Reusable filtered territory list
│   ├── CardHand.tsx           — Card display + trade UI
│   └── DiceResult.tsx         — Combat result display
└── types/
    └── game.ts                — TypeScript interfaces matching server DTOs
```

## State Management

- No Redux/Zustand — server pushes full `GameState` via SignalR
- `useConnection` hook holds the connection + latest game state
- Components read from game state, invoke hub methods for actions
- localStorage: player name, game code (for rejoin on refresh/wake)

## UX Patterns (proven in Flutter)

- **Animation lock**: disable controls briefly after actions (prevent double-tap)
- **Vibration**: buzz on your turn, different buzz on error
- **Auto-rejoin**: localStorage persists session, reconnect calls `Rejoin`
- **Host-only controls**: start game, restart, rematch — checked server-side too
- **Territory selection via list**: no interactive map on phone (too fiddly on small screens)
- **Filter/search**: territory list filterable by continent or name

## Build Order

1. Connect + Lobby screens (with CreateGame/JoinGame/StartGame)
2. Initial Placement screen
3. Reinforce phase (including card trade)
4. Attack phase (source/target/dice/result/move-in)
5. Fortify phase
6. Game Over screen
7. Polish: rejoin, reconnect overlay, vibration, animation locks
