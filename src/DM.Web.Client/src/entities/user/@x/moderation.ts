/**
 * Cross-import surface for the moderation entity (FSD @x).
 *
 * entities/moderation decides whether the viewer may see a moderated profile at
 * all, and the site role that answers that question is a user attribute. Same-
 * layer entity imports are only allowed through an explicit @x public API.
 *
 * The warning card names the person warned by the ordinary user link the rest
 * of the site draws, badge and all.
 *
 * An entry of the ban list names the banned login and the moderator who issued
 * or lifted the ban as links into their profiles, and an account that is gone
 * the way every place of the site names it, RemovedAccountLink.
 */
export { userIsModerator, UserLink, RemovedAccountLink } from "..";
