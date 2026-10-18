import type { Envelope, ListEnvelope } from "@/shared/api/models/common";
import type {
  WebsiteTestimonial,
  WebsiteTestimonialId,
  WebsiteTestimonialsQuery,
  CreateWebsiteTestimonialRequest,
  UpdateWebsiteTestimonialRequest,
} from "@/shared/api/models/community";
import { Api } from "@/shared/api";

/**
 * The refusal a caller takes over because its own screen carries it: the
 * confirmation before a delete prints the refusal in itself, and the response
 * interceptor has to stay quiet or the same sentence stands in two places.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

/** Testimonials about the website itself (not endorsements about a user). */
export default new (class TestimonialApi {
  /**
   * Get list of website testimonials
   * @param q Query parameters with paging
   * @returns List of website testimonials
   */
  public getTestimonials(
    q: WebsiteTestimonialsQuery & { number?: number; size?: number },
  ) {
    // Convert number/size to skip/take for API
    const queryParams: Record<string, string | number | undefined> = {
      search: q.search,
      sortBy: q.sortBy,
      sortOrder: q.sortOrder,
    };

    const pageSize = q.size ?? q.take ?? 10;
    queryParams.take = pageSize;

    // Convert page number to skip (number is 1-indexed page)
    const pageNumber = q.number ?? 1;
    if (pageNumber > 1) {
      queryParams.skip = (pageNumber - 1) * pageSize;
    }

    return Api.get<ListEnvelope<WebsiteTestimonial>>(
      "testimonials",
      queryParams,
    );
  }

  /**
   * Create a new website testimonial
   * @param request Testimonial creation request (text only, 10-1000 chars)
   * @returns Created testimonial
   */
  public createTestimonial(request: CreateWebsiteTestimonialRequest) {
    return Api.post<Envelope<WebsiteTestimonial>>("testimonials", request);
  }

  /**
   * Update an existing website testimonial
   * @param id Testimonial identifier
   * @param request Update request (text only)
   * @returns Updated testimonial
   */
  public updateTestimonial(
    id: WebsiteTestimonialId,
    request: UpdateWebsiteTestimonialRequest,
  ) {
    return Api.patch<Envelope<WebsiteTestimonial>>(
      `testimonials/${id}`,
      request,
    );
  }

  /**
   * Delete a website testimonial
   * @param id Testimonial identifier
   * @param options Set by the confirmation, which prints a refusal in itself
   */
  public deleteTestimonial(id: WebsiteTestimonialId, options?: RefusalFlags) {
    return Api.delete(`testimonials/${id}`, undefined, options);
  }
})();
