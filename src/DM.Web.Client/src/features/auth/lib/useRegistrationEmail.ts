// A removed account's address answers as taken: a guest at this form is not owed that decision.
import { ref } from "vue";
import { accountApi } from "@/entities/user";
import type { EmailAvailability } from "@/shared/api/models/account";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import type { RateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import { REGISTRATION_PENDING } from "./loginRefusal";

// The server's ceiling (UserRegistrationValidator); typing past it would only buy a refusal.
export const EMAIL_MAX_LENGTH = 100;

// One sentence for the check on blur and the server's refusal: they are the same answer.
const EMAIL_TAKEN_MESSAGE = "Аккаунт с этой почтой уже существует";

const verdictOf = (data: EmailAvailability) => {
  if (data.isAvailable) return { taken: false, pending: false, sentence: null };
  if (data.reason === "PendingActivation") {
    return { taken: false, pending: true, sentence: REGISTRATION_PENDING };
  }
  // Everything else is taken: the server no longer reports a removed account here.
  return { taken: true, pending: false, sentence: EMAIL_TAKEN_MESSAGE };
};

// The limit goes to the strip: a field error clears on the next keystroke, the wait does not.
const askAvailability = async (value: string, rateLimit: RateLimitNotice) => {
  const { data, error } = await accountApi.checkEmail(value);
  if (error) {
    rateLimit.take(error);
    return null;
  }
  if (!data) return null;
  return verdictOf(data);
};

export function useRegistrationEmail(rateLimit: RateLimitNotice) {
  const taken = ref(false);
  const pending = ref(false);

  // The check started on blur; typing drops it.
  const check = ref<Promise<void> | null>(null);

  const clearVerdict = () => {
    taken.value = false;
    pending.value = false;
  };

  const field = useValidatedField({
    validate: validators.combine(validators.required(), validators.email()),
    asyncValidate: async (value) => {
      const verdict = await askAvailability(value, rateLimit);
      if (!verdict) return null;
      taken.value = verdict.taken;
      pending.value = verdict.pending;
      return verdict.sentence;
    },
  });

  /** The press waits for the check in flight instead of asking again. */
  const waitForCheck = async () => {
    if (check.value) await check.value;
  };

  const onBlur = () => {
    const asked = field.value.value.trim();
    const answer = field.onBlur().then(() => {
      // A late answer about an address since rewritten would light the red under the typing.
      if (field.value.value.trim() !== asked) {
        clearVerdict();
        field.onInput();
      }
    });
    check.value = answer;
    void answer.finally(() => {
      if (check.value === answer) check.value = null;
    });
  };

  const onInput = () => {
    field.onInput();
    check.value = null;
    clearVerdict();
  };

  // "Taken" is said in the words of the check on blur, with the same offer beside the label.
  const refusalOf = (errors: Record<string, string[]>) => {
    if (errors.email?.[0] !== "Taken") return readFieldError(errors, "email");
    taken.value = true;
    return EMAIL_TAKEN_MESSAGE;
  };

  const reset = () => {
    check.value = null;
    field.reset();
    clearVerdict();
  };

  return {
    field,
    taken,
    pending,
    waitForCheck,
    onBlur,
    onInput,
    refusalOf,
    reset,
  };
}
