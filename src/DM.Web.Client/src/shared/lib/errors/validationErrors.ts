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
 * The first thing wrong with a field, ready to show under it.
 */
export function readFieldError(
  errors: Record<string, string[]>,
  field: string,
): string | undefined {
  const codes = errors[field.toLowerCase()] ?? errors[field];
  return codes?.length ? readValidationCode(codes[0]) : undefined;
}
