import type { Tag } from "@/entities/game";

interface TagPlace {
  groupSortOrder: number;
  sortOrder: number;
}

export function byTagOrder(a: TagPlace, b: TagPlace): number {
  if (a.groupSortOrder !== b.groupSortOrder)
    return a.groupSortOrder - b.groupSortOrder;
  return a.sortOrder - b.sortOrder;
}

// DOMParser builds a document nothing runs in, unlike an element given the markup.
export function descriptionText(html: string | undefined): string | undefined {
  if (!html) return html;
  const parsed = new DOMParser().parseFromString(html, "text/html");
  for (const lineBreak of parsed.querySelectorAll("br")) {
    lineBreak.replaceWith(" ");
  }
  return parsed.body.textContent ?? "";
}

interface TagBubble extends TagPlace {
  tagId: number;
  title: string;
}

// A tag the catalog does not know shows its id, at place 99 of group 99.
export function tagBubbles(
  ids: Iterable<number>,
  tags: Record<number, Tag>,
): TagBubble[] {
  return [...ids]
    .map((tagId) => ({
      tagId,
      title: tags[tagId]?.title ?? `#${tagId}`,
      groupSortOrder: tags[tagId]?.groupSortOrder ?? 99,
      sortOrder: tags[tagId]?.sortOrder ?? 99,
    }))
    .sort(byTagOrder);
}
