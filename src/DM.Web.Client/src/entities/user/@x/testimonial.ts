/**
 * Cross-import surface for the testimonial entity (FSD @x).
 *
 * A testimonial card signs itself with its author, and in the profile lists it
 * also names the user the testimonial is about: both ends of that footer are
 * user links. UserLink is the one symbol the door carries — the tooltip, icon
 * and date helpers the card also needs live in shared and are imported from
 * there, since routing them through this file would make the door a second
 * address for shared instead of a narrowing of the same-layer surface.
 */
export { UserLink } from "..";
