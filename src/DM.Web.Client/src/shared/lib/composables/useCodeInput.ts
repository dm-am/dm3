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
 * What the server would keep of this text: the code without its formatting.
 *
 * The client repeats the server's own parse rather than inventing a looser one,
 * so the field cannot produce an entry the server will refuse on shape alone.
 * Six digits is a device code; anything else goes to the recovery set, where
 * RecoveryCodeFactory.Normalize raises the case, reads I and L as one and O as
 * zero (the three characters a handwritten code loses to the digits beside
 * them) and drops everything outside the alphabet, dashes and spaces included.
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
   * The last edit lost more than the separators the field draws itself: a
   * letter, a punctuation mark, a symbol past the end of the code.
   *
   * It exists for the screen that checks a full code by itself. A recovery code
   * pasted into the digit field holds exactly six digits, so the field is full
   * and looks like a code from the authenticator, and the check would spend one
   * of the five attempts the server allows on an entry nobody meant to send.
   * The button stays where it is either way: what he typed is his to send.
   */
  droppedSymbols: Ref<boolean>;
  /**
   * How many positions a whole code of this kind takes on screen, dashes
   * included.
   *
   * Not written onto the field as `maxlength`: the browser applies that before
   * an input event is raised, so a recovery code pasted into the digit field
   * arrived already cut and the parse read a fragment of it. The length is
   * kept by the parse instead, which knows both kinds of code.
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
 * The second-factor code field.
 *
 * The filtering sits on `input` and not on `keydown` on purpose: a key press is
 * one of the ways text arrives and not the common one here. A code is pasted
 * from a password manager, filled by the one-time-code autofill of the
 * platform, or typed on a soft keyboard that emits no key events at all. A
 * keydown filter passes all three straight through, and the field then shows an
 * entry the server refuses.
 *
 * The caret is placed by the count of significant symbols in front of it rather
 * than by its offset in the string, because the string the reader sees is not
 * the string he typed: dashes appear between his symbols as he goes. Kept by
 * offset, an edit in the middle of a recovery code threw the caret to the end
 * after every keystroke.
 */
export function useCodeInput(options: UseCodeInputOptions): CodeInput {
  const inputRef = ref<HTMLInputElement | null>(null);
  const value = ref("");
  const droppedSymbols = ref(false);

  const mode = (): CodeMode => toValue(options.mode);

  /**
   * Write the filtered text back into the field and put the caret where the
   * reader left it, after the same number of significant symbols.
   *
   * The element is written to directly, not only through the ref: the caret has
   * to be set on the text that is on screen now, and a value that only reaches
   * the DOM on the next render would take the caret with it.
   */
  const apply = (raw: string, caret: number): void => {
    const current = mode();
    const before = significant(current, raw.slice(0, caret)).length;
    const next = formatted(current, raw);
    // Measured before the write, on what actually arrived: the separators are
    // the field's own and cost nothing, everything else the parse threw away
    // was somebody's text.
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
   * Say that the value changed, by the one road every change takes: the
   * `input` event of the field.
   *
   * The screen around this field hangs things on that event: it clears the
   * refusal the server gave the previous code, and decides whether to check the
   * new one. When the composable edits the value itself the browser sends no
   * event of its own (the key press was taken away from it), and the screen was
   * left showing "Код не подошел" under a code that had since changed. Raising
   * the same event instead of adding a second channel keeps one path for every
   * edit, whoever made it.
   */
  const notify = (element: HTMLInputElement): void => {
    element.dispatchEvent(new Event("input", { bubbles: true }));
  };

  /**
   * The two keys a dash would otherwise kill.
   *
   * Backspace with a dash in front of the caret takes the symbol BEFORE the
   * dash; Delete with a dash behind the caret takes the symbol AFTER it. Left
   * alone, either key deletes the dash, the reformat puts it straight back, and
   * the reader sees a key that does nothing.
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

    // A dash never ends the formatted code, so a dash under the caret always
    // has a symbol after it; the length is checked anyway, because the value
    // may have been put there from outside.
    if (text[at] !== "-" || at + 2 > text.length) return;

    event.preventDefault();
    apply(text.slice(0, at + 1) + text.slice(at + 2), at);
    notify(element);
  };

  const setValue = (raw: string): void => apply(raw, raw.length);

  const reset = (): void => setValue("");

  // The mode is switched by the screen (the segments above the field), and
  // whatever is still in the field is rewritten under the new rules rather than
  // thrown away; a screen that wants an empty field on the switch calls reset.
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
