import { GUEST_STATES } from "./states/guest";
import { MEMBER_STATES } from "./states/member";
import { STAFF_STATES } from "./states/staff";
import { SHELL_STATES } from "./states/shell";
import { GAMES_STATES } from "./states/games";
import { CHARACTERS_STATES } from "./states/characters";
import { BLOGS_FORUM_STATES } from "./states/blogsForum";
import { CHAT_STATES } from "./states/chat";
import { ACCOUNT_STATES } from "./states/account";
import { PROFILE_STATES } from "./states/profile";
import { MODERATION_STATES } from "./states/moderation";
import { TICKETS_STATES } from "./states/tickets";
import { EDITOR_STATES } from "./states/editor";
import { OTHER_PAGES_STATES } from "./states/otherPages";
import type { State } from "./states/shared";
export type { State } from "./states/shared";
export { COMMON_KEYS } from "./states/commonKeys";

// The states in the order the run takes them: the sections of the site, each in
// its own file under states/ (shared.ts holds the helpers they use).
export const STATES: State[] = [
  ...GUEST_STATES,
  ...MEMBER_STATES,
  ...STAFF_STATES,
  ...SHELL_STATES,
  ...GAMES_STATES,
  ...CHARACTERS_STATES,
  ...BLOGS_FORUM_STATES,
  ...CHAT_STATES,
  ...ACCOUNT_STATES,
  ...PROFILE_STATES,
  ...MODERATION_STATES,
  ...TICKETS_STATES,
  ...EDITOR_STATES,
  ...OTHER_PAGES_STATES,
];
