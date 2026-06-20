export type GamePhase = "Lobby" | "InitialPlacement" | "Playing" | "GameOver";
export type TurnPhase = "Reinforce" | "Attack" | "Fortify";

export interface Player {
  connectionId: string;
  name: string;
  colour: string;
  isHost: boolean;
  reinforcementsRemaining: number;
  isEliminated: boolean;
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
}
