import { describe, it, expect } from "vitest";
import { SITE_ADDRESSES, otherSiteAddresses } from "./site";

/**
 * The footer names the address a visitor is not reading, and it decides which
 * one that is from the host in the address bar rather than from the server. So
 * the whole behaviour of that line is this function, and the case that matters
 * most is the one nobody would think to open: a host the list does not know.
 */
describe("the other addresses of the site", () => {
  it("names every address except the one being read", () => {
    const main = SITE_ADDRESSES[0];
    const others = otherSiteAddresses(main.host);

    expect(others).not.toContainEqual(main);
    expect(others).toEqual(SITE_ADDRESSES.filter((a) => a.host !== main.host));
  });

  it("names the site when the second address is being read", () => {
    const second = SITE_ADDRESSES[SITE_ADDRESSES.length - 1];
    const others = otherSiteAddresses(second.host);

    expect(others.map((a) => a.host)).not.toContain(second.host);
    expect(others.length).toBe(SITE_ADDRESSES.length - 1);
  });

  it("treats a host outside the list as the first address", () => {
    // Every development and preview stand lands here. Answering with nothing
    // there hid the line from the only people who work on it, and a stand is
    // not a place where naming the second address misleads anyone: it is the
    // same text the site itself shows.
    const rest = SITE_ADDRESSES.slice(1);

    expect(otherSiteAddresses("localhost:5173")).toEqual(rest);
    expect(otherSiteAddresses("127.0.0.1:5174")).toEqual(rest);
    expect(otherSiteAddresses("")).toEqual(rest);
  });

  it("holds addresses that are hosts, not URLs", () => {
    // The footer builds the link as https://{host}{path}. A scheme or a slash
    // stored here would arrive in the middle of that string.
    for (const address of SITE_ADDRESSES) {
      expect(address.host).not.toContain("/");
      expect(address.host).not.toContain(":");
      expect(address.name.trim()).toBe(address.name);
    }
  });
});
