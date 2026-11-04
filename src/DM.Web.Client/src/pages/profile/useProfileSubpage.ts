import {
  computed,
  toValue,
  type ComputedRef,
  type MaybeRefOrGetter,
  type Ref,
} from "vue";
import { useRoute } from "vue-router";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import { getErrorConfig } from "@/shared/ui/ErrorPage";
import { useProfileSubpageUser } from "./useProfileSubpageUser";

/**
 * What a profile subpage does before it shows anything of its own: the owner
 * from the route, a 404 when he does not exist, the title "{username} | {label}"
 * and the link back. The label is a getter: the list subpages share one route
 * component and swap it without a remount.
 */
export function useProfileSubpage(label: MaybeRefOrGetter<string>): {
  /** The name as the address spells it, for requests and route params. */
  username: ComputedRef<string>;
  /** The name in its own case once loaded, for the heading and the title. */
  canonicalUsername: Ref<string>;
  /** True only on a 404 or 410: the page draws ErrorPage. */
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

  // A missing owner's page is titled by the error, as the profile page is.
  useDocumentTitle(() =>
    notFound.value
      ? getErrorConfig(404).title
      : joinTitleSegments(canonicalUsername.value, toValue(label)),
  );

  return { username, canonicalUsername, notFound, profileLink };
}
