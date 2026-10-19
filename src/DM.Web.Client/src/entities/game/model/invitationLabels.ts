// One word for each role an invitation offers, wherever one is shown: the
// settings of a game and of a blog, and the account's own invitations.
import type { InvitationType } from "@/shared/api/models/game/invitations";

export const INVITATION_TYPE_LABELS = {
  player: "Игрок",
  reader: "Читатель",
  assistant: "Ассистент",
} satisfies Record<InvitationType, string>;
