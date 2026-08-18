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

  /**
   * The shared cache, not a third hand-written one.
   *
   * What stood here was a single slot plus a timestamp, keyed by
   * `JSON.stringify(query)` — which is key order, so the same search built by
   * two call sites in two field orders missed each other, and the one slot
   * meant paging back a page always went to the network. Both are properties
   * of the bookkeeping, and the bookkeeping is `shared/lib/utils/keyedCache`:
   * `stableCacheKey` sorts the fields and drops the empty ones, and the cache
   * keeps a page per key.
   */
  const cache = createKeyedCache<ListEnvelope<WebsiteTestimonial>>({
    ttlMs: 60_000,
  });

  /**
   * Fetch testimonials (60s cache per query).
   * @returns true on success (or fresh cache hit), false on failure.
   * On failure previously loaded data is kept intact and `error` is set.
   */
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
      // Keep stale data so consumers can still show something
      error.value = "Не удалось загрузить отзывы";
      return false;
    }

    testimonials.value = data;
    cache.set(key, data);
    return true;
  }

  async function removeTestimonial(id: WebsiteTestimonialId) {
    const { error } = await testimonialApi.deleteTestimonial(id);
    if (!error && testimonials.value) {
      testimonials.value.resources = testimonials.value.resources.filter(
        (r) => r.id !== id,
      );
      // Every other page still holds the list as it was a moment ago. With one
      // slot this could not happen; with a page per key it can, and a reader
      // paging back would meet the testimonial they just removed.
      cache.clear();
    }
    return { error };
  }

  /**
   * Post a testimonial signed by `authorUsername`.
   *
   * The author is named rather than taken from the session: only senior
   * moderation may create one and it is always posted on someone else's
   * behalf (WebsiteTestimonialIntentionResolver.Create).
   */
  async function createTestimonial(authorUsername: string, text: string) {
    const { data, error } = await testimonialApi.createTestimonial({
      authorUsername,
      text,
    });
    // The endpoint answers with an Envelope. Unshifting the envelope itself
    // put a resource-shaped nothing at the head of the list: no author, no
    // text, and the card rendering it had neither to draw.
    const created = unwrapResource<WebsiteTestimonial>(data);
    if (!error && created && testimonials.value) {
      testimonials.value.resources.unshift(created);
      cache.clear();
    }
    return { data: created, error };
  }

  /**
   * Rewrite the text of one testimonial in place.
   *
   * The row on screen is replaced with what the server answered rather than
   * with what was typed, so the modification stamp the card shows is the one
   * the server recorded. Every cached page is dropped for the same reason
   * removal drops them: another page may hold this entry as it was.
   */
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
