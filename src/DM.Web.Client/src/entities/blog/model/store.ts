import { defineStore } from "pinia";
import { computed, ref } from "vue";
import type {
  Comment,
  ListEnvelope,
  Paging,
  User,
} from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
import type {
  Blog,
  BlogPremoderationTransition,
  BlogRef,
  BlogStatusTransition,
  BlogUser,
  CreateRubricInput,
  Publication,
} from "./types";
import blogApi from "../api/blogApi";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { Api, unwrapResource } from "@/shared/api";
import { useAuthStore } from "@/shared/stores";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import type { GeneralError } from "@/shared/api/models/common";
import { requestNotSent } from "@/shared/lib/errors";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";

/**
 * Search parameters for blogs query (frontend model)
 */
export interface BlogsSearchParams {
  search?: string;
  status?: string;
  /** Hosts filter - author OR assistant (OR logic) */
  hostUsernames?: string[];
  createdFromUtc?: string;
  createdToUtc?: string;
  activatedFromUtc?: string;
  activatedToUtc?: string;
  closedFromUtc?: string;
  closedToUtc?: string;
  sortBy?: string;
  sortOrder?: string;
  number?: number;
  size?: number;
}

const searchCache = createKeyedCache<ListEnvelope<Blog>>({ ttlMs: 30_000 });

export const useBlogsStore = defineStore("blogs", () => {
  // Sidebar lists with caching (60s TTL by default) - use lightweight BlogRef
  const active = useApiList<BlogRef>(() => blogApi.getActiveBlogs());
  const popular = useApiList<BlogRef>(() => blogApi.getPopularBlogs());

  // User-specific lists (still cached, but reset on logout) - use lightweight BlogRef
  // participating = blogs where user is author, assistant, or subscriber
  const participating = useApiList<BlogRef>(() =>
    blogApi.getParticipatingBlogs(),
  );

  // Search state for blogs page
  const searchResult = ref<ListEnvelope<Blog> | null>(null);
  const searchLoading = ref(false);
  const searchError = ref<string | null>(null);
  const lastSearchParams = ref<BlogsSearchParams | null>(null);

  // Request guard to discard stale out-of-order responses
  const requestGuard = createRequestGuard();

  /**
   * Search blogs with caching (stale-while-revalidate)
   */
  async function searchBlogs(params: BlogsSearchParams): Promise<void> {
    const requestId = requestGuard.next();

    // Reset error before the cache lookup so a stale error never survives
    // a cache-hit navigation.
    searchError.value = null;

    lastSearchParams.value = params;
    const cacheKey = stableCacheKey(params);
    const fresh = searchCache.get(cacheKey);

    if (fresh) {
      searchResult.value = fresh;
      searchLoading.value = false;
      return;
    }

    // Show cached data while revalidating (stale-while-revalidate)
    const stale = searchCache.getStale(cacheKey);
    if (stale) {
      searchResult.value = stale;
    }

    searchLoading.value = true;

    // Map frontend params to backend API params
    const pageSize = params.size || 20;
    const pageNumber = params.number || 1;
    const apiParams: Record<
      string,
      string | number | boolean | string[] | number[] | undefined
    > = {
      take: pageSize,
    };

    // Convert page number to skip (number is 1-indexed page)
    if (pageNumber > 1) {
      apiParams.skip = (pageNumber - 1) * pageSize;
    }

    // Search
    if (params.search) {
      apiParams.search = params.search;
    }

    // Status
    if (params.status) {
      apiParams.status = params.status;
    }

    // Hosts (author OR assistant)
    if (params.hostUsernames && params.hostUsernames.length > 0) {
      apiParams.hostUsernames = params.hostUsernames;
    }

    // Date ranges
    if (params.createdFromUtc) {
      apiParams.createdFromUtc = params.createdFromUtc;
    }
    if (params.createdToUtc) {
      apiParams.createdToUtc = params.createdToUtc;
    }
    if (params.activatedFromUtc) {
      apiParams.activatedFromUtc = params.activatedFromUtc;
    }
    if (params.activatedToUtc) {
      apiParams.activatedToUtc = params.activatedToUtc;
    }
    if (params.closedFromUtc) {
      apiParams.closedFromUtc = params.closedFromUtc;
    }
    if (params.closedToUtc) {
      apiParams.closedToUtc = params.closedToUtc;
    }

    // Sort
    if (params.sortBy) {
      apiParams.sortBy = params.sortBy;
    }
    if (params.sortOrder) {
      apiParams.sortOrder = params.sortOrder;
    }

    try {
      const { data, error } = await Api.get<ListEnvelope<Blog>>(
        "blogs",
        apiParams,
      );

      // Ignore stale responses
      if (!requestGuard.isCurrent(requestId)) {
        return;
      }

      if (error) {
        searchError.value = "Ошибка загрузки данных";
        return;
      }

      if (data) {
        searchResult.value = data;
        searchCache.set(cacheKey, data);
      }
    } catch {
      if (requestGuard.isCurrent(requestId)) {
        searchError.value = "Неожиданная ошибка";
      }
    } finally {
      // Only set loading false if this is the current request
      if (requestGuard.isCurrent(requestId)) {
        searchLoading.value = false;
      }
    }
  }

  /**
   * Clear search cache
   */
  function clearSearchCache(): void {
    searchCache.clear();
  }

  /**
   * Prefetch a page in background (for next page optimization)
   */
  async function prefetchPage(page: number): Promise<void> {
    if (!lastSearchParams.value) return;

    const params = { ...lastSearchParams.value, number: page };
    const cacheKey = stableCacheKey(params);

    // Skip only while the entry is fresh: replacing a stale one is the point of
    // a prefetch.
    if (searchCache.get(cacheKey)) return;

    // Map params to API params (same as searchBlogs)
    const pageSize = params.size || 20;
    const apiParams: Record<
      string,
      string | number | boolean | string[] | number[] | undefined
    > = {
      take: pageSize,
    };

    // Convert page number to skip (number is 1-indexed page)
    if (page > 1) {
      apiParams.skip = (page - 1) * pageSize;
    }
    if (params.search) apiParams.search = params.search;
    if (params.status) apiParams.status = params.status;
    if (params.hostUsernames && params.hostUsernames.length > 0) {
      apiParams.hostUsernames = params.hostUsernames;
    }
    if (params.createdFromUtc) apiParams.createdFromUtc = params.createdFromUtc;
    if (params.createdToUtc) apiParams.createdToUtc = params.createdToUtc;
    if (params.activatedFromUtc)
      apiParams.activatedFromUtc = params.activatedFromUtc;
    if (params.activatedToUtc) apiParams.activatedToUtc = params.activatedToUtc;
    if (params.closedFromUtc) apiParams.closedFromUtc = params.closedFromUtc;
    if (params.closedToUtc) apiParams.closedToUtc = params.closedToUtc;
    if (params.sortBy) apiParams.sortBy = params.sortBy;
    if (params.sortOrder) apiParams.sortOrder = params.sortOrder;

    const { data } = await Api.get<ListEnvelope<Blog>>("blogs", apiParams);
    if (data) {
      searchCache.set(cacheKey, data);
    }
  }

  return {
    // Data
    activeBlogs: active.data,
    popularBlogs: popular.data,
    participatingBlogs: participating.data,

    // Loading states
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

    // Reset functions (for logout)
    resetParticipatingBlogs: participating.reset,

    /**
     * After a mutation changed which blogs exist. Distinct from the reset
     * above, which blanks the lists: that is right for logout and wrong here,
     * because the sidebar blocks fetch on mount and the shell mounts once per
     * session, so a blanked list stays blank until a reload.
     */
    invalidateBlogLists: async () => {
      clearSearchCache();
      await Promise.all([
        active.invalidate(),
        popular.invalidate(),
        participating.invalidate(),
      ]);
    },

    // Search API
    searchResult,
    searchLoading,
    searchError,
    searchBlogs,
    prefetchPage,
    clearSearchCache,
  };
});

/**
 * Store for single blog details (blog page) — mirrors useGameDetailsStore.
 * The blog page loads the blog; sub-pages and the sidebar BlogPanel add
 * their own slices (publications, comments, notepad, blacklist, users,
 * readers) and call the mutation actions.
 */
export const useBlogDetailsStore = defineStore("blogDetails", () => {
  // Blog data
  const blog = ref<Blog | null>(null);
  const blogLoading = ref(false);
  const blogError = ref<string | null>(null);

  // Publications (feed) data
  const publications = ref<Publication[]>([]);
  const publicationsPaging = ref<Paging | null>(null);
  const publicationsLoading = ref(false);
  const publicationsError = ref<string | null>(null);

  // Discussion comments data
  const comments = ref<Comment[]>([]);
  const commentsPaging = ref<Paging | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref<string | null>(null);

  // Blacklist data
  const blacklist = ref<User[]>([]);
  const blacklistLoading = ref(false);
  const blacklistError = ref<string | null>(null);

  // Blog users data (role-filtered lists come through blogApi.getUsers)
  const users = ref<BlogUser[]>([]);
  const usersLoading = ref(false);
  const usersError = ref<string | null>(null);

  // Readers (subscribers) — separate slice because isSubscribed derives
  // from it (unlike games, the blog DTO carries no participation flags).
  const readers = ref<BlogUser[]>([]);
  const readersLoading = ref(false);
  const readersError = ref<string | null>(null);

  // Rubrics sorted for display (panel list, feed filter, settings)
  const rubrics = computed(() =>
    [...(blog.value?.rubrics ?? [])].sort((a, b) => a.sortOrder - b.sortOrder),
  );

  // --- Current-user role flags — single source of truth for the panel and
  // pages. Blogs serve no participation flags, so roles are derived from the
  // author/assistants fields against the authenticated user.
  const auth = useAuthStore();
  const currentUsername = computed(() => auth.user?.username ?? null);

  const isOwner = computed(
    () =>
      !!currentUsername.value &&
      blog.value?.author?.username === currentUsername.value,
  );
  const isAssistant = computed(
    () =>
      !!currentUsername.value &&
      !isOwner.value &&
      (blog.value?.assistants ?? []).some(
        (a) => a.username === currentUsername.value,
      ),
  );
  /** Owner or assistant — may edit/manage the blog. */
  const canManage = computed(() => isOwner.value || isAssistant.value);
  /** Assigned blog mentor ("наставник") — oversight, not edit rights. */
  const isMentor = computed(
    () =>
      !!currentUsername.value &&
      blog.value?.mentor?.username === currentUsername.value,
  );
  /**
   * Owner, assistant, or mentor — may open the blog notepad (mirrors the
   * backend BlogNotepadService gate). Mentors get notepad oversight without
   * the owner/assistant edit rights carried by canManage.
   */
  const canUseNotepad = computed(
    () => isOwner.value || isAssistant.value || isMentor.value,
  );
  const isSubscribed = computed(
    () =>
      !!currentUsername.value &&
      readers.value.some((r) => r.user?.username === currentUsername.value),
  );

  // Load blog
  async function loadBlog(id: string): Promise<void> {
    blogLoading.value = true;
    blogError.value = null;

    const { data, error } = await blogApi.getBlog(id);

    if (error) {
      blogError.value = "Не удалось загрузить блог";
      blog.value = null;
    } else if (data) {
      // Unwrap the single-resource envelope defensively (mirrors loadGame).
      blog.value = data.resource ?? (data as unknown as Blog);
    }

    blogLoading.value = false;
  }

  // Load publications (optionally filtered by rubric)
  async function loadPublications(
    blogId: string,
    options?: { rubricId?: string; page?: number },
  ): Promise<void> {
    publicationsLoading.value = true;
    publicationsError.value = null;

    const { data, error } = await blogApi.getPublications(blogId, {
      rubricId: options?.rubricId,
      paging: { number: options?.page ?? 1 },
    });

    if (error) {
      publicationsError.value = "Не удалось загрузить публикации";
      publications.value = [];
      publicationsPaging.value = null;
    } else if (data) {
      publications.value = data.resources;
      publicationsPaging.value = data.paging ?? null;
    }

    publicationsLoading.value = false;
  }

  // Load discussion comments
  async function loadComments(blogId: string, page: number = 1): Promise<void> {
    commentsLoading.value = true;
    commentsError.value = null;

    const { data, error } = await blogApi.getBlogComments(blogId, {
      number: page,
    });

    if (error) {
      commentsError.value = "Не удалось загрузить комментарии";
      comments.value = [];
      commentsPaging.value = null;
    } else if (data) {
      comments.value = data.resources;
      commentsPaging.value = data.paging ?? null;
    }

    commentsLoading.value = false;
  }

  // --- Single discussion-comment mutations (edit / delete / likes) ---
  // Mirror the forum boardsStore idiom: optimistic in-place list patches
  // from the server response, no full reload.

  async function updateComment(id: string, text: string) {
    const { data } = await blogApi.updateBlogComment(id, { text });
    const updated = unwrapResource<Comment>(data);
    if (updated) {
      const index = comments.value.findIndex((c) => c.id === id);
      if (index !== -1) comments.value[index] = updated;
    }
  }

  async function deleteComment(id: string) {
    await blogApi.deleteBlogComment(id);
    const index = comments.value.findIndex((c) => c.id === id);
    if (index !== -1) {
      comments.value[index] = markRemoved(comments.value[index]);
    }
  }

  async function likeComment(id: string) {
    const { data } = await blogApi.likeBlogComment(id);
    const liker = unwrapResource<User>(data);
    if (liker) {
      const index = comments.value.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value[index];
        comments.value[index] = {
          ...comment,
          likes: [...(comment.likes ?? []), liker] as Comment["likes"],
        };
      }
    }
  }

  async function unlikeComment(id: string) {
    await blogApi.unlikeBlogComment(id);
    const index = comments.value.findIndex((c) => c.id === id);
    if (index === -1) return;
    const comment = comments.value[index];
    if (comment.likes && currentUsername.value) {
      comments.value[index] = {
        ...comment,
        likes: comment.likes.filter(
          (u) => u.username !== currentUsername.value,
        ) as Comment["likes"],
      };
    }
  }

  // Load blacklist
  async function loadBlacklist(blogId: string): Promise<void> {
    blacklistLoading.value = true;
    blacklistError.value = null;

    const { data, error } = await blogApi.getBlacklist(blogId);

    if (error) {
      blacklistError.value = "Не удалось загрузить черный список";
      blacklist.value = [];
    } else if (data) {
      blacklist.value = data.resources;
    }

    blacklistLoading.value = false;
  }

  // Load blog users
  async function loadUsers(blogId: string): Promise<void> {
    usersLoading.value = true;
    usersError.value = null;

    const { data, error } = await blogApi.getUsers(blogId);

    if (error) {
      usersError.value = "Не удалось загрузить участников";
      users.value = [];
    } else if (data) {
      users.value = data.resources;
    }

    usersLoading.value = false;
  }

  // Load readers (drives isSubscribed)
  async function loadReaders(blogId: string): Promise<void> {
    readersLoading.value = true;
    readersError.value = null;

    const { data, error } = await blogApi.getReaders(blogId);

    if (error) {
      readersError.value = "Не удалось загрузить читателей";
      readers.value = [];
    } else if (data) {
      readers.value = data.resources;
    }

    readersLoading.value = false;
  }

  // === Mutations ===
  // Each mutation calls the API, re-syncs the affected slices on success, and
  // returns the problem document on failure — null when it worked. A boolean
  // here threw away what the server said, so every rejection reached the reader
  // as the caller's own generic sentence.

  async function transitionStatus(
    transition: BlogStatusTransition,
  ): Promise<GeneralError | null> {
    if (!blog.value) return requestNotSent;
    const id = blog.value.id;
    const { error } = await blogApi.transitionStatus(id, transition);
    if (error) return error;
    await loadBlog(id);
    return null;
  }

  async function changePremoderation(
    transition: BlogPremoderationTransition,
  ): Promise<GeneralError | null> {
    if (!blog.value) return requestNotSent;
    const id = blog.value.id;
    const { error } = await blogApi.changePremoderation(id, transition);
    if (error) return error;
    await loadBlog(id);
    return null;
  }

  async function deleteBlog(): Promise<GeneralError | null> {
    if (!blog.value) return requestNotSent;
    const { error } = await blogApi.deleteBlog(blog.value.id);
    if (error) return error;
    // Same as games: without this the deleted blog stays in every list.
    await useBlogsStore().invalidateBlogLists();
    return null;
  }

  async function createRubric(
    input: CreateRubricInput,
  ): Promise<GeneralError | null> {
    if (!blog.value) return requestNotSent;
    const id = blog.value.id;
    const { error } = await blogApi.createRubric(id, input);
    if (error) return error;
    await loadBlog(id);
    return null;
  }

  async function deleteRubric(rubricId: string): Promise<GeneralError | null> {
    if (!blog.value) return requestNotSent;
    const { error } = await blogApi.deleteRubric(rubricId);
    if (error) return error;
    await loadBlog(blog.value.id);
    return null;
  }

  // Subscribe to blog (become a reader)
  async function subscribe(): Promise<GeneralError | null> {
    if (!blog.value) return requestNotSent;
    const id = blog.value.id;
    const { error } = await blogApi.subscribe(id);
    if (error) return error;
    await loadReaders(id);
    return null;
  }

  // Unsubscribe from blog
  async function unsubscribe(): Promise<GeneralError | null> {
    if (!blog.value) return requestNotSent;
    const id = blog.value.id;
    const { error } = await blogApi.unsubscribe(id);
    if (error) return error;
    await loadReaders(id);
    return null;
  }

  // Reset all data (when leaving the blog zone)
  function reset(): void {
    blog.value = null;
    blogLoading.value = false;
    blogError.value = null;

    publications.value = [];
    publicationsPaging.value = null;
    publicationsLoading.value = false;
    publicationsError.value = null;

    comments.value = [];
    commentsPaging.value = null;
    commentsLoading.value = false;
    commentsError.value = null;

    blacklist.value = [];
    blacklistLoading.value = false;
    blacklistError.value = null;

    users.value = [];
    usersLoading.value = false;
    usersError.value = null;

    readers.value = [];
    readersLoading.value = false;
    readersError.value = null;
  }

  return {
    // State
    blog,
    blogLoading,
    blogError,
    publications,
    publicationsPaging,
    publicationsLoading,
    publicationsError,
    comments,
    commentsPaging,
    commentsLoading,
    commentsError,
    blacklist,
    blacklistLoading,
    blacklistError,
    users,
    usersLoading,
    usersError,
    readers,
    readersLoading,
    readersError,

    // Computed
    rubrics,
    isOwner,
    isAssistant,
    canManage,
    isMentor,
    canUseNotepad,
    isSubscribed,

    // Actions
    loadBlog,
    loadPublications,
    loadComments,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
    loadBlacklist,
    loadUsers,
    loadReaders,
    transitionStatus,
    changePremoderation,
    deleteBlog,
    createRubric,
    deleteRubric,
    subscribe,
    unsubscribe,
    reset,
  };
});
