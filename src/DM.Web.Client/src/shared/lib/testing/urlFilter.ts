import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineComponent, type Component } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import {
  createMemoryHistory,
  createRouter,
  type LocationQueryRaw,
  type Router,
} from "vue-router";
import { createPinia, setActivePinia } from "pinia";

/**
 * A filter that keeps its state in the address, put on a page of its own so a
 * spec can read it through what a reader of the list sees: an address goes
 * in, the state and the request come out, and an action comes back as the
 * address the filter moves to. Nothing of how the filter parses or builds the
 * address is reached into, so the specs hold while that is rewritten.
 */

/**
 * The time a filter gathers actions before it writes the address, read off
 * createFilterDispatcher, where it is a constant of the closure: a wait shorter
 * than the dispatcher's would read the address before the write, and a check
 * that an action writes nothing would pass without looking.
 */
const DISPATCH_MS = (() => {
  const source = readFileSync(
    resolve(
      dirname(fileURLToPath(import.meta.url)),
      "../composables/createFilterDispatcher.ts",
    ),
    "utf8",
  );
  const found = /const DEBOUNCE_MS = (\d+);/.exec(source);
  if (!found) throw new Error("createFilterDispatcher has no DEBOUNCE_MS");
  return Number(found[1]);
})();

/** That time and a margin for the navigation after it. */
const SETTLE_MS = DISPATCH_MS + 30;

export interface Page {
  router: Router;
  wrapper: VueWrapper;
  /** The address once every action given so far has been written. */
  settled: () => Promise<Record<string, string>>;
}

/**
 * A component on the page `/list` (named "list") with the given query.
 * `prepare` runs once the stores of the page exist and before the component
 * mounts: where a spec sets what a store holds and stops its requests.
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
