import type { Id, Served } from "@/api/models";
import type { User } from "@/api/models/community";

export type FeaturedPostId = Id<string>;

export interface FeaturedPost {
  id: Served<FeaturedPostId>;
  textPreview: string;
  author: User;
  characterName?: string;
  createdUtc: string;
  gameId: string;
  gameTitle: string;
  roomId: string;
  roomTitle: string;
  rating: number;
  reviewCount: number;
}

export interface FeaturedPostsEnvelope {
  bestOfWeek?: FeaturedPost;
  lastWithPlus?: FeaturedPost;
}

export enum ReviewSign {
  Positive = "Positive",
  Neutral = "Neutral",
  Negative = "Negative",
}

export interface PostReview {
  id: string;
  authorId: string;
  author?: User;
  targetType: string;
  targetId?: string;
  text?: string;
  sign?: ReviewSign;
  createdUtc: string;
}
