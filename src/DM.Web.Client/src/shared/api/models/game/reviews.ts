import type { Id, Served } from "../common";
import type { User } from "../community";

/**
 * Game review types
 * @module shared/api/models/game/reviews
 *
 * Game reviews ("рецензия") are detailed reviews on entire games.
 * - BBCode supported
 * - NO likes support
 * - One review per game per user
 *
 * @see src/DM.Web.API/Features/Game/Reviews/GameReviewDtos.cs
 */

export type GameReviewId = Id<string>;

/**
 * Game review DTO
 * Detailed review of an entire game
 */
export type GameReview = {
  /** Review unique identifier */
  id: Served<GameReviewId>;
  /** Game identifier */
  gameId: Served<string>;
  /** Title of the reviewed game — what names the row on the profile listings */
  gameTitle?: Served<string>;
  /** Review author */
  author: Served<User>;
  /** Review text (BBCode supported) */
  text: string;
  /** Creation timestamp (UTC) */
  createdUtc: Served<string>;
  /** Last modification timestamp (UTC) */
  modifiedUtc?: Served<string>;
};

/**
 * post review types
 * @module shared/api/models/game/reviews
 *
 * PostReviews ("оценка поста") are ratings with required text for individual posts.
 * - BBCode supported
 * - One review per post per user
 * - Has rating sign (Positive, Neutral, Negative)
 *
 * @see src/DM.Web.API/Features/Game/Reviews/PostReviewDtos.cs
 */

export type PostReviewId = Id<string>;

/**
 * Review rating sign/sentiment
 * @see src/DM.Domain.Core/Enums/ReviewSign.cs
 */
export enum ReviewSign {
  /** Negative review (user did not like the post) */
  Negative = -1,
  /** Neutral review (comment without rating impact) */
  Neutral = 0,
  /** Positive review (user liked the post) */
  Positive = 1,
}

/**
 * post review DTO
 * Rating with optional comment for a post
 */
export type PostReview = {
  /** Review unique identifier */
  id: Served<PostReviewId>;
  /** Post identifier being reviewed */
  postId: Served<string>;
  /** Game identifier (denormalized) */
  gameId: Served<string>;
  /** Review author */
  author: Served<User>;
  /** Post author (recipient of the review) */
  postAuthor: Served<User>;
  /** Review text (BBCode supported, required) */
  text: string;
  /** Rating sign (+1, 0, -1) */
  sign: ReviewSign;
  /** Creation timestamp (UTC) */
  createdUtc: Served<string>;
  /** Last modification timestamp (UTC) */
  modifiedUtc?: Served<string>;
};
