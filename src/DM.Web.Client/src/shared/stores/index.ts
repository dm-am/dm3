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

// Data stores
export { useTestimonialStore } from "./testimonials";
export { useStatisticsStore } from "./statistics";
export { useSubscriptionsStore } from "./subscriptions";
