import type { Envelope, ListEnvelope } from "@/shared/api/models/common";
import type {
  WebsiteTestimonial,
  WebsiteTestimonialId,
  WebsiteTestimonialsQuery,
  CreateWebsiteTestimonialRequest,
  UpdateWebsiteTestimonialRequest,
} from "@/shared/api/models/community";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";
import { pageSkip } from "@/shared/api/pageSkip";

/** Testimonials about the website itself (not endorsements about a user). */
export default new (class TestimonialApi {
  public getTestimonials(
    q: WebsiteTestimonialsQuery & { number?: number; size?: number },
  ) {
    const queryParams: Record<string, string | number | undefined> = {
      search: q.search,
      sortBy: q.sortBy,
      sortOrder: q.sortOrder,
    };

    const pageSize = q.size ?? q.take ?? 10;
    queryParams.take = pageSize;

    queryParams.skip = pageSkip(q.number ?? 1, pageSize);

    return Api.get<ListEnvelope<WebsiteTestimonial>>(
      "testimonials",
      queryParams,
    );
  }

  public createTestimonial(request: CreateWebsiteTestimonialRequest) {
    return Api.post<Envelope<WebsiteTestimonial>>("testimonials", request);
  }

  public updateTestimonial(
    id: WebsiteTestimonialId,
    request: UpdateWebsiteTestimonialRequest,
  ) {
    return Api.patch<Envelope<WebsiteTestimonial>>(
      `testimonials/${id}`,
      request,
    );
  }

  public deleteTestimonial(id: WebsiteTestimonialId, options?: RequestOptions) {
    return Api.delete(`testimonials/${id}`, undefined, options);
  }
})();
