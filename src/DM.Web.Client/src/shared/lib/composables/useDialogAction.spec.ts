/**
 * @vitest-environment jsdom
 */

/**
 * The cycle of a request sent from a window, asked without a window around it.
 * The window's own spec (shared/ui/Layout/Dialog.spec.ts) asks the same rules
 * of the screen; this one holds the edges the screen reaches only by chance.
 */
import { describe, it, expect, vi } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import {
  useDialogAction,
  type DialogActionOptions,
  type DialogActionState,
} from "./useDialogAction";

const host = (options: Partial<DialogActionOptions> = {}) => {
  let state!: DialogActionState;
  const onSuccess = vi.fn();
  const onExpired = vi.fn();
  mount(
    defineComponent({
      setup() {
        state = useDialogAction({
          onSuccess,
          onExpired,
          failure: () => undefined,
          ...options,
        });
        return () => null;
      },
    }),
  );
  return { state, onSuccess, onExpired };
};

const refused = (errors: Record<string, string[]>) => async () => ({
  error: { type: "", title: "", status: 400, traceId: "t", errors },
});

describe("a request sent from a window", () => {
  it("gives a field's refusal back to the line once the field is gone", async () => {
    const { state } = host();
    const release = state.claim("Reason");

    await state.run(refused({ reason: ["Long"] }));
    expect(state.fieldError("reason")).toBe("Слишком длинное значение");
    expect(state.refusal.value).toBe("");

    release();
    await state.run(refused({ reason: ["Long"] }));
    expect(state.refusal.value).toBe("Слишком длинное значение");
  });

  it("keeps a field claimed while another field still shows it", async () => {
    const { state } = host();
    const first = state.claim("reason");
    state.claim("reason");

    first();
    first();
    await state.run(refused({ reason: ["Empty"] }));

    expect(state.refusal.value).toBe("");
  });

  it("says the general sentence when the window names none", async () => {
    const { state } = host();

    await state.run(async () => ({
      error: { type: "", title: "", status: 404, traceId: "t" },
    }));

    expect(state.refusal.value).toBe(
      "Не удалось выполнить запрос, попробуйте позже",
    );
  });

  it("runs nothing while the server asks to wait", async () => {
    vi.useFakeTimers();
    try {
      const { state } = host();
      const action = vi.fn(async () => ({
        error: {
          type: "",
          title: "",
          status: 429,
          traceId: "t",
          retryAfterSeconds: 5,
        },
      }));

      await state.run(action);
      await state.run(action);

      expect(action).toHaveBeenCalledTimes(1);
      expect(state.waiting.value).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it("closes on success and on its own expired session, and on nothing else", async () => {
    const { state, onSuccess, onExpired } = host();

    await state.run(async () => ({
      error: { type: "", title: "", status: 0, traceId: "t" },
    }));
    expect([onSuccess.mock.calls.length, onExpired.mock.calls.length]).toEqual([
      0, 0,
    ]);

    await state.run(async () => ({
      error: { type: "", title: "", status: 401, traceId: "t" },
    }));
    expect(onExpired).toHaveBeenCalledTimes(1);

    await state.run(async () => ({ data: 1, error: null }));
    expect(onSuccess).toHaveBeenCalledWith({ data: 1, error: null });
  });
});
