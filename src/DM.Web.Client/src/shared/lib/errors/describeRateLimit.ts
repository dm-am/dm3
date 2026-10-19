import { pluralize } from "@/shared/lib/utils/pluralize";

/**
 * The rate-limit sentence of the interceptor and the guest forms alike. The
 * noun agrees with the number, rewritten each second of the countdown; past a
 * minute and a half the wait is said in minutes, rounded up.
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
