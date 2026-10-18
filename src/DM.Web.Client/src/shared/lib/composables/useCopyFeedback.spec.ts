/**
 * @vitest-environment jsdom
 */

/**
 * The one answer every copy on the site gives.
 *
 * Three things have to hold or a copy goes silent again: a refused clipboard
 * must be told apart from a written one, the answer must go back on its own,
 * and a second press must not leave the first timer running - otherwise the
 * control drops back to its resting word in the middle of the second answer.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import {
  useCopyFeedback,
  COPIED,
  COPY_FAILED,
  HOLD_MS,
  type CopyFeedback,
} from "./useCopyFeedback";

/** A component that owns one instance, so onUnmounted has somewhere to live. */
function render(): { feedback: CopyFeedback; unmount: () => void } {
  let feedback!: CopyFeedback;
  const wrapper = mount(
    defineComponent({
      setup() {
        feedback = useCopyFeedback();
        return () => null;
      },
    }),
  );
  return { feedback, unmount: () => wrapper.unmount() };
}

let write: ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.useFakeTimers();
  write = vi.fn().mockResolvedValue(undefined);
  Object.assign(navigator, { clipboard: { writeText: write } });
});

afterEach(() => {
  vi.useRealTimers();
});

describe("what a control says about the clipboard", () => {
  it("says nothing before it is pressed", () => {
    const { feedback } = render();

    expect(feedback.outcome.value).toBe("idle");
    expect(feedback.label("Копировать").value).toBe("Копировать");
    expect(feedback.mark.value).toBeNull();
  });

  it("says it copied, in words and in a mark", async () => {
    const { feedback } = render();

    await feedback.copy("/connect 123");

    expect(write).toHaveBeenCalledWith("/connect 123");
    expect(feedback.label("Копировать").value).toBe(COPIED);
    expect(feedback.mark.value).not.toBeNull();
  });

  // The half a plate used to carry that the screen otherwise cannot: a reader
  // who is not told will paste whatever was in the clipboard before.
  it("says it failed when the clipboard refused", async () => {
    write.mockRejectedValue(new Error("not allowed"));
    const { feedback } = render();

    const written = await feedback.copy("/connect 123");

    expect(written).toBe(false);
    expect(feedback.label("Копировать").value).toBe(COPY_FAILED);
    expect(feedback.mark.value).not.toBe(null);
  });

  it("goes back to itself after the hold", async () => {
    const { feedback } = render();
    await feedback.copy("текст");

    vi.advanceTimersByTime(HOLD_MS);

    expect(feedback.outcome.value).toBe("idle");
    expect(feedback.label("Копировать").value).toBe("Копировать");
  });

  // The one a second timer would break: the first press would end the second
  // answer early.
  it("restarts the hold on a second press instead of stacking timers", async () => {
    const { feedback } = render();
    await feedback.copy("первый");

    vi.advanceTimersByTime(HOLD_MS - 100);
    await feedback.copy("второй");
    vi.advanceTimersByTime(200);

    expect(feedback.outcome.value).toBe("copied");

    vi.advanceTimersByTime(HOLD_MS);
    expect(feedback.outcome.value).toBe("idle");
  });

  it("drops its timer with the component", async () => {
    const { feedback, unmount } = render();
    await feedback.copy("текст");

    unmount();

    expect(vi.getTimerCount()).toBe(0);
  });
});
