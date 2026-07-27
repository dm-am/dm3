/**
 * Cross-import surface for the testimonial entity (FSD @x).
 *
 * entities/testimonial names a recipient user in its footer, so it needs the
 * UserRef type. Same-layer entity imports are only allowed through an explicit
 * @x public API — this file is the single sanctioned door from entities/user
 * into entities/testimonial.
 */
export type { UserRef } from "..";
