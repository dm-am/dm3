export type TestimonialSortBy = "created" | "author";

export interface TestimonialsFilterState {
  search: string;

  sortBy: TestimonialSortBy;

  sortOrder: "asc" | "desc";
}

export interface TestimonialsSearchParams {
  search?: string;
  sortBy?: TestimonialSortBy;
  sortOrder?: "asc" | "desc";
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
