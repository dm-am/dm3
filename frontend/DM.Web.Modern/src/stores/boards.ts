import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type {
  Comment,
  CommentId,
  Board,
  BoardId,
  Topic,
  TopicId,
} from "@/api/models/forum";
import type { User } from "@/api/models/community";
import type { ListEnvelope, PagingQuery } from "@/api/models/common";
import forumApi from "@/api/requests/forumApi";
import { useUserStore } from "@/stores/user";
import { useApiList } from "@/composables/useApiResource";

export const useBoardsStore = defineStore("boards", () => {
  const { user: currentUser } = storeToRefs(useUserStore());

  const boardsResource = useApiList<Board>(() => forumApi.getBoards());
  const boards = boardsResource.data;
  const fetchBoards = boardsResource.fetch;

  const news = ref<Topic[] | null>(null);
  async function fetchNews() {
    const { data } = await forumApi.getNews();
    news.value = data?.resources ?? [];
  }

  const selectedBoard = ref<Board | null>(null);
  async function trySelectBoard(id: BoardId) {
    const localBoard = boards.value?.find((f) => f.id === id);
    if (localBoard) selectedBoard.value = localBoard;

    const { error, data } = await forumApi.getBoard(id);
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
  async function fetchTopics(number: number) {
    if (!selectedBoard.value) return;

    const size = currentUser.value?.settings?.pagingLimits?.topicsPerPage;
    const query: PagingQuery = { number, size };
    const [fetchedAttachedTopics, fetchedTopics] = await Promise.all([
      forumApi.getTopics(selectedBoard.value!.id, query, true),
      forumApi.getTopics(selectedBoard.value!.id, query, false),
    ]);

    attachedTopics.value = fetchedAttachedTopics.data?.resources ?? null;
    topics.value = fetchedTopics.data ?? null;
  }

  const selectedTopic = ref<Topic | null>(null);
  async function trySelectTopic(id: TopicId) {
    if (selectedTopic.value?.id !== id) selectedTopic.value = null;
    const { data } = await forumApi.getTopic(id);
    if (!data) return;

    const { resource: topic } = data;
    selectedTopic.value = topic;
    await trySelectBoard(topic.board.id);
  }

  const comments = ref<ListEnvelope<Comment> | null>(null);
  async function fetchComments(number: number) {
    comments.value = null;
    if (!selectedTopic.value) return;

    const size = currentUser.value?.settings?.pagingLimits?.commentsPerPage;
    const { data } = await forumApi.getComments(selectedTopic.value.id!, {
      number,
      size,
    });
    comments.value = data;
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
      comments.value.resources.push(data.resource);
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
        comments.value.resources[index] = data.resource;
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
          likes: [...existingLikes, data.resource] as Comment["likes"],
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
              (u) => u.login !== currentUser.value?.login,
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
        likes: [...existingLikes, data.resource] as Topic["likes"],
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
            (u) => u.login !== currentUser.value?.login,
          ) as Topic["likes"],
        };
      }
    }
  }

  return {
    boards,
    fetchBoards,
    selectedBoard,
    trySelectBoard,
    moderators,
    fetchModerators,
    attachedTopics,
    topics,
    fetchTopics,
    news,
    fetchNews,
    trySelectTopic,
    selectedTopic,
    fetchComments,
    comments,
    createComment,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
    likeTopic,
    unlikeTopic,
  };
});
