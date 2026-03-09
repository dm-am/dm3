import { defineStore } from "pinia";
import { ref } from "vue";
import type { Blog } from "./types";
import blogApi from "../api/blogApi";

export const useBlogsStore = defineStore("blogs", () => {
  const activeBlogs = ref<Blog[] | null>(null);
  const popularBlogs = ref<Blog[] | null>(null);
  const subscribedBlogs = ref<Blog[] | null>(null);
  const subscribedBlogsLoading = ref(false);

  async function fetchActiveBlogs() {
    const { data } = await blogApi.getPublicBlogs({ size: 5 });
    activeBlogs.value = data?.resources ?? [];
  }

  async function fetchPopularBlogs() {
    const { data } = await blogApi.getPopularBlogs();
    popularBlogs.value = data?.resources ?? [];
  }

  async function fetchSubscribedBlogs() {
    subscribedBlogsLoading.value = true;
    try {
      const { data } = await blogApi.getSubscribedBlogs();
      subscribedBlogs.value = data?.resources ?? [];
    } finally {
      subscribedBlogsLoading.value = false;
    }
  }

  function resetSubscribedBlogs() {
    subscribedBlogs.value = null;
  }

  return {
    activeBlogs,
    popularBlogs,
    subscribedBlogs,
    subscribedBlogsLoading,
    fetchActiveBlogs,
    fetchPopularBlogs,
    fetchSubscribedBlogs,
    resetSubscribedBlogs,
  };
});
