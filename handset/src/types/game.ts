export type GamePhase = "Lobby" | "InitialPlacement" | "Playing" | "GameOver";
export type TurnPhase = "Reinforce" | "Attack" | "Fortify";

export interface Player {
  connectionId: string;
  name: string;
  colour: string;
  isHost: boolean;
  reinforcementsRemaining: number;
  isEliminated: boolean;
  cardCount: number;
}

export type CardType = "Infantry" | "Cavalry" | "Artillery" | "Wild";

export interface Card {
  territoryId: number | null;
  type: CardType;
}

export interface Territory {
  id: number;
  name: string;
  continent: string;
  ownerId: number;
  armies: number;
  adjacent: number[];
}

export interface GameState {
  gameCode: string;
  phase: GamePhase;
  turnPhase: TurnPhase;
  players: Player[];
  territories: Territory[];
  currentPlayerIndex: number;
  attackFrontIds: number[];
  lastDiceCount: number;
}

export interface CombatResult {
  attackerDice: number[];
  defenderDice: number[];
  attackerLosses: number;
  defenderLosses: number;
  captured: boolean;
  sourceId: number;
  targetId: number;
  sourceArmies: number;
  targetArmies: number;
}
