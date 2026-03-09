import type { User } from "@/shared/api/models/common";

export type BlogId = string & { readonly __brand: unique symbol };
export type PublicationId = string & { readonly __brand: unique symbol };

/** Visibility of draft content */
export enum DraftVisibility {
  /** Only users with roles can view */
  Private = "Private",
  /** Preview visible to everyone */
  Public = "Public"
}

export interface Blog {
  id: BlogId;
  owner: User;
  title: string;
  description?: string;
  createdAt: string;
  updatedAt?: string;
  draftVisibility: DraftVisibility;
  commentsEnabled: boolean;
  publicationCount: number;
  commentsCount: number;
  subscribersCount: number;
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
  user: User;
  role: "owner" | "mentor" | "assistant" | "reader";
  joinedUtc: string;
}

export interface BlogInvitation {
  id: string;
  blogId: BlogId;
  blogTitle: string;
  invitedUser: User;
  inviterUsername: string;
  type: "assistant" | "reader";
  createdUtc: string;
  expiresUtc: string;
}
