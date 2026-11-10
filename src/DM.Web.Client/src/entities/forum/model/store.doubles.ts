// The useBoardsStore specs hand these to vi.mock and read the mocks back.
import { vi } from "vitest";
import { defineStore } from "pinia";
import { computed, ref } from "vue";
import type { User } from "@/shared/api/models/common";
import { createMockUser } from "./store.fixtures";

export const mockGetBoards = vi.fn();
export const mockGetBoard = vi.fn();

export const mockGetTopics = vi.fn();
export const mockGetTopic = vi.fn();
export const mockGetComments = vi.fn();
export const mockCreateComment = vi.fn();
export const mockUpdateComment = vi.fn();
export const mockDeleteComment = vi.fn();
export const mockPostCommentLike = vi.fn();
export const mockDeleteCommentLike = vi.fn();
export const mockPostTopicLike = vi.fn();
export const mockDeleteTopicLike = vi.fn();
export const mockGetNews = vi.fn();
export const mockUpdateTopic = vi.fn();
export const mockReorderPinnedTopics = vi.fn();

export const forumApi = () => ({
  default: {
    getBoards: mockGetBoards,
    getBoard: mockGetBoard,

    getTopics: mockGetTopics,
    getTopic: mockGetTopic,
    getComments: mockGetComments,
    createComment: mockCreateComment,
    updateComment: mockUpdateComment,
    deleteComment: mockDeleteComment,
    postCommentLike: mockPostCommentLike,
    deleteCommentLike: mockDeleteCommentLike,
    postTopicLike: mockPostTopicLike,
    deleteTopicLike: mockDeleteTopicLike,
    getNews: mockGetNews,
    updateTopic: mockUpdateTopic,
    reorderPinnedTopics: mockReorderPinnedTopics,
  },
});

// A real store, so storeToRefs runs; it stands for the leaf module usePaging imports.
export const authStore = () => ({
  useAuthStore: defineStore("auth", () => {
    const user = ref<User | null>({
      ...createMockUser("user-1", "testuser"),
      settings: { pageSize: 20 },
    });
    return {
      user,
      isAuthenticated: computed(() => user.value !== null),
      updateUser: (newUser: User | null) => {
        user.value = newUser;
      },
    };
  }),
});
