import { computed, ref, type ComputedRef, type Ref } from "vue";
import { userApi, type Username } from "@/entities/user";
import type { EndorsementEligibility } from "@/shared/api/models/community";
import { describeFailure } from "@/shared/lib/errors";

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
 */
export function useEndorsementEligibility(): {
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
    const { data, error } = await userApi.getEndorsementEligibility(username);
    asking.value = false;
    if (error) {
      askError.value = describeFailure(
        error,
        "Не удалось проверить, можно ли написать рекомендацию",
      );
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
