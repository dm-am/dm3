import { ref, computed, type Ref } from "vue";
import { communityApi, unwrapResource } from "@/shared/api";
import type { User, Username } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";

/**
 * useProfileSubpageUser — resolves the profile owner for the four
 * profile subpages (received/given reviews, received/given endorsements).
 *
 * These pages never fetch the full `UserProfile` DTO (that's ProfilePage's
 * job) — they only need the lightweight `User` (for canonical username
 * casing in the h1/document title) plus a way to distinguish "user does
 * not exist" (404 page) from "the request failed" (ErrorState, retry).
 *
 * Uses `communityApi.getUser` (`GET /v1/users/{username}`) — the same
 * truncated DTO used for list rows, already returned in canonical case.
 */
export function useProfileSubpageUser(username: Ref<string>): {
  user: Ref<User | null>;
  loading: Ref<boolean>;
  /** True only on a genuine 404/410 (user does not exist) */
  notFound: Ref<boolean>;
  /** Set on any other failure (network/5xx) — distinct from notFound */
  error: Ref<boolean>;
  /** Canonical-case username once loaded, falls back to the URL param */
  canonicalUsername: Ref<string>;
  retry: () => Promise<void>;
} {
  const user = ref<User | null>(null);
  const loading = ref(false);
  const notFound = ref(false);
  const error = ref(false);

  // GET /v1/users/{username} is documented as returning the resource
  // directly, but the sibling /profile endpoint actually wraps it as
  // `{ resource: User }` (see entities/user/model/communityStore.ts —
  // "typed lie" comment there). Unwrap defensively (shared helper) so a
  // matching drift here doesn't silently break username-casing/notFound
  // resolution.
  async function load(name: string) {
    if (!name) return;
    loading.value = true;
    notFound.value = false;
    error.value = false;
    const { data, error: apiError } = await communityApi.getUser(
      name as Username,
    );
    loading.value = false;
    if (apiError) {
      if (apiError.status === 404 || apiError.status === 410) {
        notFound.value = true;
      } else {
        error.value = true;
      }
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

  return {
    user,
    loading,
    notFound,
    error,
    canonicalUsername,
    retry: () => load(username.value),
  };
}
