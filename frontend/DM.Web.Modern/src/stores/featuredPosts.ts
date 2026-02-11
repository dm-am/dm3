import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type { FeaturedPost, FeaturedPostsEnvelope } from "@/api/models/game";
import gameApi from "@/api/requests/gameApi";

export const useFeaturedPostsStore = defineStore("featuredPosts", () => {
  const data = ref<FeaturedPostsEnvelope | null>(null);
  const loading = ref(false);
  const loaded = ref(false);

  const bestOfWeek = computed<FeaturedPost | null>(
    () => data.value?.bestOfWeek ?? null
  );
  const lastWithPlus = computed<FeaturedPost | null>(
    () => data.value?.lastWithPlus ?? null
  );

  async function fetch() {
    if (loading.value) return;
    loading.value = true;
    try {
      const { data: response } = await gameApi.getFeaturedPosts();
      data.value = response ?? null;
    } finally {
      loading.value = false;
      loaded.value = true;
    }
  }

  return {
    data,
    loading,
    loaded,
    bestOfWeek,
    lastWithPlus,
    fetch,
  };
});
