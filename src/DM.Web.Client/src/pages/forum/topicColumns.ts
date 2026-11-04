import type { Column } from "@/shared/ui/DataTable";
import type { Topic } from "@/entities/forum";

/** A row of the listing: the topic, and whether it stands as pinned. */
export type DisplayTopic = Topic & { isPinned: boolean };

/**
 * The columns of the board listing. The likes column always stands, the
 * target of the sort by likes. Every column but "Топик" has a px width for its
 * longest real content; the table lays out "auto", so where the room is short
 * the px columns give up what they can and their words take a second line.
 */
export const TOPIC_COLUMNS: Column[] = [
  {
    key: "title",
    label: "Топик",
    align: "left",
  },
  // Cells are content-box: the px are the content width, the padding sits on top.
  { key: "author", label: "Автор", width: "180px", align: "left" },
  { key: "comments", label: "Комментарии", width: "100px", align: "center" },
  { key: "likes", label: "Нравится", width: "68px", align: "center" },
  // The date columns fit their bold labels on one line; where the table is
  // narrower the date goes to two lines, over the time.
  {
    key: "created",
    label: "Дата создания",
    width: "134px",
    align: "center",
  },
  {
    key: "lastActivity",
    label: "Последняя активность",
    width: "160px",
    align: "center",
  },
];
