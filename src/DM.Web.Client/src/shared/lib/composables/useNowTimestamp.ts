/**
 * Shared minute-resolution "now" timestamp
 * @module shared/lib/composables/useNowTimestamp
 *
 * Cached timestamp for per-row time checks (isNew/isOnline in the display
 * composables), refreshed every minute to avoid creating Date objects per
 * row. One module-level ref backed by a single interval, reference-counted
 * across all subscribers: the timer starts with the first one and stops when
 * the last one unmounts.
 */
import { ref, onUnmounted, type Ref } from "vue";

const nowTimestamp = ref(Date.now());
let intervalId: ReturnType<typeof setInterval> | null = null;
let instanceCount = 0;

function startTimestampRefresh() {
  if (intervalId === null) {
    intervalId = setInterval(() => {
      nowTimestamp.value = Date.now();
    }, 60_000); // Refresh every minute
  }
  instanceCount++;
}

function stopTimestampRefresh() {
  instanceCount--;
  if (instanceCount <= 0 && intervalId !== null) {
    clearInterval(intervalId);
    intervalId = null;
    instanceCount = 0;
  }
}

/**
 * Subscribes the calling component to the shared timestamp and returns the
 * singleton ref. Must be called during setup: the subscription is released
 * in `onUnmounted`.
 */
export function useNowTimestamp(): Ref<number> {
  startTimestampRefresh();
  onUnmounted(() => {
    stopTimestampRefresh();
  });
  return nowTimestamp;
}
