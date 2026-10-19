import type { InjectionKey } from "vue";

/** A leaf's page-level failure, reported to ForumPage, which draws the ErrorPage; null clears it. */
export const reportForumShellError: InjectionKey<
  (code: number | null) => void
> = Symbol("reportForumShellError");
