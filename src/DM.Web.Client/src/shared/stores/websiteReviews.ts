import { defineStore } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { WebsiteReview, WebsiteReviewId } from "@/shared/api/models/community";
import { CommunityApi } from "@/shared/api";

export const useWebsiteReviewStore = defineStore("websiteReviews", () => {
  const websiteReviews = ref<ListEnvelope<WebsiteReview> | null>(null);

  async function fetchWebsiteReviews(number: number) {
    const { data } = await CommunityApi.getWebsiteReviews({ number }, true);
    websiteReviews.value = data;
  }

  async function removeWebsiteReview(id: WebsiteReviewId) {
    const { error } = await CommunityApi.removeWebsiteReview(id);
    if (!error && websiteReviews.value) {
      websiteReviews.value.resources = websiteReviews.value.resources.filter(
        (r) => r.id !== id,
      );
    }
  }

  async function createWebsiteReview(text: string, authorUsername: string) {
    const { data, error } = await CommunityApi.postWebsiteReview({
      text,
      authorUsername,
    });
    if (!error && data && websiteReviews.value) {
      websiteReviews.value.resources.unshift(data);
    }
    return { data, error };
  }

  return {
    websiteReviews,
    fetchWebsiteReviews,
    removeWebsiteReview,
    createWebsiteReview,
  };
});
