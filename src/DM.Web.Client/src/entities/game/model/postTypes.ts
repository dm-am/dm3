import type { UserRef } from "@/shared/api/models/common";
import type { UploadAddressee } from "@/shared/api/models/common/upload";
import type { Served } from "@/shared/api/models";
import type { Character } from "./characterTypes";
import type { DiceRoll, DiceRollInput } from "./diceTypes";
import type { Room } from "./roomTypes";
import type { ReviewSignName } from "@/shared/api/models/game/reviews";

interface PostEditInfo {
  id: string;
  modifiedUtc: string;
  /** Null when the editor's account was removed. */
  editor?: UserRef | null;
}

type PostId = Served<string>;
export type Post = {
  id: Served<PostId>;
  room?: Room;
  character?: Character;
  /** Null when the author's account was removed: the post stays, unsigned. */
  author?: UserRef | null;
  /** Absent for a player's post. */
  authorGameRole?: "DungeonMaster" | "Assistant";
  createdUtc: string;
  /** Edit history (most recent first); presence marks an edited post. */
  edits?: PostEditInfo[];
  /** BBCode rendered by the server to HTML. */
  gameText: string;
  metagameText?: string;
  diceRolls?: DiceRoll[];
  rating?: number;
  reviewCount?: number;
  /** Oldest first. */
  attachments?: PostAttachment[];
  /** The place in the room under any filter; the rated feed leaves it out. */
  number?: number;
  /** A review closes the game's other posts to the reviewer for three days. */
  gameId?: Served<string>;
  viewer?: Served<PostViewer>;
};

/** The flag is asked by the code that refuses the review; a newbie is offered "=". */
interface PostViewer {
  canReview: boolean;
  /** The author, and moderation or the leads only when they read every hidden block of the post. */
  canEdit: boolean;
}

/** `url` is an endpoint that checks access on every request, not a bucket link. */
export interface PostAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  width?: number | null;
  height?: number | null;
  url: string;
  createdUtc: string;
  /** "Получатели:" of a file kept to the characters chosen, leads first; absent on a file every reader sees. */
  recipients?: string;
  /** The characters chosen, for the author's checkboxes, one gone from the room among them; absent when recipients is. */
  addressees?: UploadAddressee[];
}

/** One per post and reviewer. */
export interface PostReview {
  id: string;
  postId: string;
  gameId: string;
  /** Null when the reviewer's account was removed. */
  author: UserRef | null;
  /** Null when the account that wrote the post was removed. */
  postAuthor: UserRef | null;
  text: string;
  sign: ReviewSignName;
  createdUtc: string;
  modifiedUtc?: string;
}

export interface FirstUnreadPostResult {
  roomId: string;
  /** 1-based. */
  postNumber: number;
  postId: string;
  totalUnreadCount: number;
  /** For a guest, whether there are any posts. */
  hasUnread: boolean;
}

/** The last post when nothing is unread, the first one for a guest. */
export interface FirstUnreadRoomPostResult {
  /** Absent for a room without posts. */
  postId?: string;
  /** 0 for a room without posts. */
  postNumber: number;
  /** At `pageSize`. */
  page: number;
  pageSize: number;
  totalUnreadCount: number;
  hasUnread: boolean;
}

/** The rated posts the posts endpoint is asked for. */
export type RatedPostsQuery = {
  sortBy?: "rating" | "lastreview" | "reviewcount" | "created";
  sortOrder?: "asc" | "desc";
  hasReviews?: boolean;
  lastReviewedFromUtc?: string;
  search?: string;
  minRating?: number;
  maxRating?: number;
  authorUsernames?: string[];
  /** Posts with at least one review by this user. */
  reviewerUsername?: string;
  createdFromUtc?: string;
  createdToUtc?: string;
  gameId?: string;
  take?: number;
  skip?: number;
  number?: number;
};

/** The pulse filters under the pulse names, plus the characters. */
export interface RoomPostsFilter {
  search?: string;
  /** The server's default is created, the order the room is written in. */
  sortBy?: "created" | "rating" | "reviewcount" | "lastreview";
  sortOrder?: "asc" | "desc";
  authorUsernames?: string[];
  characterIds?: string[];
  /** Alone: only posts without a character; with characterIds: those too. */
  withoutCharacter?: boolean;
  minRating?: number;
  maxRating?: number;
  /** False: only posts without reviews. */
  hasReviews?: boolean;
  createdFromUtc?: string;
  createdToUtc?: string;
}

export interface FirstUnreadCommentResult {
  /** 1-based. */
  commentNumber: number;
  commentId: string;
  totalUnreadCount: number;
  /** For a guest, whether there are any comments. */
  hasUnread: boolean;
}

export interface CreatePostInput {
  /** Left out for the master's and an assistant's post. */
  characterId?: string;
  gameText: string;
  metagameText?: string;
  diceRolls?: DiceRollInput[];
}

/** The character stays as it is. */
export interface UpdatePostInput {
  gameText: string;
  /** Absent keeps the stored one, an empty string erases it. */
  metagameText?: string;
}
