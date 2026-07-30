/**
 * Lifecycle status shared by games and blogs.
 *
 * One backend enum (`ModuleStatus`) was mirrored twice on the client: a TS enum
 * for games and a bare string union for blogs. The two mirrored transition
 * modules then compared differently — `status === GameStatus.Draft` against
 * `status === "Draft"` — so no shared helper could accept both.
 *
 * Declared as a frozen object plus a derived union rather than a TS enum: the
 * union is exactly what arrives on the wire (the API serializes enums as
 * strings), and the object still gives a named constant to compare against.
 */
export const ModuleStatus = {
  Draft: "Draft",
  Active: "Active",
  Closed: "Closed",
} as const;

export type ModuleStatus = (typeof ModuleStatus)[keyof typeof ModuleStatus];
