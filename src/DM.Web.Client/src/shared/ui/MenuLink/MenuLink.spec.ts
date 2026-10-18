/**
 * @vitest-environment jsdom
 */

/**
 * A row of a sidebar menu says where the reader is in two ways at once, and
 * they have to agree: bold for the eye, aria-current for a screen reader. A
 * router-link marks itself "page" on an exact match whatever the menu meant,
 * so left alone it marked the row of a board on the board and nothing on a
 * topic of it, while the row was bold on both. The row owns the mark.
 *
 * Mounted on a real router, because the router-link's own marking is exactly
 * what is under test.
 */
import { describe, it, expect } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import MenuLink from "./MenuLink.vue";

const Empty = defineComponent({ render: () => null });

async function rowAt(
  location: string,
  props: {
    to: string;
    current?: "page" | "true";
    hint?: string;
  },
  counter?: string,
) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/forum/:alias", name: "forum", component: Empty },
      { path: "/forum/:alias/:num", name: "topic", component: Empty },
    ],
  });
  await router.push(location);
  await router.isReady();
  const wrapper = mount(MenuLink, {
    props,
    slots: {
      default: () => "Общий",
      ...(counter ? { counter: () => h("span", counter) } : {}),
    },
    global: {
      plugins: [router],
      stubs: {
        Tooltip: {
          props: ["text"],
          template: '<span class="tooltip" :data-text="text"><slot /></span>',
        },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

/** What the row copies as: a non-breaking space is still a space. */
const copied = (wrapper: Awaited<ReturnType<typeof rowAt>>) =>
  wrapper.text().replace(/\u00a0/g, " ");

describe("MenuLink", () => {
  it("copies as one line: the dash, the name and the counter", async () => {
    const wrapper = await rowAt(
      "/forum/general",
      { to: "/forum/general" },
      "\u00a0(2)",
    );

    expect(copied(wrapper)).toBe("- Общий (2)");
    // The dash is the menu's mark, not a word of the row.
    expect(wrapper.find(".dash").attributes("aria-hidden")).toBe("true");
  });

  it("marks the row of the open page bold and as the page", async () => {
    const wrapper = await rowAt("/forum/general", {
      to: "/forum/general",
      current: "page",
    });
    const link = wrapper.find("a");

    expect(link.attributes("aria-current")).toBe("page");
    expect(link.classes()).toContain("current");
  });

  it("marks the row of the place the open page lies in as such", async () => {
    const wrapper = await rowAt("/forum/general/5", {
      to: "/forum/general",
      current: "true",
    });
    const link = wrapper.find("a");

    expect(link.attributes("aria-current")).toBe("true");
    expect(link.classes()).toContain("current");
  });

  it("leaves a row it was not told to mark unmarked, the router's match aside", async () => {
    // The link leads to the very page that is open: a bare router-link would
    // say "page" here on its own.
    const wrapper = await rowAt("/forum/general", { to: "/forum/general" });
    const link = wrapper.find("a");

    expect(link.attributes("aria-current")).toBeUndefined();
    expect(link.classes()).not.toContain("current");
  });

  it("puts the hint on the link and only when there is one", async () => {
    const hinted = await rowAt("/forum/general", {
      to: "/forum/general",
      hint: "Жизнь сообщества",
    });
    expect(hinted.find(".tooltip").attributes("data-text")).toBe(
      "Жизнь сообщества",
    );
    expect(hinted.find(".tooltip a").exists()).toBe(true);

    const bare = await rowAt("/forum/general", { to: "/forum/general" });
    expect(bare.find(".tooltip").exists()).toBe(false);
    expect(bare.find("a").text()).toBe("Общий");
  });
});
