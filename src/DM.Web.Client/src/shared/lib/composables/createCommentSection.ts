import { ref } from "vue";
import type {
  ApiResult,
  Comment,
  Envelope,
  ListEnvelope,
  PagingInfo,
  User,
} from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
// Module imports rather than the transport barrel for the same reason the blog
// details store spells them out: this factory talks to the adapter it is
// handed, not to the HTTP client.
import type { CommentsQuery } from "@/shared/api/commentsQuery";
import { unwrapResource } from "@/shared/api/envelope";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

/**
 * The endpoints one discussion runs against, plus the reader's username for
 * the local unlike patch. Each details store binds its own API module here —
 * the factory lives in shared because an entity must not import another
 * entity, and games and blogs are two.
 */
export interface CommentSectionApi {
  getComments(
    ownerId: string,
    query: CommentsQuery,
  ): Promise<ApiResult<ListEnvelope<Comment>>>;
  updateComment(
    id: string,
    comment: { text: string },
  ): Promise<ApiResult<Envelope<Comment>>>;
  deleteComment(id: string): Promise<ApiResult<void>>;
  likeComment(id: string): Promise<ApiResult<Envelope<User>>>;
  unlikeComment(id: string): Promise<ApiResult<void>>;
  currentUsername(): string | null | undefined;
}

/**
 * Discussion-comments slice of a details store: the list state, the guarded
 * loader and the single-comment mutations, identical between the game and
 * blog zones down to the wording. Called inside the store's setup, so the
 * refs it returns are the store's own state; the guard comes back with them
 * for the store's reset to bump.
 */
export function createCommentSection(api: CommentSectionApi) {
  // The failure is a flag and not a sentence: the discussion section spells
  // one wording for a failed load, wherever it fails.
  const comments = ref<Comment[]>([]);
  const commentsPaging = ref<PagingInfo | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref(false);
  const commentsGuard = createRequestGuard();

  // Load comments. Filter, sort and page all come from the URL through the
  // discussion section; this forwards the query it is handed.
  async function loadComments(
    ownerId: string,
    query: CommentsQuery = {},
  ): Promise<void> {
    const requestId = commentsGuard.next();
    commentsLoading.value = true;
    commentsError.value = false;

    const { data, error } = await api.getComments(ownerId, query);

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

  // --- Single comment mutations (edit / delete / likes) ---
  // Mirror the forum boardsStore idiom: in-place list patches from the server
  // response, no full reload, and nothing patched when the server refused —
  // the error goes up to the page instead.

  async function updateComment(id: string, text: string) {
    const { data, error } = await api.updateComment(id, { text });
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
    const { error } = await api.deleteComment(id);
    if (!error) {
      const index = comments.value.findIndex((c) => c.id === id);
      if (index !== -1) {
        comments.value[index] = markRemoved(comments.value[index]);
      }
    }
    return { error };
  }

  async function likeComment(id: string) {
    const { data } = await api.likeComment(id);
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
    const { error } = await api.unlikeComment(id);
    if (error) return;
    const index = comments.value.findIndex((c) => c.id === id);
    if (index === -1) return;
    const comment = comments.value[index];
    const username = api.currentUsername();
    if (comment.likes && username) {
      comments.value[index] = {
        ...comment,
        likes: comment.likes.filter(
          (u) => u.username !== username,
        ) as Comment["likes"],
      };
    }
  }

  return {
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
  };
}
