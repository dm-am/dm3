/**
 * The server's validation vocabulary, in Russian.
 *
 * Every 400 the API answers carries codes, not sentences: `errors` maps a field
 * name to entries like `Short` or `RequiresDigit`, and turning those into
 * something a person can read is the client's job. The full set is
 * DM.Domain.Core.Exceptions.ValidationError.
 *
 * It lives here rather than inside a component because every form needs it and
 * because a partial copy is worse than none: the previous one sat in
 * FormField.vue and covered six of the thirteen codes, so a password that
 * failed the digit rule showed the reader the literal text "RequiresDigit".
 */
export const VALIDATION_MESSAGES: Record<string, string> = {
  Empty: "Обязательное поле",
  Short: "Слишком короткое значение",
  Long: "Слишком длинное значение",
  Taken: "Уже занято",
  Invalid: "Некорректное значение",
  RequiresUppercase: "Нужна хотя бы одна заглавная буква",
  RequiresLowercase: "Нужна хотя бы одна строчная буква",
  RequiresDigit: "Нужна хотя бы одна цифра",
  RequiresSpecialCharacter: "Нужен хотя бы один специальный символ",
  TooMany: "Слишком много значений",
  MustBeFuture: "Дата должна быть в будущем",
  MustBePositive: "Значение должно быть больше нуля",
  Unchanged: "Значение не изменилось",
};

/**
 * What a code says after the name of the field it failed, in a line that names
 * the field: "Система: не заполнено".
 *
 * A plate is not under a field. "Обязательное поле" there used to stand in for
 * the whole answer to a save, and the reader was left to find which of the
 * fields on the screen it meant, and what had been saved at all. The call site
 * knows the fields it sent by name (FieldLabels), so the plate can say it.
 *
 * The same codes as VALIDATION_MESSAGES, lowercase because they follow a colon,
 * and worded to follow any label: "Empty" is "не заполнено" and not
 * "обязательное поле", which would name the field twice, and the rules that
 * speak of a value keep the noun, so they agree with a label of any gender.
 * The rules of a password drop "хотя бы один": after a label the line is
 * short of room, and "Пароль: нужна цифра" already says one is enough.
 */
export const FIELD_REASONS: Record<string, string> = {
  Empty: "не заполнено",
  Short: "слишком короткое значение",
  Long: "слишком длинное значение",
  Taken: "уже занято",
  Invalid: "некорректное значение",
  RequiresUppercase: "нужна заглавная буква",
  RequiresLowercase: "нужна строчная буква",
  RequiresDigit: "нужна цифра",
  RequiresSpecialCharacter: "нужен специальный символ",
  TooMany: "слишком много значений",
  MustBeFuture: "дата должна быть в будущем",
  MustBePositive: "значение должно быть больше нуля",
  Unchanged: "значение не изменилось",
};

/**
 * The label a screen shows over a field, by the name the server gives that
 * field in a 400: `{ systemName: "Система" }`. Matched without regard to case,
 * because the API spells a field the way its command spells the property
 * ("SystemName"), not the way the request body did.
 */
export type FieldLabels = Record<string, string>;

/** One part of the line: fields that failed alike, or a sentence of its own. */
type Part = { reason: string; labels: string[] } | { sentence: string };

/**
 * The fields a 400 refused, read as one line.
 *
 * A field the caller put a label on is named, and fields that failed the same
 * rule share it: "Система, Сеттинг: не заполнено". A field without a label, or
 * one refused with a sentence of the server's own rather than a code, is read
 * the way it always was. Two different sentences are two parts of the line,
 * and repeating one sentence reads like two problems, so it is said once.
 */
export function readFieldErrors(
  errors: Record<string, string[] | undefined>,
  labels: FieldLabels = {},
): string {
  const named = new Map(
    Object.entries(labels).map(([field, label]) => [
      field.toLowerCase(),
      label,
    ]),
  );
  const parts: Part[] = [];
  for (const [field, codes] of Object.entries(errors)) {
    const code = codes?.[0];
    if (!code) continue;
    const label = named.get(field.toLowerCase());
    const reason = FIELD_REASONS[code];
    if (label && reason) {
      const group = parts.find(
        (part): part is { reason: string; labels: string[] } =>
          "reason" in part && part.reason === reason,
      );
      if (!group) parts.push({ reason, labels: [label] });
      else if (!group.labels.includes(label)) group.labels.push(label);
      continue;
    }
    const sentence = readValidationCode(code);
    if (!parts.some((part) => "sentence" in part && part.sentence === sentence))
      parts.push({ sentence });
  }
  return parts
    .map((part) =>
      "sentence" in part
        ? part.sentence
        : `${part.labels.join(", ")}: ${part.reason}`,
    )
    .join(". ");
}

/**
 * Reads a validation entry the way a person would.
 *
 * An unknown code is returned as it stands: a new code on the server is a
 * missing line here, and showing it is what makes that visible instead of
 * replacing it with a shrug.
 */
export function readValidationCode(code: string): string {
  return VALIDATION_MESSAGES[code] ?? code;
}

/**
 * What a code means under one particular field, where the general word is too
 * general to act on.
 *
 * "Некорректное значение" is right for a field the reader can be wrong about in
 * a dozen ways and wrong for an address, which has exactly one thing wrong with
 * it. The forms already say "Неверный формат почты" when they check the address
 * themselves, so the same address refused by the server said something else —
 * two sentences about one mistake, and the vaguer one arriving second.
 *
 * Keyed by the lowercase field name, because that is how the API spells them.
 */
const FIELD_MESSAGES: Record<string, Record<string, string>> = {
  email: { Invalid: "Неверный формат почты" },
};

/**
 * The first thing wrong with a field, ready to show under it.
 */
export function readFieldError(
  errors: Record<string, string[]>,
  field: string,
): string | undefined {
  const name = field.toLowerCase();
  const codes = errors[name] ?? errors[field];
  if (!codes?.length) return undefined;

  return FIELD_MESSAGES[name]?.[codes[0]] ?? readValidationCode(codes[0]);
}
