import { defineStore } from "pinia";
import { ref } from "vue";
import type { Blog } from "@/api/models/blog";
import blogApi from "@/api/requests/blogApi";

export const useBlogsStore = defineStore("blogs", () => {
  const activeBlogs = ref<Blog[] | null>(null);
  const popularBlogs = ref<Blog[] | null>(null);

  async function fetchActiveBlogs() {
    const { data } = await blogApi.getPublicBlogs({ size: 5 });
    activeBlogs.value = data?.resources ?? [];
  }

  async function fetchPopularBlogs() {
    const { data } = await blogApi.getPopularBlogs();
    popularBlogs.value = data?.resources ?? [];
  }

  return {
    activeBlogs,
    popularBlogs,
    fetchActiveBlogs,
    fetchPopularBlogs,
  };
});
