// The fields of the API answers that differ between two runs of one commit on
// a fresh pinned stand, each with what moves and why it cannot be pinned. The
// value is replaced by "<field>" wherever the field occurs (on the routes that
// match, and only for the values `when` accepts, when given); null stays null,
// so a field that appears or disappears is still a difference. A field of "*"
// is any field.
//
// Every entry hides a change of that field from the snapshot, so it needs a
// reason, and the narrowest condition that holds is the one to write.

const DAY = 24 * 60 * 60_000;

/**
 * A moment of the server's own clock, written during this run or during the
 * run that took the base: within a day of when either started. On a stand
 * this tool rebuilt the seed lays its moments out around the pinned epoch,
 * months away from any real run. On the running stand the epoch is the moment
 * of the last seed, often the same day, and the band of the base's run is
 * what keeps the seed's moments near it masked alike in the base and in the
 * diff; the base's own sign-ins stay on that stand as well.
 */
const writtenByRun = (value, { started, baseStarted }) =>
  typeof value === "string" &&
  /^\d{4}-\d{2}-\d{2}T/.test(value) &&
  [started, baseStarted].some(
    (moment) =>
      Number.isFinite(moment) && Math.abs(Date.parse(value) - moment) < DAY,
  );

/** @type {{ field: string, reason: string, routes?: RegExp, when?: (value: unknown, run: { started: number, baseStarted?: number }) => boolean }[]} */
export const VOLATILE = [
  {
    field: "traceId",
    reason:
      "The id of the request's trace, drawn per request by the tracing middleware.",
  },
  {
    field: "*",
    reason:
      "The server's clock is not pinned. What it stamps during a run - the snapshot's own " +
      "sign-ins (last activity, sessions, the security log), the first passes of the periodic " +
      "jobs on a stand whose seed is months old (an inactive game's warning) - carries the " +
      "moment of that run.",
    when: writtenByRun,
  },
  {
    field: "expirationUtc",
    routes: /^GET \/v1\/account\/sessions$/,
    reason:
      "The end of the snapshot's own session: its sign-in by the server's clock plus the " +
      "lifetime, a year away from the run and so outside the band of writtenByRun.",
  },
  {
    field: "online",
    routes: /^GET \/v1\/stats$/,
    reason:
      "Accounts active in the last minutes by the server's clock: the snapshot's own sessions, " +
      "and how many of them are still counted depends on how long the run has been going.",
  },
];

/**
 * Pieces of any string that are rewritten wherever they occur, a field of
 * their own or part of an address.
 *
 * @type {{ pattern: RegExp, replacement: string, reason: string }[]}
 */
export const REWRITES = [
  {
    pattern:
      /\b[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b/g,
    replacement: "<server-drawn id>",
    reason:
      "An identifier the server drew itself (Guid.NewGuid, version 4) for what its periodic " +
      "jobs write during a run: an idle game's warning, the digest of a closed period. The seed's " +
      "identifiers come from its generator and stay; one in 64 of them happens to look like a " +
      "version-4 one and is rewritten too.",
  },
];

/**
 * Lists to which every sign-in of a run adds a row: the snapshot and the frame
 * stand sign their accounts in anew each run, so the sessions and the security
 * log of those accounts grow by a row a run. With its moment and its drawn id
 * put aside, such a row is the same as the one the previous run left, and only
 * one of each kind is kept: what the rows say is still compared, how many
 * times the stand signed in is not. The kept rows of the run stand together
 * where the first of them stood, in the order of `key`: the list is newest
 * first, and among rows whose moments are masked the newest is whichever kind
 * signed in last. Only rows the run's band wrote collapse, so a seeded row is
 * never folded into another.
 *
 * `key` is what is left of a row once its moment and id are put aside.
 *
 * @type {{ route: string, reason: string, list: (body: any) => any[] | undefined, moment: string, key: (row: any) => string }[]}
 */
export const REPEATED = [
  {
    route: "GET /v1/account/sessions",
    reason:
      "Each sign-in of a run opens a session of its own; the sessions of the stand's previous " +
      "runs stay until they expire, a year later. The current session leads the list, as the " +
      "server puts it.",
    list: (body) => body?.resources,
    moment: "createdUtc",
    key: ({ id, createdUtc, expirationUtc, ...rest }) =>
      `${rest.isCurrent ? 0 : 1} ${JSON.stringify(rest)}`,
  },
  {
    route: "GET /v1/account/logs",
    reason:
      "Each sign-in of a run writes a line to the account's security log, and the log keeps them all.",
    list: (body) => body?.resources,
    moment: "timestampUtc",
    key: ({ id, timestampUtc, ...rest }) => JSON.stringify(rest),
  },
];

/**
 * Folds the rows of `rule` that `written` takes for the run's into one of
 * each kind, in the order of their keys, where the first of them stood.
 */
export function collapseRepeated(body, rule, written) {
  const rows = rule.list(body);
  if (!Array.isArray(rows)) return;
  const run = new Map();
  let at = -1;
  const rest = [];
  for (const row of rows) {
    if (!written(row?.[rule.moment])) {
      rest.push(row);
      continue;
    }
    if (at < 0) at = rest.length;
    const key = rule.key(row);
    if (!run.has(key)) run.set(key, row);
  }
  if (at < 0) return;
  const kept = [...run.keys()].sort().map((key) => run.get(key));
  rows.splice(0, rows.length, ...rest.slice(0, at), ...kept, ...rest.slice(at));
}

/**
 * The accounts the stand's tools sign in (roles.ts, and the snapshot's own
 * three of them in api.mjs).
 */
const STAND_ACCOUNTS = new Set([
  "TestUser",
  "TestMentor",
  "TestModerator",
  "TestSeniorMod",
  "SolohinLex",
]);

/**
 * Lists the server orders by the last activity of an account, most recent
 * first: the preview of a target's subscribers (subscriberUsernames, by
 * SubscriberSummaries) and of an account's own (subscribers, by
 * UserRepository). The stand's accounts lead them, in the order the last tool
 * that ran signed them in and the last of their requests stamped them: the
 * frame stand signs in five of them, the snapshot three, so one run of either
 * between a base and a diff reorders them. Among themselves they are put in the
 * order of their names, in the places they hold; every other name stays where
 * the server put it.
 */
export const ACTIVITY_ORDERED = {
  fields: new Set(["subscriberUsernames", "subscribers"]),
  reason:
    "Ordered by the last activity of the account, which on the running stand is the moment " +
    "the stand's own tools last signed its accounts in.",
};

/** Puts the stand's accounts of an activity-ordered list in the order of their names. */
export function orderStandAccounts(list) {
  const nameOf = (item) => (typeof item === "string" ? item : item?.username);
  const slots = [];
  list.forEach((item, at) => {
    if (STAND_ACCOUNTS.has(nameOf(item))) slots.push(at);
  });
  const sorted = slots
    .map((at) => list[at])
    .sort((a, b) =>
      nameOf(a) < nameOf(b) ? -1 : nameOf(a) > nameOf(b) ? 1 : 0,
    );
  slots.forEach((at, i) => {
    list[at] = sorted[i];
  });
}
