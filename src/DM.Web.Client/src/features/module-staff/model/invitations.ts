import type { ApiResult, UserRef } from "@/shared/api/models/common";
import type { InvitationType } from "@/shared/api/models/game/invitations";
import { formatDateFull } from "@/shared/lib/utils/datetime";

/** An invitation of a game or a blog, as far as their settings show it. */
export interface ModuleInvitation {
  id: string;
  invitedUser: UserRef;
  type: InvitationType;
  createdUtc: string;
}

/** A request about the module, or null when the store holds the module no more. */
export type ModuleRequest<T> = Promise<ApiResult<T>> | null;

const sentAt = (invitation: ModuleInvitation) =>
  new Date(invitation.createdUtc).getTime();

/** Oldest first, so the invitation just sent is the row next to the controls. */
export const bySentTime = (a: ModuleInvitation, b: ModuleInvitation) =>
  sentAt(a) - sentAt(b);

export const sentLabel = (invitation: ModuleInvitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;
