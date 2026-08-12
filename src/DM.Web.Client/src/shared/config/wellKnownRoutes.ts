/**
 * SSOT for well-known routes referenced from outside their owning module.
 */

/** Seeded feedback topic ("general" forum, topic #1) where users leave reviews */
export const TESTIMONIALS_FORUM_TOPIC = {
  name: "topic",
  params: { alias: "general", num: 1 },
} as const;
