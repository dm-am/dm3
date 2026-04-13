import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type {
  Comment,
  CommentId,
  Board,
  BoardId,
  Topic,
  TopicId,
  TopicsQuery,
  CommentsQuery,
} from "./types";
import type { User } from "@/shared/api/models/common";
import type { ListEnvelope } from "@/shared/api/models/common";
import forumApi from "../api/forumApi";
import { useAuthStore } from "@/shared/stores";
import { useApiList } from "@/shared/lib/composables/useApiResource";

export const useBoardsStore = defineStore("boards", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());

  // Boards are static - 5 minute cache
  const boardsResource = useApiList<Board>(() => forumApi.getBoards(), {
    cacheMs: 300_000,
  });
  const boards = boardsResource.data;
  const boardsLoading = boardsResource.loading;
  const fetchBoards = boardsResource.fetch;

  // News rarely changes - 5 minute cache
  const newsResource = useApiList<Topic>(() => forumApi.getNews(), {
    cacheMs: 300_000,
  });
  const news = newsResource.data;
  const fetchNews = newsResource.fetch;

  const selectedBoard = ref<Board | null>(null);
  async function trySelectBoard(id: BoardId) {
    const localBoard = boards.value?.find((f) => f.id === id);
    if (localBoard) selectedBoard.value = localBoard;

    const { error, data } = await forumApi.getBoard(id);
    if (error) return false;

    selectedBoard.value = data?.resource ?? null;
    return true;
  }

  async function trySelectBoardByAlias(alias: string) {
    const localBoard = boards.value?.find((f) => f.alias === alias);
    if (localBoard) selectedBoard.value = localBoard;

    const { error, data } = await forumApi.getBoard(alias as BoardId);
    if (error) return false;

    selectedBoard.value = data?.resource ?? null;
    return true;
  }

  const moderators = ref<User[] | null>(null);
  async function fetchModerators() {
    if (!selectedBoard.value) return;

    const { data } = await forumApi.getModerators(selectedBoard.value!.id);
    if (data) moderators.value = data.resources;
  }

  const attachedTopics = ref<Topic[] | null>(null);
  const topics = ref<ListEnvelope<Topic> | null>(null);
  const topicsLoading = ref(false);

  // Topics search cache (30s TTL, max 20 entries, stale-while-revalidate)
  const TOPICS_CACHE_TTL = 30_000;
  const topicsCache = new Map<string, { data: ListEnvelope<Topic>; attached: Topic[] | null; timestamp: number }>();

  function createTopicsCacheKey(boardAlias: string, query: TopicsQuery): string {
    return JSON.stringify({ board: boardAlias, ...query });
  }

  /**
   * Search topics with filters. Single source of truth for loading topics.
   * Called by TopicsList via paramsKey watcher.
   */
  async function searchTopics(query: TopicsQuery) {
    if (!selectedBoard.value) return;

    // Apply user's page size preference
    const size = query.size ?? currentUser.value?.settings?.paging?.topicsPerPage ?? 20;
    const fullQuery = { ...query, size };
    const boardAlias = selectedBoard.value.alias as BoardId;
    const cacheKey = createTopicsCacheKey(boardAlias as string, fullQuery);

    // Return cached if fresh
    const cached = topicsCache.get(cacheKey);
    const now = Date.now();
    if (cached && now - cached.timestamp < TOPICS_CACHE_TTL) {
      topics.value = cached.data;
      attachedTopics.value = cached.attached;
      return;
    }

    // Show stale while revalidating
    if (cached) {
      topics.value = cached.data;
      attachedTopics.value = cached.attached;
    }

    topicsLoading.value = true;
    try {
      const effectiveSortBy = query.sortBy ?? "lastActivity";
      const effectiveSortOrder = query.sortOrder ?? "desc";
      const hasFilters =
        !!query.search ||
        (query.authors && query.authors.length > 0) ||
        !!query.createdFromUtc ||
        !!query.createdToUtc ||
        effectiveSortBy !== "lastActivity" ||
        effectiveSortOrder !== "desc";

      let fetchedAttached: Topic[] | null = null;
      let fetchedTopics: ListEnvelope<Topic> | null = null;

      if (hasFilters) {
        // With filters: single unified query (attached mixed with regular)
        const { data } = await forumApi.getTopics(boardAlias, fullQuery);
        fetchedAttached = null;
        fetchedTopics = data ?? null;
      } else {
        // No filters: fetch attached separately, show at top
        const [attachedResult, regularResult] = await Promise.all([
          forumApi.getTopics(boardAlias, { isAttached: true }),
          forumApi.getTopics(boardAlias, { ...fullQuery, isAttached: false }),
        ]);
        fetchedAttached = attachedResult.data?.resources ?? null;
        fetchedTopics = regularResult.data ?? null;
      }

      attachedTopics.value = fetchedAttached;
      topics.value = fetchedTopics;

      // Update cache
      if (fetchedTopics) {
        topicsCache.set(cacheKey, { data: fetchedTopics, attached: fetchedAttached, timestamp: now });
        if (topicsCache.size > 20) {
          const firstKey = topicsCache.keys().next().value;
          if (firstKey) topicsCache.delete(firstKey);
        }
      }
    } finally {
      topicsLoading.value = false;
    }
  }

  /**
   * Reorder pinned topics (moderator action)
   */
  async function reorderPinnedTopics(topicIds: string[]) {
    if (!selectedBoard.value) return { error: new Error("No board selected") };

    const { error } = await forumApi.reorderPinnedTopics(selectedBoard.value.alias as BoardId, topicIds);
    if (error) return { error };

    // Refresh attached topics to reflect new order
    const { data } = await forumApi.getTopics(selectedBoard.value.alias as BoardId, { isAttached: true });
    attachedTopics.value = data?.resources ?? null;

    return { data: true };
  }

  /**
   * Toggle topic pin status (moderator action)
   */
  async function togglePinTopic(topicId: string) {
    if (!selectedBoard.value) return { error: new Error("No board selected") };

    // Find topic to get current pin status
    const topic = [...(attachedTopics.value ?? []), ...(topics.value?.resources ?? [])]
      .find(t => t.id === topicId);

    if (!topic) return { error: new Error("Topic not found") };

    const newStatus = !topic.isAttached;
    const { error } = await forumApi.updateTopic(topicId as TopicId, { isAttached: newStatus } as any);
    if (error) return { error };

    // Refresh topics list
    const [fetchedAttached, fetchedRegular] = await Promise.all([
      forumApi.getTopics(selectedBoard.value.alias as BoardId, { isAttached: true }),
      forumApi.getTopics(selectedBoard.value.alias as BoardId, { isAttached: false }),
    ]);

    attachedTopics.value = fetchedAttached.data?.resources ?? null;
    topics.value = fetchedRegular.data ?? null;

    return { data: newStatus };
  }

  const selectedTopic = ref<Topic | null>(null);
  async function trySelectTopic(id: TopicId) {
    if (selectedTopic.value?.id !== id) selectedTopic.value = null;
    const { data } = await forumApi.getTopic(id);
    const topic = data?.resource;
    if (!topic) return;

    selectedTopic.value = topic;
    await trySelectBoard(topic.board.id);
  }

  async function trySelectTopicByNumber(boardAlias: string, topicNumber: number) {
    selectedTopic.value = null;
    const { data } = await forumApi.getTopicByNumber(boardAlias, topicNumber);
    const topic = data?.resource;
    if (!topic) return false;

    selectedTopic.value = topic;
    selectedBoard.value = topic.board;
    return true;
  }

  const comments = ref<ListEnvelope<Comment> | null>(null);
  const commentsLoading = ref(false);

  // Comments search cache (30s TTL, max 20 entries)
  const COMMENTS_CACHE_TTL = 30_000;
  const commentsCache = new Map<string, { data: ListEnvelope<Comment>; timestamp: number }>();

  function createCommentsCacheKey(topicId: string, query: CommentsQuery): string {
    return JSON.stringify({ topic: topicId, ...query });
  }

  /**
   * Search comments with filters. Single source of truth for loading comments.
   * Called by CommentsList via watcher.
   */
  async function searchComments(query: CommentsQuery) {
    if (!selectedTopic.value) return;

    const size = query.size ?? currentUser.value?.settings?.paging?.commentsPerPage ?? 20;
    const fullQuery: CommentsQuery = { ...query, size };
    const topicId = selectedTopic.value.id!;
    const cacheKey = createCommentsCacheKey(topicId, fullQuery);

    // Return cached if fresh
    const cached = commentsCache.get(cacheKey);
    const now = Date.now();
    if (cached && now - cached.timestamp < COMMENTS_CACHE_TTL) {
      comments.value = cached.data;
      return;
    }

    // Show stale while revalidating
    if (cached) {
      comments.value = cached.data;
    } else {
      comments.value = null;
    }

    commentsLoading.value = true;
    try {
      const { data } = await forumApi.getComments(topicId, fullQuery);
      comments.value = data ?? null;

      // Update cache
      if (data) {
        commentsCache.set(cacheKey, { data, timestamp: now });
        if (commentsCache.size > 20) {
          const firstKey = commentsCache.keys().next().value;
          if (firstKey) commentsCache.delete(firstKey);
        }
      }
    } finally {
      commentsLoading.value = false;
    }
  }

  // Legacy method for backwards compatibility
  async function fetchComments(number: number) {
    await searchComments({ number });
  }

  async function createComment(text: string) {
    if (!selectedTopic.value) return { error: new Error("No topic selected") };

    const { data, error } = await forumApi.createComment(
      selectedTopic.value.id!,
      { text },
    );
    if (error) return { error };

    // Add the new comment to the list
    if (data && comments.value) {
      comments.value.resources.push(data);
      // Update paging info
      if (comments.value.paging) {
        comments.value.paging.total = (comments.value.paging.total || 0) + 1;
      }
    }
    // Update topic's comment count
    if (selectedTopic.value) {
      (selectedTopic.value as any).commentsCount =
        (selectedTopic.value.commentsCount || 0) + 1;
    }

    return { data };
  }

  async function updateComment(id: string, text: string) {
    const { data } = await forumApi.updateComment(id as CommentId, { text });
    if (data && comments.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        comments.value.resources[index] = data;
      }
    }
  }

  async function deleteComment(id: string) {
    await forumApi.deleteComment(id as CommentId);
    if (comments.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        comments.value.resources[index] = {
          ...comments.value.resources[index],
          isRemoved: true as unknown as Comment["isRemoved"],
        };
      }
    }
  }

  async function likeComment(id: string) {
    const { data } = await forumApi.postCommentLike(id as CommentId);
    if (data && comments.value && currentUser.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value.resources[index];
        const existingLikes =
          comment.likes || ([] as unknown as Comment["likes"]);
        comments.value.resources[index] = {
          ...comment,
          likes: [...existingLikes, data] as Comment["likes"],
        };
      }
    }
  }

  async function unlikeComment(id: string) {
    await forumApi.deleteCommentLike(id as CommentId);
    if (comments.value && currentUser.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value.resources[index];
        if (comment.likes) {
          comments.value.resources[index] = {
            ...comment,
            likes: comment.likes.filter(
              (u) => u.username !== currentUser.value?.username,
            ) as Comment["likes"],
          };
        }
      }
    }
  }

  async function likeTopic(id: string) {
    const { data } = await forumApi.postTopicLike(id as TopicId);
    if (data && selectedTopic.value && selectedTopic.value.id === id) {
      const existingLikes =
        selectedTopic.value.likes || ([] as unknown as Topic["likes"]);
      selectedTopic.value = {
        ...selectedTopic.value,
        likes: [...existingLikes, data] as Topic["likes"],
      };
    }
  }

  async function unlikeTopic(id: string) {
    await forumApi.deleteTopicLike(id as TopicId);
    if (
      selectedTopic.value &&
      selectedTopic.value.id === id &&
      currentUser.value
    ) {
      if (selectedTopic.value.likes) {
        selectedTopic.value = {
          ...selectedTopic.value,
          likes: selectedTopic.value.likes.filter(
            (u) => u.username !== currentUser.value?.username,
          ) as Topic["likes"],
        };
      }
    }
  }

  return {
    boards,
    boardsLoading,
    fetchBoards,
    selectedBoard,
    trySelectBoard,
    trySelectBoardByAlias,
    moderators,
    fetchModerators,
    attachedTopics,
    topics,
    topicsLoading,
    searchTopics,
    reorderPinnedTopics,
    togglePinTopic,
    news,
    fetchNews,
    trySelectTopic,
    trySelectTopicByNumber,
    selectedTopic,
    fetchComments,
    searchComments,
    comments,
    commentsLoading,
    createComment,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
    likeTopic,
    unlikeTopic,
  };
});
