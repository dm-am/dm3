/**
 * Shared stores
 * @module shared/stores
 *
 * Global application state that is used across multiple layers.
 */

// Auth store - user session and authentication
export { useAuthStore, useUserStore } from "./auth";

// UI store - theme, notifications
export { useUiStore } from "./ui";

// Data stores
export { useWebsiteReviewStore } from "./websiteReviews";
export { useStatisticsStore } from "./statistics";
export { useSubscriptionsStore } from "./subscriptions";
