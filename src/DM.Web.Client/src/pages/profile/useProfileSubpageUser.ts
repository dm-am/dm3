import { ref, computed, type Ref } from "vue";
import { unwrapResource } from "@/shared/api";
import { userApi } from "@/entities/user";
import type { User, Username } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";

/**
 * The owner of a profile subpage, for the name in its own case and for the
 * 404; a failed request is not that answer, each list reports its own failure.
 */
export function useProfileSubpageUser(username: Ref<string>): {
  /** True only on a 404 or 410. */
  notFound: Ref<boolean>;
  /** The name in its own case once loaded, else the address's. */
  canonicalUsername: Ref<string>;
} {
  const user = ref<User | null>(null);
  const notFound = ref(false);

  // unwrapResource reads the answer with or without its envelope.
  async function load(name: string) {
    if (!name) return;
    notFound.value = false;
    const { data, error: apiError } = await userApi.getUser(name as Username);
    if (apiError) {
      // Only "no such user" is the page's; any other failure leaves the address's name.
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
