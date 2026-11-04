import {
  type UserRef,
  ClosedReason,
  type PremoderationStatus,
  ModuleStatus,
} from "@/shared/api/models/common";

export const BlogStatus = ModuleStatus;
export type BlogStatus = ModuleStatus;

/** The blog as `?projection=ref` serves it, for sidebars and menus. */
export interface BlogRef {
  id: string;
  /** Five lowercase letters, for addresses. */
  publicId?: string;
  title: string;
  /** Null when the owner's account was removed: the blog stays, unsigned. */
  author: UserRef | null;
  assistants?: UserRef[];
  status: BlogStatus;
  /** Only carried while the status is Closed. */
  closedReason?: ClosedReason;
  createdUtc: string;
  activatedUtc?: string;
  closedUtc?: string;
  subscribersCount: number;
  /** The first five, for the tooltip. */
  subscriberUsernames?: string[];
  unreadPublicationsCount?: number;
  unreadCommentsCount?: number;
}

/** Who sees a blog while it is in the making. */
export enum DraftVisibility {
  /** Only users with roles. */
  Private = "Private",
  Public = "Public",
}

export interface Blog extends BlogRef {
  description?: string;
  mentor?: UserRef;
  premoderationStatus?: PremoderationStatus;
  draftVisibility: DraftVisibility;
  /** The comment form goes by `viewer.canComment`, which counts the blacklist too. */
  viewerBlacklisted?: boolean;
  /** On the read of one blog and the writes that return one; a list leaves it out. */
  viewer?: BlogViewer;
  publicationCount: number;
  commentsCount: number;
  rubrics?: Rubric[];
}

/** Each flag is asked by the code that refuses the write it stands for. */
interface BlogViewer {
  canComment: boolean;
}

/** Each flag is asked by the code that refuses the write it stands for. */
interface PublicationViewer {
  canComment: boolean;
}

export interface Rubric {
  id: string;
  title: string;
  sortOrder: number;
  /** Total publications in this rubric (supplementary total, not "(N/A)"). */
  publicationCount: number;
  /** Unread publications for the current user (the "N" in "(N/A)"). */
  unreadPublicationsCount: number;
  /** Unread comments across the rubric's publications (the "A" in "(N/A)"). */
  unreadCommentsCount: number;
}

export interface BlogUser {
  user: UserRef;
  role: "owner" | "mentor" | "assistant" | "reader";
  /** Null for mentors and readers. */
  joinedUtc?: string | null;
}

export interface BlogInvitation {
  id: string;
  blogId: string;
  blogTitle: string;
  invitedUser: UserRef;
  inviterUsername: string;
  type: "assistant" | "reader";
  createdUtc: string;
  expiresUtc?: string | null;
}

export interface Publication {
  id: string;
  blogId: string;
  blogTitle?: string;
  rubric?: Rubric | null;
  /** Null when the author's account was removed. */
  author: UserRef | null;
  title: string;
  /** HTML rendered by the server. */
  content: string;
  preview: string;
  createdUtc: string;
  modifiedUtc?: string | null;
  viewCount: number;
  commentCount: number;
  likes: UserRef[];
  /** On the read of one publication and the writes that return one; a feed leaves it out. */
  viewer?: PublicationViewer;
}

export interface CreateBlogInput {
  title: string;
  /** Raw BBCode. */
  description?: string;
  draftVisibility?: DraftVisibility;
  /** Copy the personal blacklist into the blog's. */
  copyBlacklist?: boolean;
}

/** An empty string leaves the field unchanged. */
export interface UpdateBlogInput {
  title?: string;
  description?: string;
  draftVisibility?: DraftVisibility;
}

export interface CreateRubricInput {
  title: string;
  sortOrder?: number;
}

export interface CreatePublicationInput {
  rubricId?: string | null;
  title: string;
  /** Raw BBCode. */
  content: string;
  preview?: string;
}

/** An empty string leaves the field unchanged. */
export interface UpdatePublicationInput {
  rubricId?: string | null;
  /** rubricId is ignored when true. */
  clearRubric?: boolean;
  title?: string;
  content?: string;
  preview?: string;
}

/** The game's transitions without Finish: a blog is not a story that ends. */
export enum BlogStatusTransition {
  /** Draft -> Active */
  Start = "Start",
  /** Active -> Closed (Frozen) */
  Freeze = "Freeze",
  /** Active or Closed+Frozen -> Closed (None) */
  Close = "Close",
  /** Closed (any reason) -> Active */
  Reopen = "Reopen",
}

/** The first two are the moderation verdict, the third the owner asking for it. */
export enum BlogPremoderationTransition {
  /** Any status -> Approved (verdict, clears the curator) */
  SetApproved = "SetApproved",
  /** Any status -> AwaitingEdits (verdict, records the acting mentor) */
  SetAwaitingEdits = "SetAwaitingEdits",
  /** AwaitingEdits -> AwaitingApproval (the owner asks for a verdict) */
  SubmitForApproval = "SubmitForApproval",
}
