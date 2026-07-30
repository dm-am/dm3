/**
 * Shared stores
 * @module shared/stores
 *
 * Global application state that is used across multiple layers.
 */

// Auth store - user session and authentication
export { useAuthStore } from "./auth";

// UI store - theme, notifications
export { useUiStore } from "./ui";

// Domain stores are NOT here: they belong to their entity slice
// (entities/testimonial, entities/subscription, entities/statistics).
// shared/stores keeps only what is genuinely global — the session and the UI.
