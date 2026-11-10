import { type State } from "./shared";

export const STAFF_STATES: State[] = [
  // --- Staff -------------------------------------------------------------
  { name: "mentor-home", role: "mentor", url: "/" },
  { name: "mentor-game", role: "mentor", url: "/game/aaaad" },
  { name: "moderator-home", role: "moderator", url: "/" },
  { name: "moderator-moderation", role: "moderator", url: "/moderation" },
  { name: "moderator-bans", role: "moderator", url: "/moderation/bans" },
  {
    name: "moderator-warnings",
    role: "moderator",
    url: "/moderation/warnings",
  },
  { name: "moderator-tickets", role: "moderator", url: "/moderation/tickets" },
  {
    name: "moderator-ticket",
    role: "moderator",
    url: (f) => `/moderation/tickets/${f.ticketId}`,
  },
  {
    name: "moderator-profile",
    role: "moderator",
    url: "/users/RatingNegative",
  },
  { name: "senior-home", role: "senior", url: "/" },
  {
    name: "senior-username-changes",
    role: "senior",
    url: "/moderation/username-changes",
  },
  { name: "senior-violators", role: "senior", url: "/moderation/violators" },
  { name: "admin-home", role: "admin", url: "/" },
  { name: "admin-moderation", role: "admin", url: "/moderation" },
  { name: "admin-tags", role: "admin", url: "/moderation/tags" },
  {
    name: "admin-removed-accounts",
    role: "admin",
    url: "/moderation/removed-accounts",
  },
  {
    name: "admin-profile-moderation",
    role: "admin",
    url: "/users/RatingNegative",
  },
];
