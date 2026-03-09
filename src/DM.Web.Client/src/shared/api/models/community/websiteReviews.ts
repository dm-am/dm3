import type { Id, Served } from "../common";
import type { User } from "./index";

export type WebsiteReviewId = Id<string>;
export type WebsiteReview = {
  id: Served<WebsiteReviewId>;
  author: Served<User>;
  createdUtc: Served<string>;
  isApproved: boolean;
  text: string;
};
