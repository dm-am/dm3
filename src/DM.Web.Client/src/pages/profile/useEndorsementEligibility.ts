import { computed, ref, type ComputedRef, type Ref } from "vue";
import { userApi, type Username } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import type { EndorsementEligibility } from "@/shared/api/models/community";
import { describeFailureOrWait } from "@/shared/lib/errors";
import type { RequestOptions } from "@/shared/api";

/** What is said when the question itself did not get through. */
const ASK_FAILURE = "Не удалось проверить, можно ли написать рекомендацию";

/** A failed question in words; a wait in the shared wording, which names the seconds. */
function askFailure(error: GeneralError): string {
  return describeFailureOrWait(error, ASK_FAILURE);
}

/**
 * The server's answer to "may I write a recommendation about this user?": two
 * of the five conditions are knowable here, so it is asked. `canCreate` is true
 * only on an explicit yes, so a site that could not ask does not offer.
 *
 * @param options Set by the consumer: the form shows `askError` and takes both
 * flags, the profile shows nothing and leaves the interceptor to speak.
 */
export function useEndorsementEligibility(options?: RequestOptions): {
  /** True only on an explicit yes from the server. */
  canCreate: ComputedRef<boolean>;
  /** The server's own refusal sentence, "" when there is none. */
  refusal: ComputedRef<string>;
  /** The question is in flight — neither offer nor refusal is settled yet. */
  asking: Ref<boolean>;
  /** The question itself failed; not a refusal, a failure. */
  askError: Ref<string>;
  ask: (username: Username) => Promise<void>;
  clear: () => void;
} {
  const answer = ref<EndorsementEligibility | null>(null);
  const asking = ref(false);
  const askError = ref("");

  const canCreate = computed(() => answer.value?.canCreate === true);
  const refusal = computed(() =>
    canCreate.value ? "" : (answer.value?.reason ?? ""),
  );

  async function ask(username: Username) {
    answer.value = null;
    askError.value = "";
    asking.value = true;
    const { data, error } = await userApi.getEndorsementEligibility(
      username,
      options,
    );
    asking.value = false;
    if (error) {
      // Without it a failed question is a "no", and the form would send the reader back on a verdict never given.
      askError.value = askFailure(error);
      return;
    }
    answer.value = data?.resource ?? null;
  }

  /** Forget the answer — the viewer or the profile it was about has changed. */
  function clear() {
    answer.value = null;
    askError.value = "";
    asking.value = false;
  }

  return { canCreate, refusal, asking, askError, ask, clear };
}
