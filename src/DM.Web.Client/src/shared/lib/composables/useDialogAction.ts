import { computed, nextTick, ref, type ComputedRef, type Ref } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { describeFailure, REQUEST_FAILED } from "@/shared/lib/errors";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import { useRateLimitNotice } from "./useRateLimitNotice";
import { useToast } from "./useToast";

/** The answer of a window's action: `{ data, error }`, `{ error }` or nothing; only error is read. */
export type DialogActionResult = { error?: GeneralError | null } | void;

/** The work the main button of a window does. */
export type DialogAction = () =>
  DialogActionResult | Promise<DialogActionResult>;

export interface DialogActionOptions {
  /** The action succeeded, and the window closes with its result. */
  onSuccess: (result: DialogActionResult) => void;
  /** The session is gone: the window closes and asks nothing. */
  onExpired: () => void;
  /** The sentence for a failure the server did not word itself. */
  failure: () => string | undefined;
  /** Where the caret waits while the pressed button is shut: the cross. */
  spare?: () => HTMLElement | null | undefined;
  /**
   * Whether the place that prints the refusal is still on the page when the
   * answer comes: always for a window, not for a form whose host may fold it.
   * A refusal with nowhere to stand goes up as a plate.
   */
  shown?: () => boolean;
}

export interface DialogActionState {
  /** A request is in flight: every way out of the window is shut. */
  busy: Readonly<Ref<boolean>>;
  /** The server asked to wait: the main button stays shut until zero. */
  waiting: ComputedRef<boolean>;
  /** The line above the row of buttons, empty while there is nothing to say. */
  refusal: ComputedRef<string>;
  /** Runs the action once, however often the button is pressed meanwhile. */
  run: (action: DialogAction) => Promise<void>;
  /** A field of the window shows the server's word on `field`; returns the release. */
  claim: (field: string) => () => void;
  /** What the server said about one field, ready to show under it. */
  fieldError: (field: string) => string | undefined;
  /** Forgets the last answer: the window opened again. */
  reset: () => void;
}

/**
 * The cycle of a request sent from a window: open until the answer, shut while
 * it waits, the refusal printed in itself, closed on success alone. The caller
 * sets ownsRefusal and ownsRateLimit on its request; status 0 and a 401 stay
 * the interceptor's. A thrown action is a failure like any other.
 */
export function useDialogAction(
  options: DialogActionOptions,
): DialogActionState {
  const busy = ref(false);
  const said = ref("");
  const fields = ref<Record<string, string[]>>({});
  const rateLimit = useRateLimitNotice();

  /** Server field names a field of the window shows, with how many show each. */
  const claimed = new Map<string, number>();

  const claim = (field: string) => {
    const key = field.toLowerCase();
    claimed.set(key, (claimed.get(key) ?? 0) + 1);
    let held = true;
    return () => {
      if (!held) return;
      held = false;
      const left = (claimed.get(key) ?? 1) - 1;
      if (left > 0) claimed.set(key, left);
      else claimed.delete(key);
    };
  };

  const reset = () => {
    said.value = "";
    fields.value = {};
    rateLimit.clear();
  };

  /**
   * Moves the caret off the button a request is about to shut: a disabled
   * control drops focus onto <body>, outside the window. Returns the way back.
   */
  const park = (): (() => void) => {
    const spare = options.spare?.();
    const pressed = document.activeElement;
    if (!spare || !(pressed instanceof HTMLButtonElement) || pressed === spare)
      return () => {};
    spare.focus();
    return () =>
      void nextTick(() => {
        if (
          document.activeElement === spare &&
          pressed.isConnected &&
          !pressed.disabled
        )
          pressed.focus();
      });
  };

  /** Puts a refusal where it belongs: under its field, in the line, or nowhere. */
  const settle = (error: GeneralError) => {
    if (error.status === 401) {
      options.onExpired();
      return;
    }
    // Nothing came back: the interceptor has said so already.
    if (error.status === 0) return;
    if (rateLimit.take(error)) return;

    const all = parseApiErrors(error as BadRequestError);
    const rest = Object.fromEntries(
      Object.entries(all).filter(([name]) => !claimed.has(name)),
    );
    fields.value = all;
    const shown = Object.keys(all).length - Object.keys(rest).length;
    if (shown > 0 && !Object.keys(rest).length) return;

    // A field nobody shows: its codes go to the line.
    const unshown: BadRequestError =
      shown > 0 ? { ...error, errors: rest } : (error as BadRequestError);
    said.value = describeFailure(unshown, options.failure() || REQUEST_FAILED);
  };

  const run = async (action: DialogAction) => {
    if (busy.value || rateLimit.waiting.value) return;
    const back = park();
    reset();
    busy.value = true;

    let result: DialogActionResult;
    try {
      result = await action();
    } catch {
      busy.value = false;
      said.value = REQUEST_FAILED;
      back();
      sayWithNoPlace();
      return;
    }
    busy.value = false;

    const error = result ? result.error : null;
    if (!error) {
      options.onSuccess(result);
      return;
    }
    settle(error);
    back();
    sayWithNoPlace();
  };

  /**
   * A refusal whose place left the page while it was on the wire, raised as a
   * plate headed by what was being done; a wait is the interceptor's warning.
   */
  const sayWithNoPlace = () => {
    if (!options.shown || options.shown()) return;
    const waited = rateLimit.message.value;
    // The field left with the rest: the plate names the action instead.
    const text =
      waited ||
      said.value ||
      (Object.keys(fields.value).length
        ? options.failure() || REQUEST_FAILED
        : "");
    reset();
    if (!text) return;
    if (waited) {
      useToast().warning(waited);
      return;
    }
    const doing = options.failure();
    const headed =
      doing && doing !== text
        ? `${doing}: ${text.charAt(0).toLowerCase()}${text.slice(1)}`
        : text;
    useToast().error(headed);
  };

  return {
    busy,
    waiting: rateLimit.waiting,
    refusal: computed(() => rateLimit.message.value || said.value),
    run,
    claim,
    fieldError: (field: string) => readFieldError(fields.value, field),
    reset,
  };
}
