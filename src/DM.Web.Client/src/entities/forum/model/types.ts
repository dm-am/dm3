import type { User, UserRef, CommentId } from "@/shared/api/models/common";
import type { Served } from "@/shared/api/models";

export type { Comment, CommentId } from "@/shared/api/models/common";

export type BoardId = Served<string>;
export type TopicId = Served<string>;

type BoardLastComment = {
  id: CommentId;
  topicId: TopicId;
  topicTitle: string;
  topicNumber: number;
  createdUtc: string;
  author: User | null;
};

type BoardLastTopic = {
  id: TopicId;
  topicNumber: number;
  title: string;
  createdUtc: string;
  author: User | null;
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
  /** On the read of one board and the board of a topic; lists leave it out. */
  viewer?: Served<BoardViewer>;
};

/** Each flag is asked by the code that refuses the write it stands for. */
type BoardViewer = {
  canCreateTopic: boolean;
  /** A guest on a board where an ordinary member may open topics. */
  canCreateTopicAfterSignIn: boolean;
};

type LastComment = {
  id: CommentId;
  createdUtc: string;
  author: User | null;
};

export type Topic = {
  id: Served<TopicId>;
  topicNumber: Served<number>;
  /** Null when the author's account was removed. */
  author: Served<User | null>;
  createdUtc: Served<string>;
  modifiedUtc: Served<string | null>;
  title: string;
  description: string;
  isAttached: boolean;
  attachOrder: Served<number | null>;
  isClosed: boolean;
  /** Only a topic its author closed may be reopened by the author. */
  closedByAuthor: Served<boolean>;
  lastActivityUtc: Served<string>;
  lastComment: Served<LastComment | null>;
  commentsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  board: Board;
  likes: Served<User[]>;
  /** Served with the list, so the table needs no full likes of each topic. */
  likesCount: Served<number>;
  /** The closed statistics period an automatic digest topic sums up. */
  periodDigest?: PeriodDigestRef | null;
  /** On the read of one topic and the writes that return one; lists leave it out. */
  viewer?: Served<TopicViewer>;
};

/** Each flag is asked by the code that refuses the write it stands for. */
type TopicViewer = {
  canComment: boolean;
};

type PeriodDigestRef = {
  year: number;
  /** Null for a yearly digest. */
  month?: number | null;
};

/** `commentId` is null when the topic has no comments at all. */
export type FirstUnreadComment = {
  commentId: string | null;
  /** 1-based, for paging. */
  commentNumber: number;
};

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

export type { CommentsQuery } from "@/shared/api";
