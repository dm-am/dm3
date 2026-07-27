import type { InjectionKey } from "vue";

/**
 * Contract between the persistent forum shell (ForumPage) and its leaf
 * views. A leaf that hits a page-level failure (topic not found, access
 * denied) reports the error code here so the SHELL renders the full-screen
 * ErrorPage instead of the leaf embedding one under the forum header and
 * board strip. Pass null to clear (new load started / leaf unmounted).
 */
export const reportForumShellError: InjectionKey<
  (code: number | null) => void
> = Symbol("reportForumShellError");
