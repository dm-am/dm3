// The refusals of the way in, read by both steps so they read the same.
import type { GeneralError } from "@/shared/api/models/common";
import { formatDate } from "@/shared/lib/utils/datetime";
import {
  describeFailure,
  describeRateLimit,
  refusalHere,
} from "@/shared/lib/errors";

// The login's 403 carries no ban end yet, so a ban reads as one with no term.
type BanRefusal = GeneralError & { banExpiresUtc?: string | null };

// Titles of RefusalMessage.AccountBanned and AccountRemoved; never shown on screen.
const BANNED_TITLE = "Аккаунт забанен";
const REMOVED_TITLE = "Аккаунт удален";

export const isRemoved = (failure: GeneralError) =>
  failure.status === 403 && failure.title === REMOVED_TITLE;

export const readAccountRefusal = (failure: GeneralError): string | null => {
  // Only a 403 is about the account; the lockout is a 403 about the attempts.
  if (failure.status !== 403) return null;

  if (isRemoved(failure)) return "Аккаунт с этой почтой был удален";

  if (failure.title === BANNED_TITLE) {
    const until = (failure as BanRefusal).banExpiresUtc;
    return until
      ? `Аккаунт находится в полном бане до ${formatDate(until)}`
      : "Аккаунт находится в полном бане бессрочно";
  }

  return null;
};

const LOCKED_TITLE = "Вход заблокирован";

/** RefusalMessage.RegistrationPending: the login and the registration's check of the address say it alike. */
export const REGISTRATION_PENDING = "Регистрация не завершена";

// The lockout reads as the rate limit: the answer to both is to wait.
const asSaidHere = (failure: GeneralError, sentence: string) =>
  isLockout(failure) ? describeRateLimit(failure.retryAfterSeconds) : sentence;

// Handed to the rate-limit counter, so one sentence has one set of rules.
export const isLockout = (failure: GeneralError) =>
  failure.status === 403 && failure.title === LOCKED_TITLE;

const NO_ANSWER = "Не удалось войти, попробуйте еще раз";

// A 403 and a dead server are the card's to say; the plate keeps the rest.
export const sayLoginFailure = (failure: GeneralError): string | null => {
  const said =
    failure.status === 403
      ? describeFailure(failure, NO_ANSWER)
      : refusalHere(failure, NO_ANSWER);
  return said ? asSaidHere(failure, said) : null;
};
