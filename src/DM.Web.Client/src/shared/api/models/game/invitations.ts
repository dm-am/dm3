import type { UserRef } from "../common";

export type InvitationType = "assistant" | "player" | "reader";

export interface Invitation {
  id: string;
  gameId: string;
  gameTitle: string;
  invitedUser: UserRef;
  inviterUsername: string;
  type: InvitationType;
  createdUtc: string;
  expiresUtc?: string;
}
