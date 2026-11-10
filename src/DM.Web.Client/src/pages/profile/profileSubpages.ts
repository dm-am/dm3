/** Route names of the profile's list subpages, each a row of ProfileSubpage. */
export type ProfileSubpageName =
  | "received-reviews"
  | "given-reviews"
  | "received-endorsements"
  | "given-endorsements"
  | "received-game-reviews"
  | "given-game-reviews";

export type EndorsementsSubpageName = Extract<
  ProfileSubpageName,
  `${string}-endorsements`
>;

export type GameReviewsSubpageName = Extract<
  ProfileSubpageName,
  `${string}-game-reviews`
>;

/** Which side of a testimonial a profile list shows: about the user or by him. */
export type TestimonialSide = "received" | "written";
