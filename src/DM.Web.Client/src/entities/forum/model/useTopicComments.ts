import { ref, type Ref } from "vue";
import type { Comment, CommentId, Topic, CommentsQuery } from "./types";
import type { ListEnvelope, User } from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
import { asServed } from "@/shared/api/models";
import { unwrapResource } from "@/shared/api";
import { requestNotSent } from "@/shared/lib/errors";
import forumApi from "../api/forumApi";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import type { RequestOptions } from "@/shared/api/interceptor";

export interface TopicCommentsOptions {
  /** The open topic: its comments are the ones read and written. */
  selectedTopic: Ref<Topic | null>;
  /** The reader, whose like is the one taken back. */
  currentUser: Ref<User | null>;
  /** The reader's page size, for a query that names none of its own. */
  pageSize: Readonly<Ref<number>>;
}

/** The comments of the open topic, each change patched in from the server's answer. */
export function useTopicComments({
  selectedTopic,
  currentUser,
  pageSize,
}: TopicCommentsOptions) {
  const comments = ref<ListEnvelope<Comment> | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref(false);

  const commentsCache = createKeyedCache<ListEnvelope<Comment>>({
    ttlMs: 30_000,
  });

  async function searchComments(query: CommentsQuery) {
    if (!selectedTopic.value) return;

    const size = query.size ?? pageSize.value;
    const fullQuery: CommentsQuery = { ...query, size };
    const topicId = selectedTopic.value.id;
    const cacheKey = stableCacheKey({ topic: topicId, ...fullQuery });

    const fresh = commentsCache.get(cacheKey);
    if (fresh) {
      comments.value = fresh;
      return;
    }

    // Without a stale entry the list on screen stays until the answer arrives.
    const stale = commentsCache.getStale(cacheKey);
    if (stale) {
      comments.value = stale;
    }

    commentsLoading.value = true;
    commentsError.value = false;
    try {
      const { data, error } = await forumApi.getComments(topicId, fullQuery);

      // Keeps the rows already shown rather than a fake empty list.
      if (error) {
        commentsError.value = true;
        return;
      }

      comments.value = data ?? null;

      if (data) {
        commentsCache.set(cacheKey, data);
      }
    } finally {
      commentsLoading.value = false;
    }
  }

  return {
    comments,
    commentsLoading,
    commentsError,
    searchComments,
    ...commentChanges(comments, selectedTopic),
    ...commentLikes(comments, currentUser),
  };
}

function commentChanges(
  comments: Ref<ListEnvelope<Comment> | null>,
  selectedTopic: Ref<Topic | null>,
) {
  async function createComment(text: string) {
    if (!selectedTopic.value) return { error: requestNotSent };

    const { data, error } = await forumApi.createComment(
      selectedTopic.value.id,
      { text },
    );
    if (error) return { error };

    const created = unwrapResource<Comment>(data);
    if (created && comments.value) {
      comments.value.resources.push(created);
      if (comments.value.paging) {
        comments.value.paging.total = (comments.value.paging.total || 0) + 1;
      }
    }
    // The topic may have been left while the comment was on its way.
    if (selectedTopic.value) {
      selectedTopic.value.commentsCount = (selectedTopic.value.commentsCount +
        1) as Topic["commentsCount"];
    }

    return { data };
  }

  // Patched from the answer only: `Api` resolves on a refusal too.
  async function updateComment(id: string, text: string) {
    const { data, error } = await forumApi.updateComment(id as CommentId, {
      text,
    });
    if (!error) {
      const updated = unwrapResource<Comment>(data);
      if (updated && comments.value) {
        const index = comments.value.resources.findIndex((c) => c.id === id);
        if (index !== -1) {
          comments.value.resources[index] = updated;
        }
      }
    }
    return { error };
  }

  async function deleteComment(id: string, options?: RequestOptions) {
    const { error } = await forumApi.deleteComment(id as CommentId, options);
    if (!error && comments.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        comments.value.resources[index] = markRemoved(
          comments.value.resources[index],
        );
      }
    }
    return { error };
  }

  return { createComment, updateComment, deleteComment };
}

function commentLikes(
  comments: Ref<ListEnvelope<Comment> | null>,
  currentUser: Ref<User | null>,
) {
  // The error goes back: only the item that was pressed can say a like was refused.
  async function likeComment(id: string) {
    const { data, error } = await forumApi.postCommentLike(id as CommentId);
    const liker = unwrapResource<User>(data);
    if (liker && comments.value && currentUser.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value.resources[index];
        const existingLikes = comment.likes || [];
        comments.value.resources[index] = {
          ...comment,
          likes: asServed([...existingLikes, liker]),
        };
      }
    }
    return { error };
  }

  async function unlikeComment(id: string) {
    const { error } = await forumApi.deleteCommentLike(id as CommentId);
    if (!error && comments.value && currentUser.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value.resources[index];
        if (comment.likes) {
          comments.value.resources[index] = {
            ...comment,
            likes: asServed(
              comment.likes.filter(
                (u) => u.username !== currentUser.value?.username,
              ),
            ),
          };
        }
      }
    }
    return { error };
  }

  return { likeComment, unlikeComment };
}
