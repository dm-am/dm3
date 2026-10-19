import type { UserRef } from "@/shared/api/models/common";
import type { Blog, BlogRef } from "./types";

export const createMockBlogRef = (id: string, title: string): BlogRef =>
  ({
    id,
    title,
    author: { id: "user-1", username: "author" } as UserRef,
    status: "Active",
    createdUtc: "2024-01-01",
    subscribersCount: 0,
  }) as BlogRef;

export const createMockBlog = (id: string, title: string): Blog =>
  ({
    id,
    title,
    author: { id: "user-1", username: "author" } as UserRef,
    status: "Active",
    createdUtc: "2024-01-01",
    subscribersCount: 0,
    draftVisibility: "Private",
    publicationCount: 0,
    commentsCount: 0,
  }) as Blog;
