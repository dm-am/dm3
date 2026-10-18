import { computed, ref, type ComputedRef, type Ref } from "vue";
import { userApi, type Username } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import type { EndorsementEligibility } from "@/shared/api/models/community";
import { describeFailure, describeRateLimit } from "@/shared/lib/errors";

/**
 * What the consumer says about the failures of this question.
 *
 * Structural twin of the flags the API client reads off a request by name, the
 * way every module that forwards them keeps its own: a screen that stands the
 * failed question in its own strip takes it over, a screen that shows nothing
 * leaves it to the response interceptor.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

/** What is said when the question itself did not get through. */
const ASK_FAILURE = "Не удалось проверить, можно ли написать рекомендацию";

/**
 * The sentence for a failed question.
 *
 * A wait keeps the shared wording instead of the title the API sent with it:
 * that title names the refusal without the number, and the number is the part
 * a reader acts on. It is also the sentence the response interceptor would
 * have raised, so a consumer taking the limit over costs the reader nothing.
 * Everything else the API named is relayed as it stands.
 */
function askFailure(error: GeneralError): string {
  return error.status === 429
    ? describeRateLimit(error.retryAfterSeconds)
    : describeFailure(error, ASK_FAILURE);
}

/**
 * useEndorsementEligibility — the server's answer to "may I write a
 * recommendation about this user?", held for whoever draws something on it.
 *
 * The create endpoint refuses a guest, a self-recommendation, an author still
 * on probation, a pair that never played in the same game and a pair that
 * already has a recommendation. Only the first two are knowable on the client,
 * so the question is asked rather than guessed, and the answer is produced by
 * the same evaluation the POST refuses by. Two consumers share it: the profile,
 * which offers the control, and the write form, which is that control's
 * destination and re-asks on a direct hit of the URL.
 *
 * The rule that matters most here is what happens when nothing came back:
 * `canCreate` is true only on an explicit yes. A failed request leaves it
 * false, so a site that could not ask does not offer.
 *
 * @param options Who answers for a failed question, and therefore the
 * consumer's to set rather than this composable's: the form prints `askError`
 * in a strip with the read offered again and takes both flags, the profile
 * prints nothing at all — it merely leaves the control undrawn — and passes
 * none, so the interceptor goes on speaking for it. Nailing them on in here
 * would silence the profile's only word about a failed question.
 */
export function useEndorsementEligibility(options?: RefusalFlags): {
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
      // This line speaks for every status, the ones the response interceptor
      // announces included. Without it a failed question is indistinguishable
      // from a "no": `canCreate` stays false, and the form page then sends the
      // reader back to the profile on a verdict the server never gave. A
      // consumer that shows this line takes the refusal over through
      // `options`, and then the plate goes and the line stays; what it says
      // about each status is askFailure's business.
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
