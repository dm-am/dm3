import { computed, type ComputedRef, type Ref } from "vue";
import { useRoute } from "vue-router";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import { useProfileSubpageUser } from "./useProfileSubpageUser";

/**
 * useProfileSubpage — everything a profile subpage does before it shows
 * anything of its own: read the owner out of the route, resolve him (404 when
 * he does not exist), title the document "{username} | {label}" and build the
 * link back to his profile.
 *
 * All five subpages carried those twenty-odd lines verbatim, and the two pairs
 * that differ only in direction (given/received) were the closest of the page
 * pairs in the tree — a change to the shared part had to be made twice, with
 * nothing to say the second place existed. The label is the only thing they
 * disagreed on, so it is the only argument.
 */
export function useProfileSubpage(label: string): {
  /** Username exactly as the URL spells it: for API calls and route params */
  username: ComputedRef<string>;
  /** Canonical-case username once loaded, for the heading and the title */
  canonicalUsername: Ref<string>;
  /** True only on a genuine 404/410 — the page renders ErrorPage instead */
  notFound: Ref<boolean>;
  /** Route location of the owner's profile */
  profileLink: ComputedRef<{ name: "profile"; params: { username: string } }>;
} {
  const route = useRoute();
  const username = computed(() => route.params.username as string);

  const { notFound, canonicalUsername } = useProfileSubpageUser(username);

  const profileLink = computed(() => ({
    name: "profile" as const,
    params: { username: username.value },
  }));

  useDocumentTitle(() => joinTitleSegments(canonicalUsername.value, label));

  return { username, canonicalUsername, notFound, profileLink };
}
