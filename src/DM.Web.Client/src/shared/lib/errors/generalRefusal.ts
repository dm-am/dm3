/**
 * The sentences a screen falls back on when the server named nothing.
 *
 * Most refusals are worded by the API and relayed by describeFailure, which is
 * the rule refusalCopy.spec.ts holds: the wording belongs to whoever knows what
 * happened. What is left over is the failure the server did not describe, and
 * every screen used to invent a line of its own for it. Six wordings stood in
 * the tree for three facts, four of them two sentences with a full stop apiece,
 * and a reader could meet two at once: a card wrote its own line into its strip
 * while the interceptor raised a differently worded toast over the top of it.
 *
 * Three facts are left, because a reader acts differently on each:
 *
 *   - the server answered and broke. Nothing about the request was wrong and
 *     the press is worth repeating;
 *   - the request did not go through and nothing said why. It may never have
 *     left the browser, so there is nothing to relay and nothing to correct;
 *   - the letter did not go out. Not the same news, because the reader is about
 *     to go and wait for it, and only this sentence stops him.
 *
 * Each is one sentence and carries no period, the rule for a line on a plate
 * or in a window (sentencePeriods.spec.ts).
 */

/**
 * A 5xx, in the API's own words.
 *
 * The same string the error middleware titles its problem document with, so a
 * toast and the strip of the form under it say one thing and not two. Held here
 * as well because the middleware is not always reached: an edge that refuses a
 * request answers with no document at all, and the toast still has to speak.
 * generalRefusal.spec.ts keeps the two copies equal.
 */
export const SERVER_ERROR = "Ошибка сервера";

/**
 * A 403 in the general words of the authorization check.
 *
 * The API titles every refusal of the intention manager with this one sentence
 * (RefusalMessage.AccessDenied), and it names no reason beyond the refusal
 * itself. The response interceptor has to tell it apart from the refusals that
 * do name one ("Вы в черном списке этого блога"): those it relays whole, and
 * this one it turns into the tail of a plate that starts with the call's own
 * phrase (generalPlate.ts). Not a sentence the client says, so it is not one of
 * the three facts above; generalRefusal.spec.ts keeps it equal to the server's.
 */
export const ACCESS_DENIED = "Недостаточно прав для этого действия";

/**
 * The request failed and nothing in the answer said what failed.
 *
 * The fallback of last resort, for a call whose problem document is empty or
 * never arrived. It is the only one of the three that carries advice, because
 * it is the only one that states nothing a reader can act on by itself.
 */
export const REQUEST_FAILED = "Не удалось выполнить запрос, попробуйте позже";

/**
 * The letter was not sent.
 *
 * Kept apart from the two above on purpose: a screen whose whole outcome is a
 * letter leaves the reader waiting for one that was never posted if it says
 * only that something went wrong.
 */
export const LETTER_NOT_SENT = "Не удалось отправить письмо";
