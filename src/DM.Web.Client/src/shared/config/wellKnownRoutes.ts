/**
 * SSOT for well-known routes referenced from outside their owning module.
 * Used in: pages/about/TestimonialsPage.vue, pages/home/RandomTestimonials.vue
 */

/** Seeded feedback topic ("general" forum, topic #1) where users leave reviews */
export const TESTIMONIALS_FORUM_TOPIC = {
  name: "topic",
  params: { alias: "general", num: 1 },
} as const;

/**
 * Game settings page (master-only game configuration, incl. attribute schema).
 * Route itself is added by the game-settings feature.
 */
export function gameSettingsRoute(gameId: string) {
  return { name: "game-settings", params: { id: gameId } } as const;
}
