/** The caret position just after the n-th digit of the text. */
function afterDigits(text: string, count: number): number {
  if (count === 0) return 0;
  const nth = [...text.matchAll(/\d/g)][count - 1];
  return nth?.index === undefined ? text.length : nth.index + 1;
}

/**
 * The field's text shaped (its separators put in), written back with the
 * caret kept after as many digits as stood before it.
 */
export function shapeInput(
  input: HTMLInputElement,
  shape: (raw: string) => string,
): string {
  const raw = input.value;
  const shaped = shape(raw);
  if (shaped !== raw) {
    const caret = input.selectionStart ?? raw.length;
    const digitsBefore = raw.slice(0, caret).replace(/\D/g, "").length;
    input.value = shaped;
    const at = afterDigits(shaped, digitsBefore);
    input.setSelectionRange(at, at);
  }
  return shaped;
}

/** Says a reason only when it changes, so each change is said once. */
export function reasonSayer(say: (reason: string | null) => void) {
  let said: string | null = null;
  return (next: string | null) => {
    if (next === said) return;
    said = next;
    say(next);
  };
}

/**
 * The refusal for a value past one of the field's bounds, or null within
 * them; a bound left out (undefined or empty) does not hold. The bound is
 * spelled as the field shows it.
 */
export function boundReason<T extends string | number>(
  value: T,
  min: T | undefined,
  max: T | undefined,
  spell: (bound: T) => string,
): string | null {
  if (min !== undefined && min !== "" && value < min)
    return `Не раньше ${spell(min)}`;
  if (max !== undefined && max !== "" && value > max)
    return `Не позже ${spell(max)}`;
  return null;
}
