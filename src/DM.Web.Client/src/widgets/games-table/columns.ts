import type { Column } from "@/shared/ui/DataTable";

/**
 * The columns of a table of games, at their shares of the whole table:
 * "Название" takes what they leave.
 */
export const GAME_COLUMNS = {
  title: { key: "title", label: "Название", align: "left" },
  master: { key: "master", label: "Ведущие", width: "17.4%", align: "left" },
  tags: { key: "tags", label: "Теги", width: "19.2%", align: "left" },
  status: {
    key: "status",
    label: "Статус игры",
    width: "15.9%",
    align: "left",
  },
  reviews: {
    key: "reviews",
    label: "Рецензии",
    width: "8.5%",
    align: "center",
  },
  readers: {
    key: "readers",
    label: "Читатели",
    width: "7.9%",
    align: "center",
  },
} as const satisfies Record<string, Column>;

/** The shares of the same table without the tags, as the profile's "Ведущий" draws it. */
export const GAME_COLUMN_WIDTHS_WITHOUT_TAGS = {
  master: "22.2%",
  status: "20.7%",
  reviews: "8.6%",
  readers: "8%",
} as const;
