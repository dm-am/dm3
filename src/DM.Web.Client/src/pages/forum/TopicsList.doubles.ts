/** The forum API client of the TopicsList specs and of TopicCreateForm.spec.ts, handed to vi.mock. */
import { vi } from "vitest";

export const mockGetTopics = vi.fn();
export const mockUpdateTopic = vi.fn();
export const mockReorderPinnedTopics = vi.fn();
export const mockCreateTopic = vi.fn();
export const mockGetBoard = vi.fn();

/** The module @/entities/forum/api/forumApi stands for. */
export const forumApi = () => ({
  default: {
    getTopics: mockGetTopics,
    updateTopic: mockUpdateTopic,
    reorderPinnedTopics: mockReorderPinnedTopics,
    createTopic: mockCreateTopic,
    getBoard: mockGetBoard,
    getBoards: vi.fn(),
    getNews: vi.fn(),
  },
});
