import { defineComponent, type Component } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import {
  createMemoryHistory,
  createRouter,
  type LocationQueryRaw,
  type Router,
} from "vue-router";
import { createPinia, setActivePinia } from "pinia";
import { DEBOUNCE_MS } from "@/shared/lib/composables/createFilterDispatcher";

// A filter that keeps its state in the address, on a page of its own: an
// address goes in, the state and the request come out, an action comes back as
// the address, and nothing of how the filter parses it is reached into.

/** The dispatcher's wait and a margin for the navigation after it. */
const SETTLE_MS = DEBOUNCE_MS + 30;

export interface Page {
  router: Router;
  wrapper: VueWrapper;
  /** The address once every action given so far has been written. */
  settled: () => Promise<Record<string, string>>;
}

/**
 * A component on the page `/list` (named "list") with the given query;
 * `prepare` runs before it mounts, once the stores exist.
 */
export async function onListPage(
  component: Component,
  query: LocationQueryRaw = {},
  options: Parameters<typeof mount>[1] = {},
  prepare: () => void = () => {},
): Promise<Page> {
  const pinia = createPinia();
  setActivePinia(pinia);
  prepare();
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/list", name: "list", component: { render: () => null } },
      {
        path: "/elsewhere",
        name: "elsewhere",
        component: { render: () => null },
      },
    ],
  });
  await router.push({ path: "/list", query });
  await router.isReady();
  const plugins = [router, pinia, ...(options.global?.plugins ?? [])];
  const wrapper = mount(component, {
    ...options,
    global: { ...options.global, plugins },
  });
  await flushPromises();
  const settled = async () => {
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));
    await flushPromises();
    return { ...router.currentRoute.value.query } as Record<string, string>;
  };
  return { router, wrapper, settled };
}

/** A filter composable on that page, as the list that uses it would hold it. */
export async function filterOnListPage<T>(
  use: () => T,
  query: LocationQueryRaw = {},
): Promise<Page & { filter: T }> {
  let filter: T | null = null;
  const Probe = defineComponent({
    setup() {
      filter = use();
      return () => null;
    },
  });
  const page = await onListPage(Probe, query);
  return { ...page, filter: filter! };
}

/** A filter's state with its sets spelled as lists, in the order they hold. */
export function plain(state: object): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(state).map(([key, value]) => [
      key,
      value instanceof Set ? [...value] : value,
    ]),
  );
}
