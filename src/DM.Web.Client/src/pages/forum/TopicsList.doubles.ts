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

/**
 * The module vue-router stands for, under TopicCreateForm.spec.ts alone: the
 * TopicsList specs read the real router (openBoard of TopicsList.fixtures.ts).
 */
export const vueRouter = () => ({
  useRoute: () => ({ params: { alias: "main" }, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
});
