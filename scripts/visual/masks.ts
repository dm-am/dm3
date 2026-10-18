/**
 * The places of a page that are allowed to differ between two runs of the same
 * commit, each with the reason it cannot be pinned. A mask is painted over in
 * the frame, written as "[masked]" in the tree and skipped in the styles.
 *
 * Every entry is a hole in the instrument: a change inside a masked place is
 * not seen. So an entry needs a reason that names what moves and why the stand
 * cannot hold it still, and a mask is the last resort after pinning (the clock,
 * the seed, Math.random) has been tried.
 */
export interface Mask {
  /** CSS selector of the element whose content moves. */
  selector: string;
  /** What moves, and why it cannot be pinned. */
  reason: string;
  /** Only in the states whose name matches; every state when omitted. */
  states?: RegExp;
  /**
   * Only the elements whose text holds a date of the run's own day: the
   * seeded ones beside them stay compared.
   */
  runDate?: true;
}

/**
 * The server's clock is not pinned (only the seed's epoch is), and the dates
 * it writes during a run are the run's. They come from two places: the stand's
 * own sign-ins, and the first passes of the API's periodic jobs, which find a
 * seed months old and warn every game of being idle.
 */
const SERVER_CLOCK =
  "a date the server wrote with its own clock during the run, which the seed does not pin";

export const MASKS: Mask[] = [
  {
    selector: ".site-stats .online",
    reason:
      "The server counts the accounts active in the last minutes by its own clock, which is not " +
      "pinned, and the stand's sessions are those accounts: the number depends on how long the " +
      "run has been going.",
  },
  {
    selector: ".session-date",
    runDate: true,
    reason: `The session the stand signed in with: ${SERVER_CLOCK}.`,
  },
  {
    selector: ".event-date",
    runDate: true,
    reason: `The security log's line about the stand's sign-in: ${SERVER_CLOCK}.`,
  },
  {
    selector: ".notification-date",
    runDate: true,
    reason: `An idle game's warning sent by the API's first pass: ${SERVER_CLOCK}.`,
  },
];

export function masksFor(state: string): Mask[] {
  return MASKS.filter((mask) => !mask.states || mask.states.test(state));
}
