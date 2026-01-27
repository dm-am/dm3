import { defineStore } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/api/models/common";
import type { WebsiteReview, WebsiteReviewId } from "@/api/models/community";
import communityApi from "@/api/requests/communityApi";

export const useWebsiteReviewStore = defineStore("websiteReviews", () => {
  const websiteReviews = ref<ListEnvelope<WebsiteReview> | null>(null);

  async function fetchWebsiteReviews(number: number) {
    const { data } = await communityApi.getWebsiteReviews({ number }, true);
    websiteReviews.value = data;
  }

  async function removeWebsiteReview(id: WebsiteReviewId) {
    const { error } = await communityApi.removeWebsiteReview(id);
    if (!error && websiteReviews.value) {
      websiteReviews.value.resources = websiteReviews.value.resources.filter(
        (r) => r.id !== id,
      );
    }
  }

  async function createWebsiteReview(text: string, authorLogin: string) {
    const { data, error } = await communityApi.postWebsiteReview({
      text,
      authorLogin,
    });
    if (!error && data && websiteReviews.value) {
      websiteReviews.value.resources.unshift(data.resource);
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
