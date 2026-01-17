import type { Id, Served } from "@/api/models";
import type { User } from "@/api/models/community/index";

export type WebsiteReviewId = Id<string>;
export type WebsiteReview = {
  id: Served<WebsiteReviewId>;
  author: Served<User>;
  createdUtc: Served<string>;
  isApproved: boolean;
  text: string;
};
