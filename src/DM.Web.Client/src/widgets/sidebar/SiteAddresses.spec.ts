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
 * Since W3.9 B1 the block is a state board: every row carries a live signal
 * scale and a measured round trip beside the link. The measurement itself is
 * useAddressPing's business and the network has no place in a unit spec, so
 * the composable is replaced with a reactive record the tests drive by hand.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick, reactive } from "vue";
import { SITE_ADDRESSES } from "@/shared/config/site";
import type { AddressPing } from "@/shared/lib/composables/useAddressPing";
import SiteAddresses from "./SiteAddresses.vue";

// useRoute reads an injection rather than $route, so a mocked property on the
// instance never reaches it. The module is what has to answer.
const currentRoute = { fullPath: "/" };
vi.mock("vue-router", () => ({ useRoute: () => currentRoute }));

// The factory closes over the record and hands it out at mount time, long
// after this module's body has run, so the reference below is never premature.
const pings = reactive<Record<string, AddressPing>>({});
vi.mock("@/shared/lib/composables/useAddressPing", () => ({
  useAddressPing: () => pings,
}));

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

/** The copy a sighted visitor gets: the clipped screen-reader spans excluded. */
function visibleText(wrapper: ReturnType<typeof render>): string {
  const root = wrapper.element.cloneNode(true) as HTMLElement;
  for (const hidden of root.querySelectorAll(".visually-hidden")) {
    hidden.remove();
  }
  return root.textContent ?? "";
}

beforeEach(() => {
  // Every address starts unmeasured, the way a fresh mount sees the world.
  for (const address of SITE_ADDRESSES) {
    pings[address.host] = { status: "pending", latencyMs: null };
  }
});

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

  it("keeps the mark beside the link, not inside it", () => {
    // The mark is data the way the digits are (owner's call, 2026-08-25):
    // it shares the lead cell with the anchor for geometry, but neither
    // looks like a link nor navigates. Only the name is the click target.
    onHost(main.host);
    const wrapper = render("/");
    const anchors = wrapper.findAll("a[href]");

    for (const [i, address] of SITE_ADDRESSES.entries()) {
      expect(anchors[i].text()).toContain(address.name);
      expect(anchors[i].find(".address-mark").exists()).toBe(false);
      const cell = anchors[i].element.closest(".address-cell")!;
      if ("icon" in address.mark) {
        expect(cell.querySelector(".address-mark svg")).not.toBeNull();
      } else {
        expect(cell.querySelector(".address-mark")!.textContent).toContain(
          address.mark.label,
        );
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

  it("lays every row's cells into one grid container", () => {
    // One grid on the block, not one per row: the columns must land on the
    // same x in every row, and only shared tracks guarantee that. So the
    // link, the scale and the ms cell of every row are direct children of
    // the single board element.
    onHost(main.host);
    const board = render("/").find("li.site-addresses");
    expect(board.exists()).toBe(true);

    const children = Array.from(board.element.children);
    const of = (selector: string) =>
      children.filter((child) => child.matches(selector)).length;
    expect(of("span.address-cell")).toBe(SITE_ADDRESSES.length);
    expect(of(".signal")).toBe(SITE_ADDRESSES.length);
    expect(of(".latency")).toBe(SITE_ADDRESSES.length);
  });
});

describe("the state board", () => {
  it("reserves the ms cell with a dash until the first measurement lands", () => {
    // The track is there from the first render, so nothing jumps when the
    // number arrives; the dash says "not measured", not "not answering".
    onHost(main.host);
    const cells = render("/").findAll(".latency");

    expect(cells).toHaveLength(SITE_ADDRESSES.length);
    for (const cell of cells) {
      expect(cell.text()).toBe("-");
    }
  });

  it("shows the measured round trip and fills the scale by it", async () => {
    onHost(main.host);
    const wrapper = render("/");
    pings[main.host] = { status: "up", latencyMs: 42 };
    pings[second.host] = { status: "up", latencyMs: 200 };
    await nextTick();

    expect(wrapper.findAll(".latency").map((cell) => cell.text())).toEqual([
      "42 мс",
      "200 мс",
    ]);

    // 42 <= 80 fills all four bars; 160 < 200 <= 300 fills two.
    const signals = wrapper.findAll(".signal");
    expect(signals[0].findAll(".bar")).toHaveLength(4);
    expect(signals[0].findAll(".bar.filled")).toHaveLength(4);
    expect(signals[1].findAll(".bar.filled")).toHaveLength(2);
    expect(signals[0].attributes("title")).toBe(
      "отвечает из вашей сети, 42 мс",
    );
  });

  it("turns the comb red and keeps the dash for an address that does not answer", async () => {
    onHost(main.host);
    const wrapper = render("/");
    pings[main.host] = { status: "up", latencyMs: 42 };
    pings[second.host] = { status: "down", latencyMs: null };
    await nextTick();

    const signal = wrapper.findAll(".signal")[1];
    expect(signal.classes()).toContain("down");
    expect(signal.findAll(".bar")).toHaveLength(4);
    expect(signal.findAll(".bar.filled")).toHaveLength(0);
    expect(signal.attributes("title")).toBe("не отвечает из вашей сети");
    expect(wrapper.findAll(".latency")[1].text()).toBe("-");
  });

  it("says nothing visible about state", async () => {
    // The numbers say it. The words live only in the scale's title and the
    // screen-reader span; the visible copy is the names and the figures, and
    // no row is marked as "the one you are on".
    onHost(main.host);
    const wrapper = render("/");
    pings[main.host] = { status: "up", latencyMs: 42 };
    pings[second.host] = { status: "down", latencyMs: null };
    await nextTick();

    const visible = visibleText(wrapper);
    expect(visible).toContain(main.name);
    expect(visible).toContain(second.name);
    expect(visible).not.toContain("отвечает");
    expect(visible).not.toContain("текущий");

    expect(wrapper.find("strong").exists()).toBe(false);
    expect(wrapper.find("b").exists()).toBe(false);
    for (const titled of wrapper.findAll("[title]")) {
      expect(titled.classes()).toContain("signal");
    }
  });
});
