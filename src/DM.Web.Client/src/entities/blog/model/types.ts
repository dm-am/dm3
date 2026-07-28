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
 * Lightweight blog reference for sidebars and menus.
 * Omits rubrics and other detail-page-only fields.
 * Request with ?projection=ref to get this type.
 */
export interface BlogRef {
  id: BlogId;
  /** Short public identifier for URLs (5 lowercase letters), like games. */
  publicId?: string;
  title: string;
  author: UserRef;
  assistants?: UserRef[];
  /** Blog status (Draft, Active, Closed) */
  status: BlogStatus;
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

/** Visibility of draft content */
export enum DraftVisibility {
  /** Only users with roles can view */
  Private = "Private",
  /** Preview visible to everyone */
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
  commentsEnabled: boolean;
  publicationCount: number;
  commentsCount: number;
  rubrics?: Rubric[];

  // Used only at creation time
  copyBlacklist?: boolean;
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
  author: UserRef;
  title: string;
  /** Pre-rendered HTML (server-side BBCode rendering). */
  content: string;
  /** Short excerpt (max 500 chars on the server). */
  preview: string;
  createdUtc: string;
  modifiedUtc?: string | null;
  isPublished: boolean;
  publishedUtc?: string | null;
  commentsEnabled: boolean;
  viewCount: number;
  commentCount: number;
  likes: UserRef[];
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
  /** Default: Public */
  draftVisibility?: DraftVisibility;
  /** Default: true */
  commentsEnabled?: boolean;
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
  commentsEnabled?: boolean;
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
  commentsEnabled?: boolean;
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
  commentsEnabled?: boolean;
}

// === Blog notepad (mirrors the game NotepadEntry shape — both are served
// by the shared NotepadEntryResponse API DTO) ===

export interface BlogNotepadEntry {
  id: string;
  containerId: string;
  categoryId?: string | null;
  title: string;
  content: string;
  sortOrder: number;
  createdUtc: string;
  modifiedUtc?: string | null;
}

/** Payload for creating a blog notepad entry */
export interface CreateBlogNotepadEntryInput {
  categoryId?: string | null;
  title: string;
  content: string;
}

/** Payload for updating a blog notepad entry */
export interface UpdateBlogNotepadEntryInput {
  categoryId?: string | null;
  title: string;
  content: string;
  sortOrder?: number | null;
}

// === Blog state-machine transitions (mirror the game enums) ===

/**
 * Requested blog status transition. Mirrors GameStatusTransition; blogs have
 * no Freeze/Finish because ModuleStatus for blogs is Draft/Active/Closed
 * without a closed reason.
 *
 * NOTE: the backend endpoint POST v1/blogs/{id}/status does NOT exist yet
 * (games have POST v1/games/{id}/status). The UI dispatches these optimistically
 * and surfaces a toast error until the endpoint lands.
 */
export enum BlogStatusTransition {
  /** Draft -> Active */
  Start = "Start",
  /** Active -> Closed */
  Close = "Close",
  /** Closed -> Active */
  Reopen = "Reopen",
}

/**
 * Requested premoderation transition (mentor action).
 * @see src/DM.Domain.Blog/Features/Blogs/BlogPremoderationTransition.cs
 */
export enum BlogPremoderationTransition {
  /** AwaitingEdits -> AwaitingApproval (take into premoderation) */
  SendToPremoderation = "SendToPremoderation",
  /** AwaitingApproval -> Approved (release) */
  RemoveFromPremoderation = "RemoveFromPremoderation",
}
