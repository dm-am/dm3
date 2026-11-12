/**
 * @vitest-environment jsdom
 */

/** The footer: the copyright and the credits, then the legal links. */
import { afterEach, describe, expect, it, vi } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { createMemoryHistory, createRouter } from "vue-router";
import Footer from "./Footer.vue";

describe("the columns of the footer", () => {
  it("reads the copyright and the credits before the legal links", () => {
    const footer = mount(Footer, {
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    const parts = footer.findAll(".footer > *").map((el) => el.classes()[0]);
    expect(parts).toEqual(["credits", "legal"]);
    expect(footer.get(".credits > div").text()).toMatch(/^© Dungeon Master/);
    footer.unmount();
  });
});

// The credit for the logo is an intentional prank: a plain click leaves for the video.
describe("the logo credit", () => {
  const video = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

  async function mountWithRouter() {
    const page = { template: "<div />" };
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: "/", name: "home", component: page },
        { path: "/profile/:username", name: "profile", component: page },
        { path: "/user-agreement", name: "user-agreement", component: page },
        { path: "/privacy-policy", name: "privacy-policy", component: page },
      ],
    });
    await router.push("/");
    const push = vi.spyOn(router, "push");
    // jsdom refuses assignment to location, so it is replaced wholesale.
    vi.stubGlobal("location", { ...window.location, href: "" });
    const footer = mount(Footer, { global: { plugins: [router] } });
    const link = footer.findAll("a").find((a) => a.text() === "Azur")!;
    return { footer, link, push };
  }

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("points at the profile, so hovering shows nothing unusual", async () => {
    const { footer, link } = await mountWithRouter();

    expect(link.attributes("href")).toBe("/profile/Azur");
    footer.unmount();
  });

  it("leaves for the video on a plain click without starting a navigation", async () => {
    const { footer, link, push } = await mountWithRouter();

    const click = new MouseEvent("click", {
      bubbles: true,
      cancelable: true,
      button: 0,
    });

    link.element.dispatchEvent(click);

    // A started navigation loads the profile's route chunk and its requests
    // for a page the reader never sees.
    expect(push).not.toHaveBeenCalled();
    // The custom slot leaves the href to the browser unless the handler cancels it.
    expect(click.defaultPrevented).toBe(true);
    expect(window.location.href).toBe(video);
    footer.unmount();
  });

  it.each([
    ["ctrl", { ctrlKey: true }],
    ["shift", { shiftKey: true }],
    ["meta", { metaKey: true }],
  ])("stays a profile link on a %s click", async (_, modifier) => {
    const { footer, link, push } = await mountWithRouter();
    // Read on the way up, then cancelled: jsdom cannot follow a link.
    let leftToTheBrowser = false;
    footer.element.addEventListener("click", (event: Event) => {
      leftToTheBrowser = !event.defaultPrevented;
      event.preventDefault();
    });
    const click = new MouseEvent("click", {
      bubbles: true,
      cancelable: true,
      button: 0,
      ...modifier,
    });

    link.element.dispatchEvent(click);

    // The browser opens the profile where the modifier says; neither the
    // router nor the prank takes the click.
    expect(leftToTheBrowser).toBe(true);
    expect(push).not.toHaveBeenCalled();
    expect(window.location.href).toBe("");
    footer.unmount();
  });
});
