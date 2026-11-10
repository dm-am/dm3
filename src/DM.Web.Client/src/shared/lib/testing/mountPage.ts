/**
 * A page mounted the way the app runs it: the real router at the address under
 * test, a fresh store, the windows of vue-final-modal with their container and
 * the shared components the app registers globally. A spec mocks only its API
 * client, and names the addresses the page and what it draws link to.
 *
 *   const { wrapper, router } = await mountPage(
 *     ModerationBans,
 *     "/moderation/bans?number=3",
 *     { routes: [["moderation-bans", "/moderation/bans"]] },
 *   );
 */
import { defineComponent, h, type Component } from "vue";
import { DOMWrapper, mount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import {
  createMemoryHistory,
  createRouter,
  RouterView,
  type RouteMeta,
  type Router,
} from "vue-router";
import { createVfm, ModalsContainer } from "vue-final-modal";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const Blank = defineComponent({ render: () => null });

/** What a route record carries besides its name and path. */
export interface AddressRecord {
  /** The meta the page reads off its route: requiresAuth, fromLetter. */
  meta?: RouteMeta;
  /** The views by outlet name, for a shell that draws named RouterViews. */
  components?: Record<string, Component>;
}

/** A named address of the app, as its route table writes it. */
export type Address = [name: string, path: string, record?: AddressRecord];

export interface MountOptions {
  /** Every named address the page and what it draws link to, its own included. */
  routes: Address[];
  /** The store the page reads, filled in by the spec before the mount. */
  pinia?: Pinia;
  /** In the document, where an element can be scrolled to by its anchor. */
  attachTo?: HTMLElement;
  /** Runs at the address, before the page mounts: a guard to hold a navigation. */
  beforeMount?: (router: Router) => void;
  /** The page's props; wrapper.setProps changes them after the mount. */
  props?: Record<string, unknown>;
  /** Children drawn as stubs, as global.stubs of mount takes them. */
  stubs?: Record<string, Component | boolean>;
  /**
   * The page is the component of the address's route, drawn by a RouterView:
   * its leave guards run, and a navigation to another route unmounts it.
   */
  routed?: boolean;
}

export async function mountPage(
  page: Component,
  address: string,
  {
    routes,
    pinia = createPinia(),
    attachTo,
    beforeMount,
    props,
    stubs,
    routed = false,
  }: MountOptions,
) {
  setActivePinia(pinia);
  const router = createRouter({
    history: createMemoryHistory(),
    routes: routes.map(
      ([name, path, { meta, components = { default: Blank } } = {}]) => ({
        name,
        path,
        meta,
        components,
      }),
    ),
  });
  if (routed) {
    const own = router.resolve(address).matched[0];
    own.components = { default: page };
  }
  await router.push(address);
  await router.isReady();
  beforeMount?.(router);
  // The page and the container its windows open in, as App.vue draws them;
  // the host hands the page what it is given, so setProps reaches the page.
  const host = defineComponent(
    (_: Record<string, unknown>, { attrs }) =>
      () => [h(routed ? RouterView : page, attrs), h(ModalsContainer)],
    { inheritAttrs: false },
  );
  const wrapper = mount(host, {
    props,
    attachTo,
    global: {
      plugins: [pinia, router, createVfm()],
      components: {
        PageTitle,
        BlockTitle,
        SecondaryText,
        Dialog,
        Button,
        Form,
        FormField,
      },
      stubs,
    },
  });
  return { wrapper, router };
}

/** The document body, where a window teleports to. */
export const windows = () => new DOMWrapper(document.body);
