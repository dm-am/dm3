/** One die of a roll: its value, and whether it exploded or came up critical. */
export interface DiceResult {
  value: number;
  exploded?: boolean;
  critical?: boolean;
}

/** The total is not sent: it is the sum of the values and the bonus. */
export interface DiceRoll {
  rolls: number;
  edges: number;
  bonus: number;
  explosion?: number | null;
  results: DiceResult[];
  comment?: string;
}

/** A roll a new post asks the server to throw. */
export interface DiceRollInput {
  /** The edges, Y in XdY. */
  dice: number;
  /** X in XdY; the server takes 1 when left out. */
  count?: number;
  bonus?: number;
  /** The cap of explosions per die; none when left out. */
  explosion?: number;
  public?: boolean;
  comment?: string;
}
