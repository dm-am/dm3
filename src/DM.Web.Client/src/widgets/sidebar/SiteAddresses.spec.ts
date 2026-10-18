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
import Tooltip from "@/shared/ui/Tooltip/Tooltip.vue";

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

  it("lays every address out as a table row of name, scale and ms", () => {
    // A CSS table and not a grid of cells: columns line up by nature, and a
    // row copies as one line with its cells apart, where grid items copy as a
    // line each and a converter to markdown glued the whole board into one
    // bullet ("[Основной сервер](...)450 мс[RU-прокси](...)250 мс").
    // Spans and not table markup: a reader hears the links and their status
    // sentences, not a table. One row per address, three cells in reading
    // order; the scale's cell holds the tooltip trigger with the scale inside.
    // The display values themselves are the copy gate's business.
    onHost(main.host);
    const board = render("/").find("li.site-addresses");
    expect(board.exists()).toBe(true);
    expect(board.find("table").exists()).toBe(false);

    const rows = board.findAll(":scope > .address-row");
    expect(rows).toHaveLength(SITE_ADDRESSES.length);
    for (const row of rows) {
      const cells = row.findAll(":scope > span");
      expect(cells.map((cell) => cell.classes()[0])).toEqual([
        "address-cell",
        "signal-cell",
        "latency",
      ]);
      expect(cells[0].find("a.address-link").exists()).toBe(true);
      expect(cells[1].find(".tooltip-trigger .signal").exists()).toBe(true);
    }
  });
});

describe("the state board", () => {
  it("holds the ms cell with an ellipsis until the first measurement lands", () => {
    // The track is there from the first render, so nothing jumps when the
    // number arrives; the ellipsis says "measuring", not "not answering".
    onHost(main.host);
    const cells = render("/").findAll(".latency");

    expect(cells).toHaveLength(SITE_ADDRESSES.length);
    for (const cell of cells) {
      expect(cell.text()).toBe("...");
    }
  });

  it("shows the measured round trip and fills the scale by it", async () => {
    onHost(main.host);
    const wrapper = render("/");
    pings[main.host] = { status: "up", latencyMs: 42 };
    pings[second.host] = { status: "up", latencyMs: 196 };
    await nextTick();

    // Whole tens: a figure changing by units on every look reads as an event.
    expect(wrapper.findAll(".latency").map((cell) => cell.text())).toEqual([
      "40 мс",
      "200 мс",
    ]);

    // 42 <= 80 fills all four bars; 160 < 200 <= 300 fills two.
    const signals = wrapper.findAll(".signal");
    expect(signals[0].findAll(".bar")).toHaveLength(4);
    expect(signals[0].findAll(".bar.filled")).toHaveLength(4);
    expect(signals[1].findAll(".bar.filled")).toHaveLength(2);
    // The sentence is the site's tooltip on the scale, not the browser's
    // title: the title never showed for a finger or a keyboard.
    expect(wrapper.findAllComponents(Tooltip)[0].props("text")).toBe(
      "Скорость ответа из вашей сети",
    );
  });

  it("turns the comb red and says нет for an address that does not answer", async () => {
    onHost(main.host);
    const wrapper = render("/");
    pings[main.host] = { status: "up", latencyMs: 42 };
    pings[second.host] = { status: "down", latencyMs: null };
    await nextTick();

    const signal = wrapper.findAll(".signal")[1];
    expect(signal.classes()).toContain("down");
    expect(signal.findAll(".bar")).toHaveLength(4);
    expect(signal.findAll(".bar.filled")).toHaveLength(0);
    expect(wrapper.findAllComponents(Tooltip)[1].props("text")).toBe(
      "Не отвечает из вашей сети",
    );
    expect(wrapper.findAll(".latency")[1].text()).toBe("нет");
  });

  it("says nothing visible about state", async () => {
    // The numbers say it. The words live only in the scale's tooltip and the
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
    // No browser title anywhere: a title bubble is not the site's tooltip, and
    // a state sentence in one would come back through the clipboard of a
    // hover as well as look foreign.
    expect(wrapper.findAll("[title]")).toHaveLength(0);
  });
});
