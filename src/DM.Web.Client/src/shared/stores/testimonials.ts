import { defineStore } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type {
  WebsiteTestimonial,
  WebsiteTestimonialId,
  WebsiteTestimonialsQuery,
} from "@/shared/api/models/community";
import { CommunityApi } from "@/shared/api";

const CACHE_TTL = 60_000; // 60 seconds

/**
 * Website testimonials store
 * Manages website testimonials (positive reviews about the platform)
 * - Plain text only (NO BBCode)
 * - One testimonial per user
 * - NO likes support
 */
export const useTestimonialStore = defineStore("testimonials", () => {
  const testimonials = ref<ListEnvelope<WebsiteTestimonial> | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const lastFetchTime = ref(0);
  const lastQueryKey = ref("");

  function createQueryKey(query: WebsiteTestimonialsQuery): string {
    return JSON.stringify(query);
  }

  /**
   * Fetch testimonials (60s cache per query).
   * @returns true on success (or fresh cache hit), false on failure.
   * On failure previously loaded data is kept intact and `error` is set.
   */
  async function fetchTestimonials(
    query: WebsiteTestimonialsQuery,
    force = false,
  ): Promise<boolean> {
    const queryKey = createQueryKey(query);
    const now = Date.now();

    // Return cached if fresh and same query
    if (
      !force &&
      testimonials.value &&
      queryKey === lastQueryKey.value &&
      now - lastFetchTime.value < CACHE_TTL
    ) {
      error.value = null;
      return true;
    }

    loading.value = true;
    error.value = null;
    const { data, error: apiError } = await CommunityApi.getTestimonials(query);
    loading.value = false;

    if (apiError || !data) {
      // Keep stale data so consumers can still show something
      error.value = "Не удалось загрузить отзывы";
      return false;
    }

    testimonials.value = data;
    lastFetchTime.value = now;
    lastQueryKey.value = queryKey;
    return true;
  }

  async function removeTestimonial(id: WebsiteTestimonialId) {
    const { error } = await CommunityApi.removeTestimonial(id);
    if (!error && testimonials.value) {
      testimonials.value.resources = testimonials.value.resources.filter(
        (r) => r.id !== id,
      );
    }
    return { error };
  }

  async function createTestimonial(text: string) {
    const { data, error } = await CommunityApi.postTestimonial({ text });
    if (!error && data && testimonials.value) {
      testimonials.value.resources.unshift(data);
    }
    return { data, error };
  }

  return {
    testimonials,
    loading,
    error,
    fetchTestimonials,
    removeTestimonial,
    createTestimonial,
  };
});
