/**
 * @vitest-environment jsdom
 */

/** The addresses of the site, drawn without a request; the ping is a reactive record the tests drive. */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import type { VueWrapper } from "@vue/test-utils";
import { nextTick, reactive } from "vue";
import { SITE_ADDRESSES } from "@/shared/config/site";
import type { AddressPing } from "@/shared/lib/composables/useAddressPing";
import SiteAddresses from "./SiteAddresses.vue";
import Tooltip from "@/shared/ui/Tooltip/Tooltip.vue";
import { mountPage } from "@/shared/lib/testing/mountPage";

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

/** The block on the page being read at the address. */
async function render(fullPath: string) {
  const { wrapper } = await mountPage(SiteAddresses, fullPath, {
    routes: [["page", "/:pathMatch(.*)*"]],
  });
  return wrapper;
}

/** Every link of the block: its host, and the text a reader sees on it. */
function links(wrapper: VueWrapper) {
  return wrapper.findAll("a[href]").map((a) => ({
    host: new URL(a.attributes("href") as string).host,
    text: a.text(),
  }));
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
  it("draws every address the site answers on as a link", async () => {
    // Including the one being read: the rows are one list of the same kind of
    // thing, and a single row silently not being a link reads as a defect.
    onHost(main.host);
    const drawn = links(await render("/"));

    expect(drawn.map((l) => l.host)).toEqual(SITE_ADDRESSES.map((a) => a.host));
  });

  it("keeps the mark beside the link, not inside it", async () => {
    // The mark is data, as the digits are: only the name is the click target.
    onHost(main.host);
    const wrapper = await render("/");
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

  it("carries the page being read across to the other address", async () => {
    // A link to the home page would cost the reader his place.
    onHost(main.host);
    const href = (await render("/forum/topic/17?page=2#post-9"))
      .findAll("a[href]")
      .map((a) => a.attributes("href"))
      .find((h) => new URL(h as string).host === second.host);

    expect(href).toBe(`https://${second.host}/forum/topic/17?page=2#post-9`);
  });

  it("draws the same rows on a host that is neither of them", async () => {
    // localhost is every development stand.
    onHost("localhost:5173");

    expect(links(await render("/")).map((l) => l.host)).toEqual(
      SITE_ADDRESSES.map((a) => a.host),
    );
  });

  it("lays every address out as a table row of name, scale and ms", async () => {
    // A CSS table of spans: one row per address, three cells in reading order.
    onHost(main.host);
    const board = (await render("/")).find("li.site-addresses");
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
  it("holds the ms cell with an ellipsis until the first measurement lands", async () => {
    // The track stands from the first render, so nothing jumps when the number arrives.
    onHost(main.host);
    const cells = (await render("/")).findAll(".latency");

    expect(cells).toHaveLength(SITE_ADDRESSES.length);
    for (const cell of cells) {
      expect(cell.text()).toBe("...");
    }
  });

  it("shows the measured round trip and fills the scale by it", async () => {
    onHost(main.host);
    const wrapper = await render("/");
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
    // The site's tooltip, not the browser's title.
    expect(wrapper.findAllComponents(Tooltip)[0].props("text")).toBe(
      "Скорость ответа из вашей сети",
    );
  });

  it("turns the comb red and says нет for an address that does not answer", async () => {
    onHost(main.host);
    const wrapper = await render("/");
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
});
