import type { UserRef } from "@/shared/api/models/common";
import { ModuleStatus } from "@/shared/api/models/common";

export type BlogId = string & { readonly __brand: unique symbol };
export type PublicationId = string & { readonly __brand: unique symbol };

/** Blog lifecycle status — an alias of the shared {@link ModuleStatus}. */
export const BlogStatus = ModuleStatus;
export type BlogStatus = ModuleStatus;

/**
 * Premoderation status for newbie blogs (mirrors backend PremoderationStatus).
 * "Approved" means the blog does not require premoderation and is hidden in UI.
 */
export type BlogPremoderationStatus =
  | "Approved"
  | "AwaitingApproval"
  | "AwaitingEdits";

/**
 * Why a closed blog is closed (mirrors the backend ClosedReason, which one
 * status machine serves both modules from). Declared here rather than imported
 * from the game entity, the same way BlogPremoderationStatus is: entities do
 * not reach into each other.
 */
export type BlogClosedReason = "None" | "Finished" | "Frozen";

/**
 * Lightweight blog reference for sidebars and menus.
 * Omits rubrics and other detail-page-only fields.
 * Request with ?projection=ref to get this type.
 */
export interface BlogRef {
  id: BlogId;
  /** Short public identifier for URLs (5 lowercase letters), like games. */
  publicId?: string;
  title: string;
  /** Null when the owner's account was removed: the blog stays, unsigned. */
  author: UserRef | null;
  assistants?: UserRef[];
  /** Blog status (Draft, Active, Closed) */
  status: BlogStatus;
  /** Why the blog is closed; only carried while the status is Closed. */
  closedReason?: BlogClosedReason;
  /** When the blog was created */
  createdUtc: string;
  /** When the blog was first activated */
  activatedUtc?: string;
  /** When the blog was closed */
  closedUtc?: string;
  subscribersCount: number;
  /** Subscriber usernames for tooltip (first 5) */
  subscriberUsernames?: string[];
  unreadPublicationsCount?: number;
  unreadCommentsCount?: number;
}

/** Who sees a blog while it is in the making ("оформляющийся блог") */
export enum DraftVisibility {
  /** Only users with roles can view */
  Private = "Private",
  /** Visible to everyone */
  Public = "Public",
}

/**
 * Full blog DTO for lists and cards.
 * Extends BlogRef with additional fields.
 *
 * Inherits from BlogRef:
 * - id, title, author, assistants, status, createdUtc, activatedUtc, closedUtc
 * - subscribersCount, unreadPublicationsCount, unreadCommentsCount
 */
export interface Blog extends BlogRef {
  // Inherited from BlogRef:
  // id, title, author, assistants, status, createdUtc, activatedUtc, closedUtc,
  // subscribersCount, unreadPublicationsCount, unreadCommentsCount

  description?: string;
  /** Assigned blog mentor ("наставник"), if any. Full Blog projection only. */
  mentor?: UserRef;
  /**
   * Premoderation status (newbie blogs). Hidden in UI when "Approved" (the
   * blog does not require premoderation) — doc 4.2.2.15.
   */
  premoderationStatus?: BlogPremoderationStatus;
  draftVisibility: DraftVisibility;
  /**
   * The viewer is on the blacklist of this blog: they may not comment on the
   * blog or under its publications, nor edit a comment of theirs there;
   * everything else is open to them as to anyone. False for a guest. The
   * comment form goes by `viewer.canComment`, which says the blacklist
   * together with every other reason.
   */
  viewerBlacklisted?: boolean;
  /**
   * What the viewer may do in this blog, answered by the code that refuses the
   * request. On the read of one blog and the writes that return one; a list
   * of blogs leaves it out.
   */
  viewer?: BlogViewer;
  publicationCount: number;
  commentsCount: number;
  rubrics?: Rubric[];
}

/**
 * What the viewer may do in a blog (mirrors BlogViewer). The server asks each
 * flag with the code that refuses the write it stands for.
 */
export interface BlogViewer {
  /**
   * May write a new comment in the blog's discussion: the blog in the making
   * open to the viewer, the blacklist and the ban at once. False for a guest.
   */
  canComment: boolean;
}

/**
 * What the viewer may do under a publication (mirrors PublicationViewer). The
 * server asks each flag with the code that refuses the write it stands for.
 */
export interface PublicationViewer {
  /**
   * May write a new comment under the publication: published, the blog's
   * blacklist and the ban at once. False for a guest.
   */
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

/**
 * Blog member with role.
 * Mirrors `DM.Web.API.Features.Blog.Users.BlogUser` (role strings are
 * lowercase: owner / assistant / mentor / reader).
 */
export interface BlogUser {
  user: UserRef;
  role: "owner" | "mentor" | "assistant" | "reader";
  /** Null when the join date is unavailable (mentors, readers) */
  joinedUtc?: string | null;
}

/** Mirrors `DM.Web.API.Features.Blog.Invitations.BlogInvitation`. */
export interface BlogInvitation {
  id: string;
  blogId: BlogId;
  blogTitle: string;
  invitedUser: UserRef;
  inviterUsername: string;
  type: "assistant" | "reader";
  createdUtc: string;
  expiresUtc?: string | null;
}

/**
 * Blog publication (post) — slim DTO shape used by lists and widgets.
 * Mirrors `DM.Web.API.Features.Blog.Publications.Publication`.
 */
export interface Publication {
  id: PublicationId;
  blogId: BlogId;
  /** Parent blog title (so cards can show the blog name, not its id). */
  blogTitle?: string;
  rubric?: Rubric | null;
  /** Null when the author's account was removed. */
  author: UserRef | null;
  title: string;
  /** Pre-rendered HTML (server-side BBCode rendering). */
  content: string;
  /** Short excerpt (max 500 chars on the server). */
  preview: string;
  createdUtc: string;
  modifiedUtc?: string | null;
  isPublished: boolean;
  publishedUtc?: string | null;
  viewCount: number;
  commentCount: number;
  likes: UserRef[];
  /**
   * What the viewer may do under this publication, answered by the code that
   * refuses the request. On the read of one publication and the writes that
   * return one; a feed leaves it out.
   */
  viewer?: PublicationViewer;
}

// === Blog mutations (mirror the game entity's input types) ===

/**
 * Payload for POST v1/blogs (maps to backend CreateBlogRequest).
 * @see src/DM.Web.API/Features/Blog/Blogs/Blog.cs
 */
export interface CreateBlogInput {
  /** Blog title (1-200 characters, required) */
  title: string;
  /** Blog description (raw BBCode source, max 5000 characters) */
  description?: string;
  /** Who sees the blog while it is in the making. Default: Public */
  draftVisibility?: DraftVisibility;
  /** Copy the personal blacklist into the blog blacklist on creation */
  copyBlacklist?: boolean;
}

/**
 * Payload for PATCH v1/blogs/{id}
 * (maps to backend UpdateBlogRequest — empty string = not changed).
 * @see src/DM.Web.API/Features/Blog/Blogs/Blog.cs
 */
export interface UpdateBlogInput {
  title?: string;
  /** Blog description (raw BBCode source) */
  description?: string;
  draftVisibility?: DraftVisibility;
}

/**
 * Payload for POST v1/blogs/{id}/rubrics (maps to CreateRubricRequest).
 * @see src/DM.Web.API/Features/Blog/Blogs/Blog.cs
 */
export interface CreateRubricInput {
  title: string;
  sortOrder?: number;
}

/**
 * Payload for POST v1/blogs/{blogId}/publications
 * (maps to CreatePublicationRequest).
 * @see src/DM.Web.API/Features/Blog/Publications/Publication.cs
 */
export interface CreatePublicationInput {
  rubricId?: string | null;
  title: string;
  /** Publication content (raw BBCode source) */
  content: string;
  /** Short excerpt (max 500 chars) */
  preview?: string;
  /** Publish immediately instead of saving a draft */
  publishImmediately?: boolean;
}

/**
 * Payload for PATCH v1/publications/{id}
 * (maps to UpdatePublicationRequest — empty string = not changed).
 * @see src/DM.Web.API/Features/Blog/Publications/Publication.cs
 */
export interface UpdatePublicationInput {
  rubricId?: string | null;
  /** Remove the rubric (rubricId is ignored when true) */
  clearRubric?: boolean;
  title?: string;
  /** Publication content (raw BBCode source; empty = keep current) */
  content?: string;
  preview?: string;
  isPublished?: boolean;
}

// === Blog notepad ===
// The entry shape is not declared here: all three notepads (personal, game,
// blog) are served by one API DTO and are declared once in
// shared/api/models/notepads.

// === Blog state-machine transitions (mirror the game enums) ===

/**
 * Requested blog status transition. One machine serves both modules
 * (ModuleStatusTransition.cs), so the member names are the game's; Finish is
 * left out because a blog is not a story that ends.
 *
 * Served by POST v1/blogs/{id}/status (BlogController.PostBlogStatus).
 */
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

/**
 * Requested premoderation transition. Exactly three, and they do not share an
 * actor: the first two are the moderation verdict (Mentor and above, legal from
 * any status), the third is the owner asking for that verdict.
 * @see src/DM.Domain.Core/Statuses/ModuleStatusTransition.cs
 */
export enum BlogPremoderationTransition {
  /** Any status -> Approved (verdict, clears the curator) */
  SetApproved = "SetApproved",
  /** Any status -> AwaitingEdits (verdict, records the acting mentor) */
  SetAwaitingEdits = "SetAwaitingEdits",
  /** AwaitingEdits -> AwaitingApproval (the owner asks for a verdict) */
  SubmitForApproval = "SubmitForApproval",
}
