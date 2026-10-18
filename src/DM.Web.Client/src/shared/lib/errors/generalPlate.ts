import { useToast } from "@/shared/lib/composables/useToast";

/**
 * The plate the response interceptor raises for a refusal, held for a moment
 * so the call that pressed can name it or take it over.
 *
 * Two refusals reach every screen through the interceptor and name nothing
 * about the press they answer: a 403 titled with the general sentence of the
 * authorization check (or with no title at all), and a 5xx. The plate used to
 * say just that, the general title of the check or "Ошибка сервера", and a
 * reader who had pressed three buttons in a row could not tell which of them
 * it was about. The call site knows - it holds a sentence like "Не удалось
 * сохранить пост" for exactly this - but it keeps quiet on these statuses
 * (announcedByInterceptor), because a failure gets one plate.
 *
 * So the plate is one, and the two halves come from the two layers that know
 * them: the call site says what it was doing, the interceptor says why it did
 * not happen. "Не удалось сохранить пост: ошибка сервера".
 *
 * A PAGE THAT SAYS IT ITSELF. Most presses have a place on the page: the line
 * under the button that was pressed, the row of the list, the form. There the
 * refusal is written at the place (refusalHere, useActionResult), and a plate
 * in the corner would be the same refusal said twice, far from where the
 * reader looks (the owner's rule of 26 September 2026: a plate only where the
 * page has nowhere to say it). So every refusal the interceptor speaks for -
 * the general ones, the ones the server worded itself, a rate limit - is held
 * the same way, and the call that shows it takes it down (takeOverRefusal)
 * before it goes up. An expired session and a request that never arrived are
 * not held: they are about the reader and the network, not about the press.
 *
 * HOW THE HALVES MEET. The interceptor runs before the caller has its answer,
 * so it does not raise the plate at once: it holds it until the end of the
 * current task. A caller that reads the failure and calls notifyFailure or
 * takes it over does so in the same run of promise callbacks, and hands its
 * phrase over here, or takes the plate down, before the hold runs out. A load
 * that nobody names - a list, a refresh in the background - gets UNNAMED_CALL
 * at the head instead. Both land before the browser paints, so the reader
 * never sees the unnamed plate turn into the named one.
 *
 * A caller that waits on something else first (another request, a timer) is
 * too late to name the plate, and the unnamed one stands. That is the honest
 * end of the rule and not a failure of it: the plate is still there, and still
 * says why.
 */

/** The head of a plate no call named: a load, a refresh in the background. */
export const UNNAMED_CALL = "Запрос не выполнен";

/**
 * The tail of the plate: why the call did not happen, as far as the
 * interceptor can tell. Lowercase, because it follows a colon; the server's
 * own capitalised sentence for a 5xx is SERVER_ERROR, which a form strip
 * quotes whole.
 */
export const GENERAL_REASONS = {
  forbidden: "недостаточно прав",
  server: "ошибка сервера",
} as const;

export type GeneralReason =
  (typeof GENERAL_REASONS)[keyof typeof GENERAL_REASONS];

/** The one line a plate reads: what was being done, and why it was not. */
export const generalPlate = (phrase: string, reason: string): string =>
  `${phrase}: ${reason}`;

/**
 * A plate the interceptor is holding. `raise` puts it up, with the phrase of
 * the call at its head where the plate has room for one.
 */
interface Held {
  raise: (phrase?: string) => void;
  timer: ReturnType<typeof setTimeout>;
}

/**
 * Held plates by the problem document the caller is handed. Weak, so a failure
 * nobody reads leaves nothing behind once its plate is up.
 */
const held = new WeakMap<object, Held>();

/** Holds a plate until the end of the current task, then raises it unnamed. */
function hold(problem: object, raise: (phrase?: string) => void): void {
  const timer = setTimeout(() => {
    held.delete(problem);
    raise();
  }, 0);
  held.set(problem, { raise, timer });
}

/** Takes a held plate off the hold, or undefined when there is none. */
function release(problem: object): Held | undefined {
  const plate = held.get(problem);
  if (!plate) return undefined;
  clearTimeout(plate.timer);
  held.delete(problem);
  return plate;
}

/**
 * Raises the plate for a general refusal, after giving the call a moment to
 * name itself or to say it on the page.
 *
 * @param problem The very object the caller receives as `error`
 * @param reason Why the call failed
 */
export function announceGeneral(problem: object, reason: GeneralReason): void {
  hold(problem, (phrase = UNNAMED_CALL) =>
    useToast().error(generalPlate(phrase, reason)),
  );
}

/**
 * Raises a plate whose words are complete already - a refusal the server
 * worded itself, a rate limit - after giving the call a moment to say it on
 * the page instead. A call's phrase does not go at its head: the sentence
 * names its own subject.
 *
 * @param problem The very object the caller receives as `error`
 * @param raise Puts the plate up
 */
export function announceWorded(problem: object, raise: () => void): void {
  hold(problem, () => raise());
}

/**
 * Puts the call's phrase at the head of the plate the interceptor is holding
 * for this failure, and raises it.
 *
 * @returns Whether there was a plate to name: false for a failure the
 * interceptor has no plate for, and for one whose plate is already up
 */
export function nameGeneral(problem: object, phrase: string): boolean {
  const plate = release(problem);
  if (!plate) return false;
  plate.raise(phrase);
  return true;
}

/**
 * Takes down the plate the interceptor is holding for this failure: the page
 * says the refusal at the place of the press, and one refusal is said once.
 *
 * @returns Whether there was a plate to take down: false for a failure the
 * interceptor has no plate for, and for one whose plate is already up
 */
export function takeOverRefusal(problem: object): boolean {
  return release(problem) !== undefined;
}
