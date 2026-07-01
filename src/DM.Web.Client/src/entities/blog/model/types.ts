import type { User, UserRef } from "@/shared/api/models/common";

export type BlogId = string & { readonly __brand: unique symbol };
export type PublicationId = string & { readonly __brand: unique symbol };

/** Blog status (same as game status) */
export type BlogStatus = "Draft" | "Active" | "Closed";

/**
 * Lightweight blog reference for sidebars and menus.
 * Omits rubrics and other detail-page-only fields.
 * Request with ?projection=ref to get this type.
 */
export interface BlogRef {
  id: BlogId;
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
}

export interface BlogUser {
  user: UserRef;
  role: "author" | "mentor" | "assistant" | "reader";
  joinedUtc: string;
}

export interface BlogInvitation {
  id: string;
  blogId: BlogId;
  blogTitle: string;
  invitedUser: UserRef;
  inviterUsername: string;
  type: "assistant" | "reader";
  createdUtc: string;
  expiresUtc: string;
}

/**
 * Blog publication (post) — slim DTO shape used by lists and widgets.
 * Mirrors `DM.Web.API.Features.Blog.Publications.Publication`.
 */
export interface Publication {
  id: PublicationId;
  blogId: BlogId;
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
