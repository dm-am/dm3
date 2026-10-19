/**
 * A minute-resolution "now" for per-row time checks, one ref and one interval
 * counted across subscribers: started by the first, stopped by the last.
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

/** Subscribes the caller during setup; released in onUnmounted. */
export function useNowTimestamp(): Ref<number> {
  startTimestampRefresh();
  onUnmounted(() => {
    stopTimestampRefresh();
  });
  return nowTimestamp;
}
