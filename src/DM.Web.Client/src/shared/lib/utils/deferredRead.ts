import { startStopwatch, type Stopwatch } from "./keyedCache";

/**
 * A read that is asked for far more often than it is worth making: `later`
 * waits `debounceMs` after the last ask, but never longer than `maxWaitMs`
 * from the first one of the batch, so a steady stream of asks cannot hold the
 * answer still. `now` reads at once and drops what was waiting.
 */
export function deferredRead(
  read: () => Promise<void>,
  debounceMs: number,
  maxWaitMs: number,
) {
  let timer: ReturnType<typeof setTimeout> | null = null;
  let waitingSince: Stopwatch | null = null;

  const doRead = async () => {
    waitingSince = null;
    await read();
  };

  function cancel() {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
  }

  return {
    async now() {
      cancel();
      await doRead();
    },
    later() {
      cancel();
      if (waitingSince === null) waitingSince = startStopwatch();
      const delay = Math.min(debounceMs, waitingSince.remainingMs(maxWaitMs));
      timer = setTimeout(doRead, delay);
    },
  };
}
