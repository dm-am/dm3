import { ref } from "vue";
import type { BlogRef } from "./types";
import blogApi from "../api/blogApi";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { SIDEBAR_BLOG_ROWS } from "./sidebarRows";

/** The plain lists of blogs the sidebars and the home page read. */
export function createBlogLists() {
  // The rows "Активные блоги" asks for: its own, or more when a block above leaves some out.
  const activeTake = ref<number>(SIDEBAR_BLOG_ROWS);
  const active = useApiList<BlogRef>(() =>
    blogApi.getActiveBlogs(activeTake.value),
  );
  const popular = useApiList<BlogRef>(() => blogApi.getPopularBlogs());

  // Reset on logout.
  const participating = useApiList<BlogRef>(() =>
    blogApi.getParticipatingBlogs(),
  );

  const lists = {
    // Data
    activeBlogs: active.data,
    popularBlogs: popular.data,
    participatingBlogs: participating.data,

    activeBlogsLoading: active.loading,
    popularBlogsLoading: popular.loading,
    participatingBlogsLoading: participating.loading,

    // Error states
    activeBlogsError: active.error,
    popularBlogsError: popular.error,
    participatingBlogsError: participating.error,

    // Fetch functions
    fetchActiveBlogs: active.fetch,
    fetchPopularBlogs: popular.fetch,
    fetchParticipatingBlogs: participating.fetch,

    // The rows asked for, and a read asking for more of them.
    activeBlogsTake: activeTake,
    widenActiveBlogs: (take: number) => {
      activeTake.value = take;
      return active.fetch(true);
    },

    // Reset functions (for logout)
    resetParticipatingBlogs: participating.reset,
  };

  // Refetches rather than blanks: the sidebar mounts once per session.
  const invalidateLists = () =>
    Promise.all([
      active.invalidate(),
      popular.invalidate(),
      participating.invalidate(),
    ]);

  return { lists, invalidateLists };
}
