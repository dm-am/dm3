import {
  computed,
  ref,
  toValue,
  watch,
  type ComputedRef,
  type MaybeRefOrGetter,
  type Ref,
} from "vue";

/** Which code the field is taking. */
export type CodeMode = "digits" | "recovery";

/**
 * Crockford's alphabet, as DM.Domain.Account RecoveryCodeFactory spells it: no
 * I, no L, no O, no U.
 */
const CROCKFORD = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

/** TotpCalculator.Digits: the length of a code from the authenticator. */
const DIGIT_LENGTH = 6;

/** RecoveryCodeFactory.CodeLength: symbols one recovery code is made of. */
const RECOVERY_LENGTH = 16;

/** RecoveryCodeFactory.GroupSize: symbols shown between separators. */
const RECOVERY_GROUP = 4;

/** Dashes a full recovery code is shown with. */
const RECOVERY_SEPARATORS = Math.ceil(RECOVERY_LENGTH / RECOVERY_GROUP) - 1;

const GROUPS = new RegExp(`(.{${RECOVERY_GROUP}})(?=.)`, "g");

/**
 * What the field itself puts between the groups, and what a code pasted from a
 * letter or a password manager carries between them. Losing one of these to the
 * parse costs nothing: the field writes them back in.
 */
const SEPARATORS = /[-\s]/g;

/**
 * The code as the server keeps it, by the server's own parse: six digits is a
 * device code, anything else a recovery code (RecoveryCodeFactory.Normalize).
 */
const significant = (mode: CodeMode, raw: string): string => {
  if (mode !== "recovery") {
    return raw.replace(/[^0-9]/g, "").slice(0, DIGIT_LENGTH);
  }

  let kept = "";
  for (const character of raw) {
    if (kept.length === RECOVERY_LENGTH) break;
    const upper = character.toUpperCase();
    const mapped =
      upper === "I" || upper === "L" ? "1" : upper === "O" ? "0" : upper;
    if (CROCKFORD.includes(mapped)) kept += mapped;
  }
  return kept;
};

/** The same code as the field shows it: groups of four, dashes put in for him. */
const formatted = (mode: CodeMode, raw: string): string => {
  const kept = significant(mode, raw);
  return mode === "recovery" ? kept.replace(GROUPS, "$1-") : kept;
};

export interface UseCodeInputOptions {
  /** Which parse to repeat. Switching it rewrites what is already typed. */
  mode: MaybeRefOrGetter<CodeMode>;
}

export interface CodeInput {
  /** The element the field is drawn on: bind with `:ref="code.inputRef"`. */
  inputRef: Ref<HTMLInputElement | null>;
  /** The text on screen, dashes included. */
  value: Ref<string>;
  /** The text to send: no dashes, no case, nothing outside the alphabet. */
  normalized: ComputedRef<string>;
  /** A whole code is entered: six digits, or sixteen symbols. */
  isComplete: ComputedRef<boolean>;
  /**
   * The last edit lost more than the field's own separators: a recovery code
   * pasted into the digit field would fill it and spend one of five attempts.
   */
  droppedSymbols: Ref<boolean>;
  /**
   * How many positions a whole code takes, dashes included. Not a maxlength:
   * the browser cuts a paste before the input event.
   */
  maxLength: ComputedRef<number>;
  /** Bind to `@input`. */
  onInput: () => void;
  /** Bind to `@keydown`. */
  onKeydown: (event: KeyboardEvent) => void;
  /** Put a code in from outside (a paste handled elsewhere, a prefill). */
  setValue: (raw: string) => void;
  /** Empty the field. */
  reset: () => void;
}

/**
 * The second-factor code field. Filtered on input, not keydown: paste,
 * autofill and soft keyboards send no key events. The caret is kept by the
 * count of significant symbols, since dashes appear as the reader types.
 */
export function useCodeInput(options: UseCodeInputOptions): CodeInput {
  const inputRef = ref<HTMLInputElement | null>(null);
  const value = ref("");
  const droppedSymbols = ref(false);

  const mode = (): CodeMode => toValue(options.mode);

  /**
   * Writes the filtered text and the caret to the element at once: a value that
   * reached the DOM on the next render would take the caret with it.
   */
  const apply = (raw: string, caret: number): void => {
    const current = mode();
    const before = significant(current, raw.slice(0, caret)).length;
    const next = formatted(current, raw);
    // Measured before the write: only text other than the separators counts as lost.
    droppedSymbols.value =
      raw.replace(SEPARATORS, "").length !== significant(current, raw).length;
    value.value = next;

    const element = inputRef.value;
    if (!element) return;
    if (element.value !== next) element.value = next;

    let position = 0;
    let seen = 0;
    while (position < next.length && seen < before) {
      if (next[position] !== "-") seen += 1;
      position += 1;
    }
    // A caret resting in front of a dash belongs after it: the dash is not a
    // position the reader can type into.
    while (position < next.length && next[position] === "-") position += 1;
    element.setSelectionRange(position, position);
  };

  const onInput = (): void => {
    const element = inputRef.value;
    if (!element) return;
    apply(element.value, element.selectionStart ?? element.value.length);
  };

  /**
   * Raises the field's input event for an edit made here, the one path the
   * screen listens on (it clears the previous refusal).
   */
  const notify = (element: HTMLInputElement): void => {
    element.dispatchEvent(new Event("input", { bubbles: true }));
  };

  /**
   * Backspace and Delete next to a dash take the symbol beyond it, or the
   * reformat would put the dash back and the key would do nothing.
   */
  const onKeydown = (event: KeyboardEvent): void => {
    if (event.key !== "Backspace" && event.key !== "Delete") return;
    const element = inputRef.value;
    if (!element) return;

    const at = element.selectionStart ?? 0;
    if (at !== element.selectionEnd) return;
    const text = element.value;

    if (event.key === "Backspace") {
      if (at < 2 || text[at - 1] !== "-") return;

      event.preventDefault();
      apply(text.slice(0, at - 2) + text.slice(at), at - 2);
      notify(element);
      return;
    }

    // A dash never ends the code; checked anyway for a value set from outside.
    if (text[at] !== "-" || at + 2 > text.length) return;

    event.preventDefault();
    apply(text.slice(0, at + 1) + text.slice(at + 2), at);
    notify(element);
  };

  const setValue = (raw: string): void => apply(raw, raw.length);

  const reset = (): void => setValue("");

  // Switched by the screen; what is in the field is rewritten under the new rules.
  watch(mode, () => setValue(value.value));

  const normalized = computed(() => significant(mode(), value.value));

  const isComplete = computed(
    () =>
      normalized.value.length ===
      (mode() === "recovery" ? RECOVERY_LENGTH : DIGIT_LENGTH),
  );

  const maxLength = computed(() =>
    mode() === "recovery"
      ? RECOVERY_LENGTH + RECOVERY_SEPARATORS
      : DIGIT_LENGTH,
  );

  return {
    inputRef,
    value,
    normalized,
    isComplete,
    droppedSymbols,
    maxLength,
    onInput,
    onKeydown,
    setValue,
    reset,
  };
}
