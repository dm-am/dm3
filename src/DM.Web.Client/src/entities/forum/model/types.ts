import type {
  User,
  UserRef,
  Comment as BaseComment,
  CommentId as BaseCommentId,
} from "@/shared/api/models/common";
import type { Id, Served } from "@/shared/api/models";

export type BoardId = Id<string>;
export type TopicId = Id<string>;

// Re-export Comment types from shared for backwards compatibility
export type CommentId = BaseCommentId;
export type Comment = BaseComment;

export type BoardLastComment = {
  id: CommentId;
  topicId: TopicId;
  topicTitle: string;
  topicNumber: number;
  createdUtc: string;
  author: User;
};

export type BoardLastTopic = {
  id: TopicId;
  topicNumber: number;
  title: string;
  createdUtc: string;
  author: User;
};

export type Board = {
  id: Served<BoardId>;
  title: Served<string>;
  alias: Served<string>;
  description: Served<string>;
  moderators: Served<UserRef[]>;
  topicsCount: Served<number>;
  commentsCount: Served<number>;
  unreadTopicsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  lastComment: Served<BoardLastComment | null>;
  lastTopic: Served<BoardLastTopic | null>;
};

export type LastComment = {
  id: CommentId;
  createdUtc: string;
  author: User;
};

export type Topic = {
  id: Served<TopicId>;
  topicNumber: Served<number>;
  author: Served<User>;
  createdUtc: Served<string>;
  modifiedUtc: Served<string | null>;
  title: string;
  description: string;
  isAttached: boolean;
  attachOrder: Served<number | null>;
  isClosed: boolean;
  lastActivityUtc: Served<string>;
  lastComment: Served<LastComment | null>;
  commentsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  board: Board;
  likes: Served<User[]>;
};

// Query parameters for topics list
export type TopicsQuery = {
  number?: number;
  size?: number;
  isAttached?: boolean;
  search?: string;
  authors?: string[];
  createdFromUtc?: string;
  createdToUtc?: string;
  sortBy?: string;
  sortOrder?: string;
};

// Query parameters for comments list
export type CommentsQuery = {
  number?: number;
  size?: number;
  search?: string;
  authors?: string[];
  createdFromUtc?: string;
  createdToUtc?: string;
  sortBy?: string;
  sortOrder?: string;
};

// Comment type is now imported from shared and re-exported above
