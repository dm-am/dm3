/**
 * Cross-import surface for the game entity (FSD @x).
 *
 * entities/game renders author links, and UserLink is the one symbol this
 * slice owns. The chrome and session it also needs (AvatarImg, useAuthStore)
 * live in shared and are imported from there directly: routing them through
 * this file would make the door a second address for shared instead of a
 * narrowing of the same-layer surface.
 */
export { UserLink } from "..";
