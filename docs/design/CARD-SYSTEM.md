# Card System — Design & Decisions

## Overview

44-card deck: 42 territory cards (1 per territory) + 2 wilds. Each territory card has a troop type (Infantry, Cavalry, or Artillery). Players earn cards by capturing at least one territory per turn. Trade sets of 3 for escalating army bonuses.

---

## Earning Cards

- First capture in a turn earns one card (drawn from shuffled deck)
- Only one card per turn, regardless of how many territories captured
- Server tracks `EarnedCardThisTurn` flag, deals card at end of attack phase (or when moving to Fortify)
- If deck is empty: no card earned (rare — only in very long games)

---

## Valid Trade Sets

Three cards form a valid set if:
1. **All same type** — 3× Infantry, 3× Cavalry, or 3× Artillery
2. **All different** — 1× Infantry + 1× Cavalry + 1× Artillery
3. **Any 2 + Wild** — wild substitutes for any type

Player selects exactly 3 cards to trade. Server validates the combination.

---

## Escalation (Global)

| Trade # | Armies Received |
|---------|-----------------|
| 1 | 4 |
| 2 | 6 |
| 3 | 8 |
| 4 | 10 |
| 5 | 12 |
| 6 | 15 |
| 7+ | +5 each (20, 25, 30...) |

`CardTradeCount` is global — shared across all players. This accelerates the game as it progresses.

### House Rule: Fixed Card Values (default ON)

Classic UK/European edition rules — set type determines value, no escalation:

| Set | Armies |
|-----|--------|
| 3× Infantry | 4 |
| 3× Cavalry | 6 |
| 3× Artillery | 8 |
| One of each | 10 |

Wilds fill in as the type they complete. Controlled by `HouseRules.FixedCardValues`. When enabled, `CardTradeCount` is not incremented.

---

## Territory Bonus

If any of the 3 traded cards matches a territory the player currently owns → +2 armies placed automatically on that territory. Multiple matches = +2 each. This happens server-side with no player choice required.

---

## Forced Trade

- **Start of turn:** If player has 5+ cards at the start of Reinforce → must trade before placing any armies
- **Elimination capture:** If taking an eliminated player's cards pushes you above 5 → must trade immediately (mid-attack)

---

## When Trading Happens

| Trigger | Phase | Flow |
|---------|-------|------|
| Voluntary | Reinforce | Player taps "Trade Cards" before/during placement |
| Forced (5+ at turn start) | Reinforce | Trade UI shown first, can't place until traded down to <5 |
| Forced (elimination) | Attack | Overlay/modal interrupts attack flow, trade then resume |

---

## Handset UI

### Card Display
- **Persistent badge** in header: card count icon (e.g. "🃏 3")
- Tap badge or "Trade Cards" button → expands to show hand
- Cards shown as a list: territory name + troop type icon (⚔️ Infantry, 🐎 Cavalry, 💣 Artillery, 🌟 Wild)

### Trade Flow
1. Card list shown with tap-to-select (highlight selected, max 3)
2. "Trade" button enables when exactly 3 selected and set is valid
3. Server responds with armies granted → added to reinforcement pool
4. Territory bonus armies placed automatically (toast/flash notification: "+2 on Brazil")

### Forced Trade (elimination mid-attack)
- Modal overlay on AttackScreen: "You captured [player]'s cards. You must trade."
- Same card selection UI as Reinforce trade
- Dismiss after trade(s) complete → resume attack

### When Not Your Turn
- Badge still shows your card count (private info, only you see it)
- No trade actions available

---

## Server Implementation

### Model

```csharp
public enum CardType { Infantry, Cavalry, Artillery, Wild }

public record Card(int? TerritoryId, CardType Type);
// TerritoryId is null for Wild cards
```

### GameState Additions

```csharp
public List<Card> Deck { get; set; }           // shuffled, draw from top
public int CardTradeCount { get; set; }         // global escalation counter
// Per-player: List<Card> Cards, bool EarnedCardThisTurn
```

### Hub Methods

| Method | Params | Notes |
|--------|--------|-------|
| `TradeCards` | int[] cardIndices (3) | Validates set, calculates bonus, places territory bonus, removes cards, returns to deck (shuffled back in) |

### Broadcasts

| Event | Payload | To |
|-------|---------|-----|
| `CardsUpdated` | player's hand | Caller only |
| `CardTraded` | playerIndex, armiesReceived | All (TV shows event) |
| `CardEarned` | — | Caller only (just increment count) |

---

## TV Display

- Cards are private — TV does NOT show hands
- TV shows event notifications: "John traded cards → +10 armies"
- Optional: show card count per player in the player bar (public info in standard rules? **Decision needed** — see below)

---

## Decisions

1. **Card counts are public.** Hand contents are private. Show count per player in TV player bar / debug GUI.
2. **Traded cards return to deck and are shuffled back in.**
3. **Elimination: trade all forfeited cards** until ≤4 remain (multiple trades in sequence if needed).

---

## Implementation Order

1. Server: Card model, deck generation, shuffle
2. Server: `EarnCard` logic (end of attack phase if captured)
3. Server: `TradeCards` hub method (validate set, escalation, territory bonus)
4. Server: Forced trade gate (start of Reinforce, post-elimination)
5. Handset: Card badge + expandable hand view
6. Handset: Trade selection UI (tap 3, confirm)
7. Handset: Forced trade modal (elimination interrupt)
8. TV: Trade event notification

---

*Review and confirm decisions, then we build.*
