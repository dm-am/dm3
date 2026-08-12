import { ref, computed, type Ref } from "vue";
import { unwrapResource } from "@/shared/api";
import { userApi } from "@/entities/user";
import type { User, Username } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";

/**
 * useProfileSubpageUser — resolves the profile owner for the four
 * profile subpages (received/given reviews, received/given endorsements).
 *
 * These pages never fetch the full `UserProfile` DTO (that's ProfilePage's
 * job) — they only need the lightweight `User` for canonical username casing
 * in the h1/document title, and the answer to "does this user exist at all",
 * which turns the page into a 404. A failed request is not that answer: each
 * page reports its own list's failure with its own ErrorState.
 *
 * Uses `userApi.getUser` (`GET /v1/users/{username}`) — the same
 * truncated DTO used for list rows, already returned in canonical case.
 */
export function useProfileSubpageUser(username: Ref<string>): {
  /** True only on a genuine 404/410 (user does not exist) */
  notFound: Ref<boolean>;
  /** Canonical-case username once loaded, falls back to the URL param */
  canonicalUsername: Ref<string>;
} {
  const user = ref<User | null>(null);
  const notFound = ref(false);

  // GET /v1/users/{username} is documented as returning the resource
  // directly, but the sibling /profile endpoint actually wraps it as
  // `{ resource: User }` (see entities/user/model/communityStore.ts —
  // "typed lie" comment there). Unwrap defensively (shared helper) so a
  // matching drift here doesn't silently break username-casing/notFound
  // resolution.
  async function load(name: string) {
    if (!name) return;
    notFound.value = false;
    const { data, error: apiError } = await userApi.getUser(name as Username);
    if (apiError) {
      // Only "no such user" belongs to the page as a whole. Any other failure
      // leaves the heading on the URL-cased name — what the page shows while
      // the request is still in flight anyway.
      notFound.value = apiError.status === 404 || apiError.status === 410;
      user.value = null;
      return;
    }
    user.value = unwrapResource<User>(data);
  }

  useFetchData(
    () => load(username.value),
    [
      {
        param: () => username.value,
        callback: () => load(username.value),
      },
    ],
  );

  const canonicalUsername = computed(
    () => user.value?.username ?? username.value,
  );

  return { notFound, canonicalUsername };
}
