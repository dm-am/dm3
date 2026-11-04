/**
 * The bot trap of a guest form: a field a person never fills (HoneypotField),
 * sent as `website`, and a least time that is sat out rather than refused,
 * since a password manager submits faster than a person.
 */
import { onMounted, ref } from "vue";

export function useBotTrap(options: { minFillMs?: number } = {}) {
  const { minFillMs = 0 } = options;

  /** What the hidden field holds; empty unless something filled it in. */
  const honeypot = ref("");

  // The moment the form went on screen, which is where its fill time starts.
  let loadedAt = 0;
  onMounted(() => {
    loadedAt = Date.now();
  });

  /** Waits out whatever is left of the minimum fill time, if anything is. */
  const sitOut = async (): Promise<void> => {
    const left = minFillMs - (Date.now() - loadedAt);
    if (left > 0) await new Promise((resolve) => setTimeout(resolve, left));
  };

  return { honeypot, sitOut };
}
