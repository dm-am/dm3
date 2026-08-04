// Store for a single blog: the zone shell, its sub-pages and the blog panel.
//
// Its own module rather than a second store next to the list store, for the
// reason the game zone was split for: the sidebar blocks that draw the blog
// lists are reachable from the entry, and a store declared at the top level of
// a module is not something the bundler may drop, so sharing one file put every
// publication, comment, notepad and blacklist request of the blog zone into the
// entry chunk with them.

import { defineStore } from "pinia";
import { computed, ref } from "vue";
import type {
  Comment,
  GeneralError,
  PagingInfo,
  User,
} from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
import type {
  Blog,
  BlogPremoderationTransition,
  BlogStatusTransition,
  BlogUser,
  CreateRubricInput,
  Publication,
} from "./types";
import blogApi from "../api/blogApi";
import { type CommentsQuery } from "@/shared/api";
// The envelope reader is a pure function over a payload shape, so it comes from
// its own module instead of the transport barrel: this store talks to blogApi
// and not to the HTTP client, and pulling a helper through the barrel made it
// depend on the client for nothing.
import { unwrapResource } from "@/shared/api/envelope";
import { useAuthStore } from "@/shared/stores";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { requestNotSent } from "@/shared/lib/errors";
// One edge, and it points this way on purpose: deleting a blog has to drop the
// list caches. The list store must not import this one back — that is what put
// the whole blog zone in the entry chunk in the first place.
import { useBlogsStore } from "./store";

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
  /**
   * HTTP status of the refusal, kept alongside the sentence (twin of
   * gameErrorStatus).
   *
   * The shell cannot keep this in a local ref: loadBlog is called from five
   * places outside it (PublicationCreate, PublicationEdit, BlogInfoSection and
   * RolesSection twice), and a refusal from any of them nulls `blog` while a
   * shell-local code stays null — the blog zone then holds its title skeleton
   * forever instead of drawing the refusal.
   */
  const blogErrorStatus = ref<number | null>(null);

  // Publications (feed) data
  const publications = ref<Publication[]>([]);
  const publicationsPaging = ref<PagingInfo | null>(null);
  const publicationsLoading = ref(false);
  const publicationsError = ref<string | null>(null);

  // Discussion comments data. The failure is a flag and not a sentence: the
  // discussion section spells one wording for a failed load, wherever it fails.
  const comments = ref<Comment[]>([]);
  const commentsPaging = ref<PagingInfo | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref(false);

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

  // One monotonic token per independent slice — same reason as in the game
  // details store: this store is a single bag for "the current blog", and a
  // slower reply for the blog (or rubric, or page) the reader already left
  // would otherwise win simply by landing last.
  const blogGuard = createRequestGuard();
  const publicationsGuard = createRequestGuard();
  const commentsGuard = createRequestGuard();
  const blacklistGuard = createRequestGuard();
  const usersGuard = createRequestGuard();
  const readersGuard = createRequestGuard();
  const detailGuards = [
    blogGuard,
    publicationsGuard,
    commentsGuard,
    blacklistGuard,
    usersGuard,
    readersGuard,
  ];

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

  /**
   * Load the blog the URL names.
   *
   * Returns `{ ok, status }` for the same reason loadGame does: a missing blog,
   * a refused one and a broken server are three pages. `blogError` stays for
   * the sidebar panel.
   */
  async function loadBlog(
    id: string,
  ): Promise<{ ok: boolean; status?: number }> {
    const requestId = blogGuard.next();
    blogLoading.value = true;
    blogError.value = null;
    blogErrorStatus.value = null;

    const { data, error } = await blogApi.getBlog(id);

    // A newer load owns the visible state, and reports ok as a no-op - see the
    // guards above.
    if (!blogGuard.isCurrent(requestId)) return { ok: true };

    if (error) {
      blogError.value = "Не удалось загрузить блог";
      blogErrorStatus.value = error.status ?? null;
      blog.value = null;
      blogLoading.value = false;
      return { ok: false, status: error.status };
    }
    if (data) {
      blog.value = unwrapResource<Blog>(data);
    }

    blogLoading.value = false;
    return { ok: true };
  }

  // Load publications (optionally filtered by rubric)
  async function loadPublications(
    blogId: string,
    options?: { rubricId?: string; page?: number },
  ): Promise<void> {
    const requestId = publicationsGuard.next();
    publicationsLoading.value = true;
    publicationsError.value = null;

    const { data, error } = await blogApi.getPublications(blogId, {
      rubricId: options?.rubricId,
      paging: { number: options?.page ?? 1 },
    });

    // Stale continuation — the newer request owns the visible state.
    if (!publicationsGuard.isCurrent(requestId)) return;

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

  // Load discussion comments. Filter, sort and page all come from the URL
  // through the discussion section; this forwards the query it is handed.
  async function loadComments(
    blogId: string,
    query: CommentsQuery = {},
  ): Promise<void> {
    const requestId = commentsGuard.next();
    commentsLoading.value = true;
    commentsError.value = false;

    const { data, error } = await blogApi.getBlogComments(blogId, query);

    // Stale continuation — the newer request owns the visible state.
    if (!commentsGuard.isCurrent(requestId)) return;

    if (error) {
      commentsError.value = true;
      comments.value = [];
      commentsPaging.value = null;
    } else if (data) {
      comments.value = data.resources;
      commentsPaging.value = data.paging ?? null;
    }

    commentsLoading.value = false;
  }

  // --- Single discussion-comment mutations (edit / delete / likes) ---
  // Mirror the forum boardsStore idiom: in-place list patches from the server
  // response, no full reload, and nothing patched when the server refused —
  // the error goes up to the page instead.

  async function updateComment(id: string, text: string) {
    const { data, error } = await blogApi.updateBlogComment(id, { text });
    if (!error) {
      const updated = unwrapResource<Comment>(data);
      if (updated) {
        const index = comments.value.findIndex((c) => c.id === id);
        if (index !== -1) comments.value[index] = updated;
      }
    }
    return { error };
  }

  async function deleteComment(id: string) {
    const { error } = await blogApi.deleteBlogComment(id);
    if (!error) {
      const index = comments.value.findIndex((c) => c.id === id);
      if (index !== -1) {
        comments.value[index] = markRemoved(comments.value[index]);
      }
    }
    return { error };
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
    const { error } = await blogApi.unlikeBlogComment(id);
    if (error) return;
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
    const requestId = blacklistGuard.next();
    blacklistLoading.value = true;
    blacklistError.value = null;

    const { data, error } = await blogApi.getBlacklist(blogId);

    // Stale continuation — the newer request owns the visible state.
    if (!blacklistGuard.isCurrent(requestId)) return;

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
    const requestId = usersGuard.next();
    usersLoading.value = true;
    usersError.value = null;

    const { data, error } = await blogApi.getUsers(blogId);

    // Stale continuation — the newer request owns the visible state.
    if (!usersGuard.isCurrent(requestId)) return;

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
    const requestId = readersGuard.next();
    readersLoading.value = true;
    readersError.value = null;

    const { data, error } = await blogApi.getReaders(blogId);

    // Stale continuation — the newer request owns the visible state. This slice
    // decides isSubscribed, so a reply for another blog flips the subscribe
    // button rather than only the list under it.
    if (!readersGuard.isCurrent(requestId)) return;

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
    // Replies still on the wire belong to the blog being left — bump every
    // token so none of them repopulates the store after the wipe.
    detailGuards.forEach((guard) => guard.next());

    blog.value = null;
    blogLoading.value = false;
    blogError.value = null;
    blogErrorStatus.value = null;

    publications.value = [];
    publicationsPaging.value = null;
    publicationsLoading.value = false;
    publicationsError.value = null;

    comments.value = [];
    commentsPaging.value = null;
    commentsLoading.value = false;
    commentsError.value = false;

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
    blogErrorStatus,
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
