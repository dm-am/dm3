import type { User, Comment as BaseComment, CommentId as BaseCommentId } from "@/shared/api/models/common";
import type { Id, Served } from "@/shared/api/models";

export type BoardId = Id<string>;
export type TopicId = Id<string>;

// Re-export Comment types from shared for backwards compatibility
export type CommentId = BaseCommentId;
export type Comment = BaseComment;

export type BoardLastComment = {
  id: CommentId;
  topicId: TopicId;
  createdUtc: string;
  author: User;
};

export type Board = {
  id: Served<BoardId>;
  description: Served<string>;
  topicsCount: Served<number>;
  commentsCount: Served<number>;
  unreadTopicsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  lastComment: Served<BoardLastComment | null>;
};

export type LastComment = {
  createdUtc: string;
  author: User;
};

export type Topic = {
  id: Served<TopicId>;
  author: Served<User>;
  createdUtc: Served<string>;
  editedUtc: Served<string | null>;
  title: string;
  description: string;
  isAttached: boolean;
  isClosed: boolean;
  lastComment: Served<LastComment | null>;
  commentsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  board: Board;
  likes: Served<User[]>;
};

// Comment type is now imported from shared and re-exported above
