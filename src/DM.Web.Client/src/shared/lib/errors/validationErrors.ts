/**
 * The server's validation codes (DM.Domain.Core.Exceptions.ValidationError) in
 * Russian; every form reads them from here.
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
 * A code after the name of the field it failed: "Система: обязательное поле".
 * The sentence of VALIDATION_MESSAGES, lowercase after the colon; an unknown
 * code has none.
 */
export function fieldReason(code: string): string | undefined {
  const sentence = VALIDATION_MESSAGES[code];
  return sentence && sentence[0].toLowerCase() + sentence.slice(1);
}

/** Said of an address of mail in a shape no address has, by the form and by the server alike. */
export const EMAIL_INVALID = "Неверный формат почты";

/**
 * The label a screen shows over a field, by the name the server gives that
 * field in a 400: `{ systemName: "Система" }`. Matched without regard to case:
 * the API spells the property of its command ("SystemName").
 */
export type FieldLabels = Record<string, string>;

/** One part of the line: fields that failed alike, or a sentence of its own. */
type Part = { reason: string; labels: string[] } | { sentence: string };

/**
 * The fields a 400 refused, as one line: labelled fields that failed alike
 * share it ("Система, Сеттинг: обязательное поле"); a sentence is said once.
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
    const reason = fieldReason(code);
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

/** An unknown code is returned as it stands, so a new server code shows. */
function readValidationCode(code: string): string {
  return VALIDATION_MESSAGES[code] ?? code;
}

/** The first thing wrong with a field, ready to show under it. */
export function readFieldError(
  errors: Record<string, string[]>,
  field: string,
): string | undefined {
  const codes = errors[field.toLowerCase()] ?? errors[field];
  if (!codes?.length) return undefined;

  return readValidationCode(codes[0]);
}
