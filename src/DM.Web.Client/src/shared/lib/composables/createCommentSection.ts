import { ref, type Ref } from "vue";
import type {
  ApiResult,
  Comment,
  Envelope,
  ListEnvelope,
  PagingInfo,
  User,
} from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
// Module imports, not the transport barrel: this talks to the adapter it is handed.
import type { CommentsQuery } from "@/shared/api/commentsQuery";
import { unwrapResource } from "@/shared/api/envelope";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import type { RequestOptions } from "@/shared/api";

/**
 * The endpoints of one discussion and the reader's username for the unlike
 * patch; in shared because games and blogs are two entities.
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
  /** The confirmation prints a refused delete itself (options). */
  deleteComment(id: string, options?: RequestOptions): Promise<ApiResult<void>>;
  likeComment(id: string): Promise<ApiResult<Envelope<User>>>;
  unlikeComment(id: string): Promise<ApiResult<void>>;
  currentUsername(): string | null | undefined;
}

/**
 * The comments slice of a details store, called inside its setup so the refs
 * are the store's state; the guard comes back for the store's reset to bump.
 */
export function createCommentSection(api: CommentSectionApi) {
  const list = commentList(api);
  return { ...list, ...commentChanges(list.comments, api) };
}

/** The list of a discussion, and its loader under the stale-answer guard. */
function commentList(api: CommentSectionApi) {
  // A flag, not a sentence: the discussion section words a failed load once.
  const comments = ref<Comment[]>([]);
  const commentsPaging = ref<PagingInfo | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref(false);
  const commentsGuard = createRequestGuard();

  async function loadComments(
    ownerId: string,
    query: CommentsQuery = {},
  ): Promise<void> {
    const requestId = commentsGuard.next();
    commentsLoading.value = true;
    commentsError.value = false;

    const { data, error } = await api.getComments(ownerId, query);

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

  return {
    comments,
    commentsPaging,
    commentsLoading,
    commentsError,
    commentsGuard,
    loadComments,
  };
}

/**
 * Edit, delete and likes patch the list in place from the server's answer; a
 * refusal patches nothing and goes up to the page.
 */
function commentChanges(comments: Ref<Comment[]>, api: CommentSectionApi) {
  function patch(id: string, change: (comment: Comment) => Comment): void {
    const index = comments.value.findIndex((c) => c.id === id);
    if (index !== -1) comments.value[index] = change(comments.value[index]);
  }

  async function updateComment(id: string, text: string) {
    const { data, error } = await api.updateComment(id, { text });
    if (!error) {
      const updated = unwrapResource<Comment>(data);
      if (updated) patch(id, () => updated);
    }
    return { error };
  }

  async function deleteComment(id: string, options?: RequestOptions) {
    const { error } = await api.deleteComment(id, options);
    if (!error) patch(id, markRemoved);
    return { error };
  }

  // A refused like leaves "Нравится" as it was, and only the pressed item says so.
  async function likeComment(id: string) {
    const { data, error } = await api.likeComment(id);
    const liker = unwrapResource<User>(data);
    if (liker) {
      patch(id, (comment) => ({
        ...comment,
        likes: [...(comment.likes ?? []), liker] as Comment["likes"],
      }));
    }
    return { error };
  }

  async function unlikeComment(id: string) {
    const { error } = await api.unlikeComment(id);
    if (error) return { error };
    const username = api.currentUsername();
    if (!username) return { error };
    patch(id, (comment) =>
      comment.likes
        ? {
            ...comment,
            likes: comment.likes.filter(
              (u) => u.username !== username,
            ) as Comment["likes"],
          }
        : comment,
    );
    return { error };
  }

  return { updateComment, deleteComment, likeComment, unlikeComment };
}
