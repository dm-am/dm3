// Lists the API returns in no defined order: the query has no ORDER BY (or
// none for a part of the list), so two runs on the same data may list the
// same rows differently. Both instruments put them in an order of their own:
// the API snapshot before it compares, the frame stand before the page gets
// the answer. Each entry is a defect of the API rather than of the fixture,
// and goes away when the server orders the list.
//
// `order` rearranges the answer's body in place; it receives the body and a
// test of whether a moment was written by the run (the server's clock)
// rather than by the seed.

const byName = (name) => (a, b) =>
  name(a) < name(b) ? -1 : name(a) > name(b) ? 1 : 0;

const moderatorsOf = (board) => {
  if (Array.isArray(board?.moderators))
    board.moderators.sort(byName((m) => m.username));
};

const BOARD_MODERATORS =
  "The moderators of a board come in no order, alone or inside the board.";

const GAME_READERS =
  "The readers of a game (its subscribers, in the readers list and in the game's details) " +
  "come in no order: GameSubscriptionService.GetSubscribersAsync reads the accounts by a list " +
  "of ids without an ORDER BY, and on the running stand every sign-in rewrites an account's " +
  "row, so the rows come back in another order run to run.";

/** @type {{ route: string, reason: string, order: (body: any, written: (moment: unknown) => boolean) => void }[]} */
export const UNORDERED = [
  {
    route: "GET /v1/boards",
    reason: BOARD_MODERATORS,
    order: (body) => body?.resources?.forEach(moderatorsOf),
  },
  {
    route: "GET /v1/boards/{id}",
    reason: BOARD_MODERATORS,
    order: (body) => moderatorsOf(body?.resource),
  },
  {
    route: "GET /v1/boards/{id}/moderators",
    reason: BOARD_MODERATORS,
    order: (body) => body?.resources?.sort(byName((m) => m.username)),
  },
  {
    route: "GET /v1/games/{id}/users",
    reason:
      "The master and the players of a game come in the order they joined; the readers " +
      "after them come in no order.",
    order: (body) => {
      const list = body?.resources;
      if (!Array.isArray(list)) return;
      body.resources = [
        ...list.filter((u) => u.role !== "reader"),
        ...list
          .filter((u) => u.role === "reader")
          .sort(byName((u) => u.user.username)),
      ];
    },
  },
  {
    route: "GET /v1/games/{id}/readers",
    reason: GAME_READERS,
    order: (body) => body?.resources?.sort(byName((r) => r.user?.username)),
  },
  {
    route: "GET /v1/games/{id}/details",
    reason: GAME_READERS,
    order: (body) => {
      const game = body?.resource;
      for (const list of [game?.readers, game?.subscribers])
        if (Array.isArray(list)) list.sort(byName((u) => u.username));
    },
  },
  {
    route: "GET /v1/users/me/notifications",
    reason:
      "The notifications the run's own periodic jobs raise (an idle game's warning, one per " +
      "game) are written by the worker within the same moment, and among themselves they " +
      "come in the order the worker happened to write them. The seeded ones keep their order.",
    order: (body, written) => {
      const list = body?.resources;
      if (!Array.isArray(list)) return;
      const run = list.filter((n) => written(n.createdUtc));
      const rest = list.filter((n) => !written(n.createdUtc));
      body.resources = [
        ...run.sort(byName((n) => `${n.eventType} ${n.url}`)),
        ...rest,
      ];
    },
  },
];

/** The address pattern of a route, for a page that asks it with any ids and query. */
export const addressOf = (route) =>
  new RegExp(
    `${route
      .slice("GET ".length)
      .replace(/\{\w+\}/g, "[^/?]+")
      .replace(/\//g, "\\/")}(\\?|$)`,
  );
