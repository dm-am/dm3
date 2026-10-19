/**
 * @vitest-environment node
 */

// Batches asks for a read: one read per pause, and none held off past a ceiling.
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { deferredRead } from "./deferredRead";

const DEBOUNCE = 2000;
const CEILING = 10000;

describe("deferredRead", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const setup = () => {
    const read = vi.fn(async () => {});
    return { read, batch: deferredRead(read, DEBOUNCE, CEILING) };
  };

  it("reads once, a pause after the last ask", async () => {
    const { read, batch } = setup();

    batch.later();
    await vi.advanceTimersByTimeAsync(1500);
    batch.later();
    await vi.advanceTimersByTimeAsync(1500);
    expect(read).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(500);
    expect(read).toHaveBeenCalledTimes(1);
  });

  it("does not let a steady stream of asks hold the read off past the ceiling", async () => {
    const { read, batch } = setup();

    for (let spent = 0; spent < CEILING; spent += 1000) {
      batch.later();
      await vi.advanceTimersByTimeAsync(1000);
    }
    // The tenth second: the first ask of the batch has waited as long as it may.
    batch.later();
    await vi.advanceTimersByTimeAsync(0);

    expect(read).toHaveBeenCalledTimes(1);
  });

  it("starts the ceiling over once a read has been made", async () => {
    const { read, batch } = setup();

    batch.later();
    await vi.advanceTimersByTimeAsync(DEBOUNCE);
    expect(read).toHaveBeenCalledTimes(1);

    batch.later();
    await vi.advanceTimersByTimeAsync(DEBOUNCE - 1);
    expect(read).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(read).toHaveBeenCalledTimes(2);
  });

  it("reads at once on now and drops the read that was waiting", async () => {
    const { read, batch } = setup();

    batch.later();
    await batch.now();
    expect(read).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(CEILING);
    expect(read).toHaveBeenCalledTimes(1);
  });
});
