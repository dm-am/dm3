/**
 * Cross-import surface for the game entity (FSD @x).
 *
 * entities/game legitimately renders user chrome (author links, avatars) and
 * reads the current user. Same-layer entity imports are only allowed through
 * an explicit @x public API — this file is the single sanctioned door from
 * entities/user into entities/game.
 */
export { UserLink, AvatarImg, useAuthStore } from "..";
