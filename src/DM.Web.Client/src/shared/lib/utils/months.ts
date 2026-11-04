// Russian month names for every month-picking control, from the date library's
// Russian locale. A month formatted alone comes in the nominative ("январь"),
// the standalone form; inside a date the library declines it on its own.
import dayjs from "dayjs";
import "dayjs/locale/ru";

const capitalized = (word: string) => word[0].toUpperCase() + word.slice(1);

/** The twelve months, January first, by the given format token. */
const monthsAs = (token: "MMMM" | "MMM"): readonly string[] =>
  Array.from({ length: 12 }, (_, month) =>
    capitalized(
      dayjs(new Date(2000, month, 1))
        .locale("ru")
        .format(token),
    ),
  );

/** Capitalized nominative ("Январь"): calendar grid headers, picker trigger buttons ("Июль 2026"). */
export const RU_MONTHS_CAPITALIZED = monthsAs("MMMM");

/** The library's abbreviations, capitalized ("Янв.", "Март"): dense month grids. */
export const RU_MONTHS_SHORT = monthsAs("MMM");
