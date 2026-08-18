import type { Id, Served, PagingQuery } from "../common";
import type { User } from "./index";

/**
 * Website testimonial types
 * @module shared/api/models/community/testimonials
 *
 * WebsiteTestimonials are positive reviews about the platform.
 * - Plain text only (NO BBCode support)
 * - One testimonial per user
 * - NO likes support
 *
 * @see src/DM.Web.API/Features/Community/WebsiteTestimonials/WebsiteTestimonialDtos.cs
 */

export type WebsiteTestimonialId = Id<string>;

/**
 * Website testimonial DTO
 * Positive review about the website/platform
 */
export type WebsiteTestimonial = {
  /** Testimonial unique identifier */
  id: Served<WebsiteTestimonialId>;
  /** Testimonial author details */
  author: Served<User>;
  /** Testimonial content (plain text, NO BBCode) */
  text: string;
  /** Creation timestamp (UTC) */
  createdUtc: Served<string>;
  /** Last modification timestamp (UTC) */
  modifiedUtc?: Served<string>;
};

/**
 * Request to create a new website testimonial
 */
export type CreateWebsiteTestimonialRequest = {
  /**
   * Username of the participant the testimonial is signed by. Senior
   * moderation submits the entry on their behalf, so the author is named
   * rather than taken from the session.
   */
  authorUsername: string;
  /** Testimonial text content (10-1000 characters, plain text, positive only) */
  text: string;
};

/**
 * Request to update an existing website testimonial
 */
export type UpdateWebsiteTestimonialRequest = {
  /** Updated testimonial text (10-1000 characters, plain text, positive only) */
  text?: string;
};

/**
 * Query parameters for fetching website testimonials
 */
export type WebsiteTestimonialsQuery = PagingQuery & {
  search?: string;
  sortBy?: "created" | "author";
  sortOrder?: "asc" | "desc";
};
