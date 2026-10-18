import { pluralize } from "@/shared/lib/utils/pluralize";

/**
 * What a request refused by the rate limiter says, wherever it is said.
 *
 * Two places say it: the response interceptor, for a request that leaves the
 * refusal to it, and the guest forms, which take the refusal over and stand it
 * in their own strip with the seconds running down. The number is the part a
 * reader acts on, so the two must not be free to word it differently — a second
 * copy of the sentence is the one that drifts.
 *
 * The noun agrees with the number, because the number moves: the sentence is
 * rewritten every second of the countdown, so a single spelling of the word is
 * wrong on most of the seconds a reader actually watches — "через 3 секунд"
 * stood on the way in, which is the one screen a person retries on purpose.
 *
 * A wait longer than a minute and a half is said in minutes, rounded up. The
 * rate limits of the guest forms come back in seconds and are unaffected; the
 * lockout after fifteen wrong passwords is half an hour, and "через 1800
 * секунд" is a number a reader has to do arithmetic on before it means
 * anything.
 *
 * @param retryAfterSeconds Whole seconds the server asked for, when it said.
 */
export function describeRateLimit(retryAfterSeconds?: number): string {
  if (!retryAfterSeconds) return "Слишком много попыток, попробуйте позже";

  if (retryAfterSeconds > 90) {
    const minutes = Math.ceil(retryAfterSeconds / 60);
    const unit = pluralize(minutes, "минуту", "минуты", "минут");
    return `Слишком много попыток, попробуйте через ${minutes} ${unit}`;
  }

  const unit = pluralize(retryAfterSeconds, "секунду", "секунды", "секунд");
  return `Слишком много попыток, попробуйте через ${retryAfterSeconds} ${unit}`;
}
