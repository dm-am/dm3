import { computed, nextTick, ref, type ComputedRef, type Ref } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { describeFailure, REQUEST_FAILED } from "@/shared/lib/errors";
import { getFieldError, parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { useRateLimitNotice } from "./useRateLimitNotice";
import { useToast } from "./useToast";

/**
 * What the action of a window answers with: the `{ data, error }` the api
 * client resolves to, a store's `{ error }`, or nothing at all for an action
 * that asks no server. Only `error` is read.
 */
export type DialogActionResult = { error?: GeneralError | null } | void;

/** The work the main button of a window does. */
export type DialogAction = () =>
  | DialogActionResult
  | Promise<DialogActionResult>;

export interface DialogActionOptions {
  /** The action succeeded, and the window closes with its result. */
  onSuccess: (result: DialogActionResult) => void;
  /**
   * The window's own request found the session gone. The window closes and
   * asks nothing: there is no session left to send the typed text with.
   */
  onExpired: () => void;
  /** The sentence for a failure the server did not word itself. */
  failure: () => string | undefined;
  /**
   * Where the caret waits while the button that was pressed is shut: the
   * cross, which stays in the tab order for the length of the request.
   */
  spare?: () => HTMLElement | null | undefined;
  /**
   * Whether the place that prints the refusal is still on the page when the
   * answer comes. A window shuts every way out while it waits, so it always
   * is. A form or a question on the page is opened and closed by its host,
   * and the host's control stays live meanwhile: the form folds, the row it
   * stood in scrolls out of a virtual list, another row takes the question.
   * The request carried `ownsRefusal`, so the interceptor said nothing, and a
   * refusal with nowhere to stand would be said by nobody. It goes up as a
   * plate then - a plate is for a refusal the page has no place for - and
   * the next open starts clean.
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
  /**
   * Says that a field of the window shows what the server says about
   * `field`, so the refusal is put under it and not in the line. Returns the
   * release, for a field that leaves the window.
   */
  claim: (field: string) => () => void;
  /** What the server said about one field, ready to show under it. */
  fieldError: (field: string) => string | undefined;
  /** Forgets the last answer: the window opened again. */
  reset: () => void;
}

/**
 * The cycle of a request sent from a window, owned in one place.
 *
 * Every window of the site used to write it by hand, and each wrote a
 * different half of it. Thirteen confirmations closed before the request went
 * out and seven closed on any answer, so a refusal fell as a plate on a page
 * whose window was gone; six more caught an exception the client never throws
 * and closed as if the request had succeeded. A window that runs its request
 * through here stays open until the answer, is shut while it waits, prints the
 * refusal in itself and closes on success alone.
 *
 * Who speaks for which answer is the rule of the whole site (UI_STANDARDS,
 * "Всплывающие сообщения"), bent for a window in one way: the request carries
 * `ownsRefusal` and `ownsRateLimit`, so the interceptor keeps quiet about a
 * 403, a 429 and a 5xx and the window says them in its line. The flags ride on
 * the call the caller writes, which this cannot see, so they are the caller's
 * to set. Two answers stay the interceptor's whatever the flags say: a request
 * that never arrived (status 0) is about the network and the interceptor offers
 * the other address, and a 401 ends the session, which closes the window.
 *
 * An exception thrown by the action is a failure like any other. The action is
 * the caller's and may do more than ask the server - update a list, a store -
 * and a window left busy by a throw would be a trap with no way out but a
 * reload.
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
   * Moves the caret off the button a request is about to shut. The browser
   * drops focus from a control that turns disabled onto <body>, outside the
   * window, where neither Escape nor Tab reaches the window any more; the
   * spare is still in the window and still in the tab order. Returns the way
   * back, for when the button is live again.
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
    // Nothing came back: the interceptor has said so already, and the line
    // stays empty rather than say it a second time in other words.
    if (error.status === 0) return;
    if (rateLimit.take(error)) return;

    const all = parseApiErrors(error as BadRequestError);
    const rest = Object.fromEntries(
      Object.entries(all).filter(([name]) => !claimed.has(name)),
    );
    fields.value = all;
    const shown = Object.keys(all).length - Object.keys(rest).length;
    if (shown > 0 && !Object.keys(rest).length) return;

    // A field nobody shows would take its refusal with it: those codes go to
    // the line, read the way describeFailure reads them.
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
   * A refusal whose place left the page while it was on the wire (`shown`),
   * raised as a plate with the sentence of what was being done at its head,
   * and forgotten here. A wait the server asked for is the interceptor's
   * warning, word for word.
   */
  const sayWithNoPlace = () => {
    if (!options.shown || options.shown()) return;
    const waited = rateLimit.message.value;
    // What the server said about a field stood under that field, which has
    // left with the rest: the plate names the action instead.
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
    fieldError: (field: string) => getFieldError(fields.value, field),
    reset,
  };
}
