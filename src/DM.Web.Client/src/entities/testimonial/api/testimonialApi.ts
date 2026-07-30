import type { ListEnvelope } from "@/shared/api/models/common";
import type {
  WebsiteTestimonial,
  WebsiteTestimonialId,
  WebsiteTestimonialsQuery,
  CreateWebsiteTestimonialRequest,
  UpdateWebsiteTestimonialRequest,
} from "@/shared/api/models/community";
import { Api } from "@/shared/api";

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
    return Api.post<WebsiteTestimonial>("testimonials", request);
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
    return Api.patch<WebsiteTestimonial>(`testimonials/${id}`, request);
  }

  /**
   * Delete a website testimonial
   * @param id Testimonial identifier
   */
  public deleteTestimonial(id: WebsiteTestimonialId) {
    return Api.delete(`testimonials/${id}`);
  }
})();
