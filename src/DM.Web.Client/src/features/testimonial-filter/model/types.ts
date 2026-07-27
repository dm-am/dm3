/**
 * Sort field options for testimonials
 */
export type TestimonialSortBy = "created" | "author";

/**
 * Filter state for testimonials
 */
export interface TestimonialsFilterState {
  /** Text search query (testimonial text, author username) */
  search: string;

  /** Sort field */
  sortBy: TestimonialSortBy;

  /** Sort direction */
  sortOrder: "asc" | "desc";
}

/**
 * API search parameters for testimonials
 */
export interface TestimonialsSearchParams {
  search?: string;
  sortBy?: TestimonialSortBy;
  sortOrder?: "asc" | "desc";
  number?: number;
  size?: number;
}

/**
 * Default filter state
 */
export const DEFAULT_FILTER_STATE: TestimonialsFilterState = {
  search: "",
  sortBy: "created",
  sortOrder: "desc",
};

/**
 * Sort options
 */
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
