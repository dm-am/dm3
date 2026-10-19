import type { Column } from "@/shared/ui/DataTable";

/**
 * The columns of a table of blogs, at their shares of the whole table:
 * "Название" takes what they leave.
 */
export const BLOG_COLUMNS: Column[] = [
  { key: "title", label: "Название", align: "left" },
  { key: "authors", label: "Ведущие", width: "23.7%", align: "left" },
  { key: "status", label: "Статус блога", width: "23.7%", align: "left" },
  { key: "readers", label: "Читатели", width: "10.6%", align: "center" },
];
