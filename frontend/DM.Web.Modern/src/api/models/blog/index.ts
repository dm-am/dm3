import type { User } from "@/api/models/community";

export type BlogId = string & { readonly __brand: unique symbol };

export interface Blog {
  id: BlogId;
  owner: User;
  title: string;
  description?: string;
  createdAt: string;
  updatedAt?: string;
  isPublic: boolean;
  commentsEnabled: boolean;
  publicationCount: number;
  commentsCount: number;
  subscribersCount: number;
  rubrics?: Rubric[];
}

export interface Rubric {
  id: string;
  title: string;
  sortOrder: number;
}
