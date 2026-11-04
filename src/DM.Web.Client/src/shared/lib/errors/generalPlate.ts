import { useToast } from "@/shared/lib/composables/useToast";

/**
 * The plate the interceptor raises for a refusal, held until the end of the
 * task so the call that pressed can name it ("Не удалось сохранить пост: ошибка
 * сервера") or take it down to say the refusal on the page. A caller that
 * awaits something else first is too late, and the unnamed plate stands.
 */

/** The head of a plate no call named: a load, a refresh in the background. */
export const UNNAMED_CALL = "Запрос не выполнен";

/** Lowercase, since it follows a colon; the capitalised 5xx title is SERVER_ERROR. */
export const GENERAL_REASONS = {
  forbidden: "недостаточно прав",
  server: "ошибка сервера",
} as const;

export type GeneralReason =
  (typeof GENERAL_REASONS)[keyof typeof GENERAL_REASONS];

/** The one line a plate reads: what was being done, and why it was not. */
export const generalPlate = (phrase: string, reason: string): string =>
  `${phrase}: ${reason}`;

/** A held plate: raise puts it up, with the call's phrase where it has room. */
interface Held {
  raise: (phrase?: string) => void;
  timer: ReturnType<typeof setTimeout>;
}

/** Weak, so a failure nobody reads leaves nothing behind. */
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
 * @param problem The very object the caller receives as `error`
 * @param reason Why the call failed
 */
export function announceGeneral(problem: object, reason: GeneralReason): void {
  hold(problem, (phrase = UNNAMED_CALL) =>
    useToast().error(generalPlate(phrase, reason)),
  );
}

/**
 * A plate already worded (the server's own refusal, a rate limit): no phrase
 * goes at its head.
 *
 * @param problem The very object the caller receives as `error`
 * @param raise Puts the plate up
 */
export function announceWorded(problem: object, raise: () => void): void {
  hold(problem, () => raise());
}

/** @returns false when there is no plate held for this failure */
export function nameGeneral(problem: object, phrase: string): boolean {
  const plate = release(problem);
  if (!plate) return false;
  plate.raise(phrase);
  return true;
}

/** @returns false when there is no plate held for this failure */
export function takeOverRefusal(problem: object): boolean {
  return release(problem) !== undefined;
}
