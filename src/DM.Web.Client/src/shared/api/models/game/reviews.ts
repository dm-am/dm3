import type { Served } from "../common";
import type { User } from "../community";

/**
 * Reviews of whole games ("рецензия"): BBCode, no likes, one per game per user.
 * @see src/DM.Web.API/Features/Game/Reviews/GameReviewDtos.cs
 */

type GameReviewId = Served<string>;

export type GameReview = {
  id: Served<GameReviewId>;
  gameId: Served<string>;
  /** Names the row on the profile listings. */
  gameTitle?: Served<string>;
  /** Null when the account was removed. */
  author: Served<User | null>;
  /** BBCode */
  text: string;
  createdUtc: Served<string>;
  modifiedUtc?: Served<string>;
  viewer: Served<GameReviewViewer>;
};

/** Each flag is asked by the code that refuses the write it stands for. */
type GameReviewViewer = {
  /** The author within a day of writing it, an admin author at any time. */
  canEdit: boolean;
  /** The author or senior moderation. */
  canDelete: boolean;
};

/**
 * The sign of a post review ("оценка поста").
 * @see src/DM.Domain.Core/Enums/ReviewSign.cs
 */
export enum ReviewSign {
  /** Negative review (user did not like the post) */
  Negative = -1,
  /** A comment without rating impact. */
  Neutral = 0,
  /** Positive review (user liked the post) */
  Positive = 1,
}

/** The sign as the wire spells it: the server writes an enum by its name. */
export type ReviewSignName = keyof typeof ReviewSign;
