// Kept apart from the list store, which the entry reaches through the sidebar:
// in one module the whole blog zone landed in the entry chunk.

import { defineStore } from "pinia";
import { computed, ref, type ComputedRef, type Ref } from "vue";
import type {
  GeneralError,
  PagingInfo,
  User,
} from "@/shared/api/models/common";
import type {
  Blog,
  BlogPremoderationTransition,
  BlogStatusTransition,
  BlogUser,
  CreateRubricInput,
  Publication,
} from "./types";
import blogApi from "../api/blogApi";
// From its own module, so the store does not depend on the HTTP client.
import { unwrapResource } from "@/shared/api/envelope";
import { useAuthStore } from "@/shared/stores";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { createCommentSection } from "@/shared/lib/composables/createCommentSection";
import { requestNotSent } from "@/shared/lib/errors";
// Deleting a blog drops the list caches; the list store must not import this one back.
import { useBlogsStore } from "./store";
import type { RequestOptions } from "@/shared/api/interceptor";

export const useBlogDetailsStore = defineStore("blogDetails", () => {
  const {
    blog,
    blogLoading,
    blogError,
    blogErrorStatus,
    blogGuard,
    loadBlog,
    resetBlog,
  } = createBlogSlice();
  const {
    publications,
    publicationsPaging,
    publicationsLoading,
    publicationsError,
    publicationsGuard,
    loadPublications,
    resetPublications,
  } = createPublicationsSlice();

  // The reader the roles and the discussion are read against.
  const auth = useAuthStore();
  const currentUsername = computed(() => auth.user?.username ?? null);

  // The username goes in as a getter: the section reads it at unlike time.
  const {
    comments,
    commentsPaging,
    commentsLoading,
    commentsError,
    commentsGuard,
    loadComments,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
  } = createCommentSection({
    getComments: (blogId, query) => blogApi.getBlogComments(blogId, query),
    updateComment: (id, comment) => blogApi.updateBlogComment(id, comment),
    deleteComment: (id, options) => blogApi.deleteBlogComment(id, options),
    likeComment: (id) => blogApi.likeBlogComment(id),
    unlikeComment: (id) => blogApi.unlikeBlogComment(id),
    currentUsername: () => currentUsername.value,
  });

  const blacklist = ref<User[]>([]);
  const blacklistLoading = ref(false);
  const blacklistError = ref<string | null>(null);

  const users = ref<BlogUser[]>([]);
  const usersLoading = ref(false);
  const usersError = ref<string | null>(null);

  // isSubscribed is read off the readers: the blog carries no participation flags.
  const readers = ref<BlogUser[]>([]);
  const readersLoading = ref(false);
  const readersError = ref<string | null>(null);

  // One token per slice, so a slower reply for the blog already left cannot win by landing last.
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

  const {
    isOwner,
    isAssistant,
    canManage,
    isMentor,
    canUseNotepad,
    isSubscribed,
  } = createBlogRoles(blog, readers, currentUsername);

  async function loadBlacklist(blogId: string): Promise<void> {
    const requestId = blacklistGuard.next();
    blacklistLoading.value = true;
    blacklistError.value = null;

    const { data, error } = await blogApi.getBlacklist(blogId);

    if (!blacklistGuard.isCurrent(requestId)) return;

    if (error) {
      blacklistError.value = "Не удалось загрузить черный список";
      blacklist.value = [];
    } else if (data) {
      blacklist.value = data.resources;
    }

    blacklistLoading.value = false;
  }

  async function loadUsers(blogId: string): Promise<void> {
    const requestId = usersGuard.next();
    usersLoading.value = true;
    usersError.value = null;

    const { data, error } = await blogApi.getUsers(blogId);

    if (!usersGuard.isCurrent(requestId)) return;

    if (error) {
      usersError.value = "Не удалось загрузить участников";
      users.value = [];
    } else if (data) {
      users.value = data.resources;
    }

    usersLoading.value = false;
  }

  async function loadReaders(blogId: string): Promise<void> {
    const requestId = readersGuard.next();
    readersLoading.value = true;
    readersError.value = null;

    const { data, error } = await blogApi.getReaders(blogId);

    if (!readersGuard.isCurrent(requestId)) return;

    if (error) {
      readersError.value = "Не удалось загрузить читателей";
      readers.value = [];
    } else if (data) {
      readers.value = data.resources;
    }

    readersLoading.value = false;
  }

  const { rubrics, createRubric, deleteRubric } = createRubrics(blog, loadBlog);
  const { transitionStatus, changePremoderation, deleteBlog } =
    createBlogChanges(blog, loadBlog);
  const { subscribe, unsubscribe } = createSubscriptionChanges(
    blog,
    loadReaders,
  );

  function reset(): void {
    // Replies still on the wire belong to the blog being left.
    detailGuards.forEach((guard) => guard.next());

    resetBlog();
    resetPublications();

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

function createBlogSlice() {
  const blog = ref<Blog | null>(null);
  const blogLoading = ref(false);
  const blogError = ref<string | null>(null);
  // Kept in the store: loadBlog has callers outside the shell, and any refusal nulls `blog`.
  const blogErrorStatus = ref<number | null>(null);
  const blogGuard = createRequestGuard();

  /** A missing blog, a refused one and a broken server are three different pages. */
  async function loadBlog(
    id: string,
  ): Promise<{ ok: boolean; status?: number }> {
    const requestId = blogGuard.next();
    blogLoading.value = true;
    blogError.value = null;
    blogErrorStatus.value = null;

    const { data, error } = await blogApi.getBlog(id);

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

  function resetBlog(): void {
    blog.value = null;
    blogLoading.value = false;
    blogError.value = null;
    blogErrorStatus.value = null;
  }

  return {
    blog,
    blogLoading,
    blogError,
    blogErrorStatus,
    blogGuard,
    loadBlog,
    resetBlog,
  };
}

function createPublicationsSlice() {
  const publications = ref<Publication[]>([]);
  const publicationsPaging = ref<PagingInfo | null>(null);
  const publicationsLoading = ref(false);
  const publicationsError = ref<string | null>(null);
  const publicationsGuard = createRequestGuard();

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

  function resetPublications(): void {
    publications.value = [];
    publicationsPaging.value = null;
    publicationsLoading.value = false;
    publicationsError.value = null;
  }

  return {
    publications,
    publicationsPaging,
    publicationsLoading,
    publicationsError,
    publicationsGuard,
    loadPublications,
    resetPublications,
  };
}

// The blog serves no participation flags, so roles are read off its author fields.
function createBlogRoles(
  blog: Ref<Blog | null>,
  readers: Ref<BlogUser[]>,
  currentUsername: ComputedRef<string | null>,
) {
  const isViewer = (username: string | undefined) =>
    !!currentUsername.value && username === currentUsername.value;

  const isOwner = computed(() => isViewer(blog.value?.author?.username));
  const isAssistant = computed(
    () =>
      !isOwner.value &&
      (blog.value?.assistants ?? []).some((a) => isViewer(a.username)),
  );
  const canManage = computed(() => isOwner.value || isAssistant.value);
  const isMentor = computed(() => isViewer(blog.value?.mentor?.username));
  // The mentor reads the notepad without the rights canManage carries.
  const canUseNotepad = computed(
    () => isOwner.value || isAssistant.value || isMentor.value,
  );
  const isSubscribed = computed(() =>
    readers.value.some((r) => isViewer(r.user?.username)),
  );

  return {
    isOwner,
    isAssistant,
    canManage,
    isMentor,
    canUseNotepad,
    isSubscribed,
  };
}

// A mutation answers the problem document or null, so the caller can say what the server said.

type LoadBlog = (id: string) => Promise<unknown>;

// Sent for the blog on screen; the reload follows only a change the server took.
async function onBlog(
  blog: Ref<Blog | null>,
  send: (id: string) => Promise<{ error: GeneralError | null }>,
  reload: (id: string) => Promise<unknown>,
): Promise<GeneralError | null> {
  if (!blog.value) return requestNotSent;
  const id = blog.value.id;
  const { error } = await send(id);
  if (error) return error;
  await reload(id);
  return null;
}

function createRubrics(blog: Ref<Blog | null>, loadBlog: LoadBlog) {
  const rubrics = computed(() =>
    [...(blog.value?.rubrics ?? [])].sort((a, b) => a.sortOrder - b.sortOrder),
  );

  const createRubric = (input: CreateRubricInput) =>
    onBlog(blog, (id) => blogApi.createRubric(id, input), loadBlog);

  const deleteRubric = (rubricId: string, options?: RequestOptions) =>
    onBlog(blog, (id) => blogApi.deleteRubric(id, rubricId, options), loadBlog);

  return { rubrics, createRubric, deleteRubric };
}

function createBlogChanges(blog: Ref<Blog | null>, loadBlog: LoadBlog) {
  const transitionStatus = (
    transition: BlogStatusTransition,
    options?: RequestOptions,
  ) =>
    onBlog(
      blog,
      (id) => blogApi.transitionStatus(id, transition, options),
      loadBlog,
    );

  const changePremoderation = (
    transition: BlogPremoderationTransition,
    options?: RequestOptions,
  ) =>
    onBlog(
      blog,
      (id) => blogApi.changePremoderation(id, transition, options),
      loadBlog,
    );

  // Without the lists' reload the deleted blog stays in every list.
  const deleteBlog = (options?: RequestOptions) =>
    onBlog(
      blog,
      (id) => blogApi.deleteBlog(id, options),
      () => useBlogsStore().invalidateBlogLists(),
    );

  return { transitionStatus, changePremoderation, deleteBlog };
}

function createSubscriptionChanges(
  blog: Ref<Blog | null>,
  loadReaders: (blogId: string) => Promise<void>,
) {
  const subscribe = () =>
    onBlog(blog, (id) => blogApi.subscribe(id), loadReaders);

  const unsubscribe = () =>
    onBlog(blog, (id) => blogApi.unsubscribe(id), loadReaders);

  return { subscribe, unsubscribe };
}
