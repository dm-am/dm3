import { vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import type {
  Board,
  Topic,
  Comment,
  BoardId,
  TopicId,
  CommentId,
} from "./types";
import type {
  ListEnvelope,
  PagingInfo,
  User,
} from "@/shared/api/models/common";
import { asServed } from "@/shared/api/models";

/** A list as the API serves it. */
export const listOf = <T>(
  resources: T[],
  paging: PagingInfo | null = null,
): ListEnvelope<T> => ({ resources, paging });

/** One page holding every row of a list of `total`. */
export const pageOf = (total: number): PagingInfo => ({
  current: 1,
  pages: 1,
  skip: 0,
  take: 20,
  total,
});

export const createMockUser = (id: string, username: string): User =>
  ({
    id,
    username,
    role: "RegularUser",
    isOnline: false,
    lastActivityUtc: "2024-01-01T00:00:00Z",
  }) as unknown as User;

export const createMockBoard = (id: string, title: string): Board =>
  ({
    id: asServed(id as BoardId),
    title: asServed(title),
    alias: asServed(title.toLowerCase().replace(/\s+/g, "-")),
    description: asServed(`Description of ${title}`),
    moderators: asServed([]),
    topicsCount: asServed(10),
    commentsCount: asServed(50),
    unreadTopicsCount: asServed(0),
    unreadCommentsCount: asServed(0),
    lastComment: asServed(null),
    lastTopic: asServed(null),
  }) as Board;

export const createMockTopic = (
  id: string,
  title: string,
  boardId: string,
  topicNumber: number = 1,
): Topic =>
  ({
    id: asServed(id as TopicId),
    topicNumber: asServed(topicNumber),
    author: asServed(createMockUser("user-1", "author")),
    createdUtc: asServed("2024-01-01T00:00:00Z"),
    modifiedUtc: asServed(null),
    title,
    description: "Topic content",
    isAttached: false,
    attachOrder: asServed(null),
    isClosed: false,
    closedByAuthor: asServed(false),
    lastActivityUtc: asServed("2024-01-01T00:00:00Z"),
    lastComment: asServed(null),
    commentsCount: asServed(5),
    unreadCommentsCount: asServed(0),
    board: createMockBoard(boardId, "Board"),
    likes: asServed([]),
    likesCount: asServed(0),
  }) as Topic;

export const createMockComment = (id: string, text: string): Comment =>
  ({
    id: asServed(id as CommentId),
    text,
    author: asServed(createMockUser("user-1", "author")),
    createdUtc: asServed("2024-01-01T00:00:00Z"),
    modifiedUtc: asServed(null),
    likes: asServed([]),
    isRemoved: asServed(false),
  }) as Comment;

/** A store of its own for the test, over mocks that have not been called. */
export function freshStore() {
  setActivePinia(createPinia());
  vi.clearAllMocks();
}
