import { ref } from "vue";
import { accountApi } from "@/entities/user";
import { describeFailure, LETTER_NOT_SENT } from "@/shared/lib/errors";
import type { RateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

export type ResendOutcome = "sent" | "waiting" | { refusal: string };

export function useActivationResend(rateLimit: RateLimitNotice) {
  /** A letter already on the wire, so a second press buys no second letter. */
  const resending = ref(false);

  /** How long the link of the letter sent lives, as the answer named it. */
  const hours = ref<number>();

  // The receipt only on "ActivationResent": anything else means no letter went out.
  const resend = async (email: string): Promise<ResendOutcome> => {
    if (resending.value || rateLimit.waiting.value) return "waiting";

    resending.value = true;
    // Claimed at the press, not in the method: the activation page calls it with no strip.
    const { data, error } = await accountApi.recover(email, OWNS_REFUSAL);
    resending.value = false;

    if (error) {
      if (rateLimit.take(error)) return "waiting";
      return { refusal: describeFailure(error, LETTER_NOT_SENT) };
    }

    if (data?.status !== "ActivationResent")
      return { refusal: LETTER_NOT_SENT };

    hours.value = data.linkLifetimeHours;
    return "sent";
  };

  return { resend, hours };
}
