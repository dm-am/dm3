import { inject, type ComputedRef, type InjectionKey, type Ref } from "vue";

/**
 * Whether a component stands in a window of the site.
 *
 * Provided by the window (Dialog.vue) and read by the form inside it: a form
 * in a window takes the window's recipe (fields and buttons at the window's
 * control height, the row of two equal halves and no grey footer), and the
 * same form on a page keeps the page's. It is the window that says so and not
 * each form: eleven windows open a form, and a flag written at every one of
 * them is a flag the twelfth forgets.
 */
export const IN_DIALOG: InjectionKey<boolean> = Symbol("in-dialog");

/** True inside a window that provides IN_DIALOG, false everywhere else. */
export const useInDialog = (): boolean => inject(IN_DIALOG, false);

/**
 * What a window that runs an action hands the form inside it.
 *
 * The window owns the request: the busy state, the refusal and the closing
 * after success (useDialogAction). The form owns where they are drawn: its main
 * button, its "Отмена", and the line above the two. So the form asks the
 * window rather than each caller wiring the two together, which is how
 * thirteen windows came to close before the answer.
 */
export interface DialogForm {
  /** The window has an action, and the form's buttons are the window's. */
  routed: ComputedRef<boolean>;
  /** The main button: runs the window's action. */
  submit: () => void;
  /** "Отмена": the same way out as the cross, question included. */
  cancel: () => void;
  /** A request is in flight: the main button is busy, "Отмена" is shut. */
  busy: Readonly<Ref<boolean>>;
  /** The server asked to wait: the main button is shut until zero. */
  waiting: Readonly<Ref<boolean>>;
  /** The line above the buttons, empty while there is nothing to say. */
  refusal: Readonly<Ref<string>>;
  /** A field shows what the server says about `field`; returns the release. */
  claim: (field: string) => () => void;
  /** What the server said about one field, ready to show under it. */
  fieldError: (field: string) => string | undefined;
}

export const DIALOG_FORM: InjectionKey<DialogForm> = Symbol("dialog-form");

/** The window's side of the form inside it, or null outside a window. */
export const useDialogForm = (): DialogForm | null => inject(DIALOG_FORM, null);
