import { defineStore } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type {
  WebsiteTestimonial,
  WebsiteTestimonialId,
  WebsiteTestimonialsQuery,
} from "@/shared/api/models/community";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { unwrapResource } from "@/shared/api";
import { testimonialApi } from "../api";
import type { RequestOptions } from "@/shared/api/interceptor";

// Plain text, one per user, no likes.
export const useTestimonialStore = defineStore("testimonials", () => {
  const testimonials = ref<ListEnvelope<WebsiteTestimonial> | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const cache = createKeyedCache<ListEnvelope<WebsiteTestimonial>>({
    ttlMs: 60_000,
  });

  /** False on a failure, which keeps the data already loaded. */
  async function fetchTestimonials(
    query: WebsiteTestimonialsQuery,
    force = false,
  ): Promise<boolean> {
    const key = stableCacheKey(query);

    if (!force) {
      const cached = cache.get(key);
      if (cached) {
        testimonials.value = cached;
        error.value = null;
        return true;
      }
    }

    loading.value = true;
    error.value = null;
    const { data, error: apiError } =
      await testimonialApi.getTestimonials(query);
    loading.value = false;

    if (apiError || !data) {
      error.value = "Не удалось загрузить отзывы";
      return false;
    }

    testimonials.value = data;
    cache.set(key, data);
    return true;
  }

  async function removeTestimonial(
    id: WebsiteTestimonialId,
    options?: RequestOptions,
  ) {
    const { error } = await testimonialApi.deleteTestimonial(id, options);
    if (!error && testimonials.value) {
      testimonials.value.resources = testimonials.value.resources.filter(
        (r) => r.id !== id,
      );
      // Every other cached page still holds the removed one.
      cache.clear();
    }
    return { error };
  }

  // The author is named: senior moderation always posts one on someone else's behalf.
  async function createTestimonial(authorUsername: string, text: string) {
    const { data, error } = await testimonialApi.createTestimonial({
      authorUsername,
      text,
    });
    const created = unwrapResource<WebsiteTestimonial>(data);
    if (!error && created && testimonials.value) {
      testimonials.value.resources.unshift(created);
      cache.clear();
    }
    return { data: created, error };
  }

  // With the server's answer, so the card shows the modification stamp it recorded.
  async function updateTestimonial(id: WebsiteTestimonialId, text: string) {
    const { data, error } = await testimonialApi.updateTestimonial(id, {
      text,
    });
    const updated = unwrapResource<WebsiteTestimonial>(data);
    if (!error && updated && testimonials.value) {
      const list = testimonials.value.resources;
      const at = list.findIndex((r) => r.id === id);
      if (at >= 0) list[at] = updated;
      cache.clear();
    }
    return { data: updated, error };
  }

  return {
    testimonials,
    loading,
    error,
    fetchTestimonials,
    removeTestimonial,
    createTestimonial,
    updateTestimonial,
  };
});
