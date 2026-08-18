/**
 * @vitest-environment jsdom
 */

/**
 * The only place inside the site that names its addresses, and it is there for
 * the visitor whose usual address stopped answering. So it has to be drawn from
 * the host in the address bar rather than from a request. A stand is not one of
 * the site's addresses and still draws the lines: the developer has to see what
 * he is changing, and what he sees is what a visitor on the main address sees.
 *
 * These tests moved here from Footer.spec.ts together with the lines
 * themselves — the behaviour is the block's now, not the footer's.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { SITE_ADDRESSES } from "@/shared/config/site";
import SiteAddresses from "./SiteAddresses.vue";

// useRoute reads an injection rather than $route, so a mocked property on the
// instance never reaches it. The module is what has to answer.
const currentRoute = { fullPath: "/" };
vi.mock("vue-router", () => ({ useRoute: () => currentRoute }));

const [main, second] = SITE_ADDRESSES;

/** jsdom refuses assignment to location, so the host is replaced wholesale. */
function onHost(host: string) {
  vi.stubGlobal("location", { ...window.location, host });
}

function render(fullPath: string) {
  currentRoute.fullPath = fullPath;
  return mount(SiteAddresses);
}

/** Every link of the block: its host, and the text a reader sees on it. */
function links(wrapper: ReturnType<typeof render>) {
  return wrapper.findAll("a[href]").map((a) => ({
    host: new URL(a.attributes("href") as string).host,
    text: a.text(),
  }));
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("the site address block", () => {
  it("draws every address the site answers on as a link", () => {
    // Including the one being read: the rows are one list of the same kind of
    // thing, and a single row silently not being a link reads as a defect.
    onHost(main.host);
    const drawn = links(render("/"));

    expect(drawn.map((l) => l.host)).toEqual(SITE_ADDRESSES.map((a) => a.host));
  });

  it("puts the mark of the address inside its link", () => {
    // The mark names the address just as the words do, so it belongs to the
    // same click target and takes the same colour. An icon mark is an <svg>
    // of the set; a label mark is its letters.
    onHost(main.host);
    const wrapper = render("/");
    const anchors = wrapper.findAll("a[href]");

    for (const [i, address] of SITE_ADDRESSES.entries()) {
      expect(anchors[i].text()).toContain(address.name);
      if ("icon" in address.mark) {
        expect(anchors[i].find("svg").exists()).toBe(true);
      } else {
        expect(anchors[i].text()).toContain(address.mark.label);
      }
    }
  });

  it("carries the page being read across to the other address", () => {
    // A link that always landed on the home page would cost the reader their
    // place, and on this site the place is usually the point of the link.
    onHost(main.host);
    const href = render("/forum/topic/17?page=2#post-9")
      .findAll("a[href]")
      .map((a) => a.attributes("href"))
      .find((h) => new URL(h as string).host === second.host);

    expect(href).toBe(`https://${second.host}/forum/topic/17?page=2#post-9`);
  });

  it("draws the same rows on a host that is neither of them", () => {
    // localhost is every development stand. The lines used to be absent here,
    // so the only people who could break them were the only people who never
    // saw them.
    onHost("localhost:5173");

    expect(links(render("/")).map((l) => l.host)).toEqual(
      SITE_ADDRESSES.map((a) => a.host),
    );
  });
});
