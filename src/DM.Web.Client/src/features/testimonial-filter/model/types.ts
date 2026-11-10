import type { SortDirection } from "@/shared/lib/filters/types";

import type { TestimonialSortBy } from "@/shared/api/models/community";

export type { TestimonialSortBy };

export interface TestimonialsFilterState {
  search: string;

  sortBy: TestimonialSortBy;

  sortOrder: SortDirection;
}

export interface TestimonialsSearchParams {
  search?: string;
  sortBy?: TestimonialSortBy;
  sortOrder?: SortDirection;
  number?: number;
  size?: number;
}

export const DEFAULT_FILTER_STATE: TestimonialsFilterState = {
  search: "",
  sortBy: "created",
  sortOrder: "desc",
};

export const SORT_OPTIONS = [
  {
    value: "created" as const,
    label: "Дата",
    hint: "По дате создания",
    defaultDirection: "desc" as const,
  },
  {
    value: "author" as const,
    label: "Автор",
    hint: "По имени автора",
    defaultDirection: "asc" as const,
  },
] as const;
