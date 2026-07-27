/**
 * Russian month names — SSOT for every month-picking control (MonthYearPicker,
 * DatePicker calendar). Previously copy-pasted per component.
 */

// Lowercase nominative ("январь") — the mid-sentence form. Not exported:
// standalone UI labels (buttons, headers) use the capitalized set below;
// prose composes month names inline where needed.
const RU_MONTHS_FULL = [
  "январь",
  "февраль",
  "март",
  "апрель",
  "май",
  "июнь",
  "июль",
  "август",
  "сентябрь",
  "октябрь",
  "ноябрь",
  "декабрь",
] as const;

/** Capitalized nominative ("Январь") — standalone labels: calendar grid
 * headers, picker trigger buttons ("Июль 2026"). */
export const RU_MONTHS_CAPITALIZED = RU_MONTHS_FULL.map(
  (m) => m[0].toUpperCase() + m.slice(1),
) as readonly string[];

/** Three-letter abbreviations ("Янв") — dense month grids. */
export const RU_MONTHS_SHORT = [
  "Янв",
  "Фев",
  "Мар",
  "Апр",
  "Май",
  "Июн",
  "Июл",
  "Авг",
  "Сен",
  "Окт",
  "Ноя",
  "Дек",
] as const;
