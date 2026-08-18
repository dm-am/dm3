/**
 * Cross-import surface for the game entity (FSD @x).
 *
 * entities/game renders author links, and UserLink is the one symbol this
 * slice owns. It also has to answer who may take down or correct an already
 * published rating, and the role ladder those answers are read off is this
 * slice's knowledge too. The chrome and session it needs besides (AvatarImg,
 * useAuthStore) live in shared and are imported from there directly: routing
 * them through this file would make the door a second address for shared
 * instead of a narrowing of the same-layer surface.
 */
export { UserLink, userIsAdmin, userIsSeniorModerator } from "..";
