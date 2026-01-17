import type { User } from "@/api/models/community";
import type { Id, Served } from "@/api/models";

export type BoardId = Id<string>;
export type TopicId = Id<string>;
export type CommentId = Id<string>;

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

export type Comment = {
  id: Served<CommentId>;
  author: Served<User>;
  createdUtc: Served<string>;
  updatedUtc: Served<string | null>;
  text: string;
  isRemoved: Served<boolean>;
  likes: Served<User[]>;
};
