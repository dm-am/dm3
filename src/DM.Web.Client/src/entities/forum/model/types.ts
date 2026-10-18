import type {
  User,
  UserRef,
  Comment as BaseComment,
  CommentId as BaseCommentId,
} from "@/shared/api/models/common";
import type { Id, Served } from "@/shared/api/models";

export type BoardId = Id<string>;
export type TopicId = Id<string>;

// The comment shape is one contract for the forum, the game and the blog, so it
// is declared in shared; the slice names it under its own roof.
export type CommentId = BaseCommentId;
export type Comment = BaseComment;

export type BoardLastComment = {
  id: CommentId;
  topicId: TopicId;
  topicTitle: string;
  topicNumber: number;
  createdUtc: string;
  // Null when the author account has been deleted (backend maps to null).
  author: User | null;
};

export type BoardLastTopic = {
  id: TopicId;
  topicNumber: number;
  title: string;
  createdUtc: string;
  // Null when the author account has been deleted (backend maps to null).
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
  /**
   * What the viewer may do on this board, answered by the code that refuses the
   * request. On the read of one board and on the board of a topic read alone;
   * the list of boards and the rows of a topic list leave it out.
   */
  viewer?: Served<BoardViewer>;
};

/**
 * What the viewer may do on a board (mirrors BoardViewer). The server asks each
 * flag with the code that refuses the write it stands for.
 */
export type BoardViewer = {
  /** May open a topic: the board's policy against the role, and no ban. False for a guest. */
  canCreateTopic: boolean;
  /**
   * Signing in is the way to a topic here: the viewer is a guest, and an
   * ordinary member may open topics on the board. False for anybody signed in,
   * and on a board whose topics come from the moderation (the news, the
   * contests).
   */
  canCreateTopicAfterSignIn: boolean;
};

export type LastComment = {
  id: CommentId;
  createdUtc: string;
  // Null when the author account has been deleted (backend maps to null).
  author: User | null;
};

export type Topic = {
  id: Served<TopicId>;
  topicNumber: Served<number>;
  // Null when the author account has been deleted (backend maps to null).
  author: Served<User | null>;
  createdUtc: Served<string>;
  modifiedUtc: Served<string | null>;
  title: string;
  description: string;
  isAttached: boolean;
  attachOrder: Served<number | null>;
  isClosed: boolean;
  /**
   * Whether the topic was closed by its author, who may then reopen it. A topic
   * closed by a board moderator or an administrator stays closed to its author;
   * false while the topic is open.
   */
  closedByAuthor: Served<boolean>;
  lastActivityUtc: Served<string>;
  lastComment: Served<LastComment | null>;
  commentsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  board: Board;
  likes: Served<User[]>;
  /**
   * Denormalised likes count — populated by the backend listing path so
   * the table can render the column without hydrating each topic's full
   * Likes list. Also drives the "sort by likes" column.
   */
  likesCount: Served<number>;
  /**
   * Set when the topic is an auto-created period digest ("Итоги …"): the
   * closed statistics period it summarizes. The client renders the
   * period's leaderboards inside the topic from the statistics API.
   */
  periodDigest?: PeriodDigestRef | null;
  /**
   * What the viewer may do under this topic, answered by the code that refuses
   * the request. On the read of one topic and the writes that return one, the
   * PATCH that closes it included; a list of topics leaves it out.
   */
  viewer?: Served<TopicViewer>;
};

/**
 * What the viewer may do under a topic (mirrors TopicViewer). The server asks
 * each flag with the code that refuses the write it stands for.
 */
export type TopicViewer = {
  /** May write a new comment: the topic open and no ban. False for a guest. */
  canComment: boolean;
};

/** Closed statistics period a digest topic summarizes */
export type PeriodDigestRef = {
  year: number;
  /** Calendar month 1-12; null for a yearly digest */
  month?: number | null;
};

/**
 * Where the reader continues in a topic: the first comment he has not read,
 * or the topic's last comment when everything is read. `commentId` is null
 * when the topic has no comments at all.
 */
export type FirstUnreadComment = {
  commentId: string | null;
  /** Position of that comment in the topic (1-based), for paging */
  commentNumber: number;
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

// Query parameters for comments list. Shared with the game and the blog: the
// four discussions read the same query, so it is declared once in shared/api.
export type { CommentsQuery } from "@/shared/api";

// Comment type is now imported from shared and re-exported above
