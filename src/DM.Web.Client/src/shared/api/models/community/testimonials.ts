import type { Served, PagingQuery } from "../common";
import type { User } from "./index";
import type { SortDirection } from "@/shared/lib/filters/types";

/**
 * Testimonials about the site: plain text, one per user, no likes.
 * @see src/DM.Web.API/Features/Community/WebsiteTestimonials/WebsiteTestimonialDtos.cs
 */

export type WebsiteTestimonialId = Served<string>;

export type WebsiteTestimonial = {
  id: Served<WebsiteTestimonialId>;
  author: Served<User>;
  /** Plain text, no BBCode. */
  text: string;
  createdUtc: Served<string>;
  modifiedUtc?: Served<string>;
};

export type CreateWebsiteTestimonialRequest = {
  /**
   * Senior moderation submits the entry on the participant's behalf, so the
   * author is named rather than taken from the session.
   */
  authorUsername: string;
  /** 1-1000 characters, plain text, positive only. */
  text: string;
};

export type UpdateWebsiteTestimonialRequest = {
  /** 1-1000 characters, plain text, positive only. */
  text?: string;
};

export type TestimonialSortBy = "created" | "author";

export type WebsiteTestimonialsQuery = PagingQuery & {
  search?: string;
  sortBy?: TestimonialSortBy;
  sortOrder?: SortDirection;
};
