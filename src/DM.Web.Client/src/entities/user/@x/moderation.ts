/**
 * Cross-import surface for the moderation entity (FSD @x).
 *
 * entities/moderation decides whether the viewer may see a moderated profile at
 * all, and the site role that answers that question is a user attribute. Same-
 * layer entity imports are only allowed through an explicit @x public API — one
 * predicate is all this door carries.
 */
export { userIsModerator } from "..";
