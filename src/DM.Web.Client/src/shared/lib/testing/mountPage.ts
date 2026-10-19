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
import { createMemoryHistory, createRouter, type Router } from "vue-router";
import { createVfm, ModalsContainer } from "vue-final-modal";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const Blank = defineComponent({ render: () => null });

/** A named address of the app, as its route table writes it. */
export type Address = [name: string, path: string];

export interface MountOptions {
  /** Every named address the page and what it draws link to, its own included. */
  routes: Address[];
  /** The store the page reads, filled in by the spec before the mount. */
  pinia?: Pinia;
  /** In the document, where an element can be scrolled to by its anchor. */
  attachTo?: HTMLElement;
  /** Runs at the address, before the page mounts: a guard to hold a navigation. */
  beforeMount?: (router: Router) => void;
}

export async function mountPage(
  page: Component,
  address: string,
  { routes, pinia = createPinia(), attachTo, beforeMount }: MountOptions,
) {
  setActivePinia(pinia);
  const router = createRouter({
    history: createMemoryHistory(),
    routes: routes.map(([name, path]) => ({ name, path, component: Blank })),
  });
  await router.push(address);
  await router.isReady();
  beforeMount?.(router);
  // The page and the container its windows open in, as App.vue draws them.
  const host = defineComponent({
    setup: () => () => [h(page), h(ModalsContainer)],
  });
  const wrapper = mount(host, {
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
    },
  });
  return { wrapper, router };
}

/** The document body, where a window teleports to. */
export const windows = () => new DOMWrapper(document.body);
