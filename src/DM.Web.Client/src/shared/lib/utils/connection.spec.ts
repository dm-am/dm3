/**
 * @vitest-environment jsdom
 */

/**
 * The two questions asked before spending a reader's traffic on something they
 * did not ask for.
 *
 * Worth a file of its own because of how this code fails. Both answers are
 * read off a field name in an object the platform hands over, both are
 * compared against string literals, and both callers use the answer to do
 * NOTHING — the address block skips its probes, route prefetch skips its
 * fetch. A typo in one of the literals therefore breaks nothing visible: the
 * site keeps working, keeps rendering, keeps passing every other gate, and the
 * only symptom is a reader on a metered phone quietly paying for pages they
 * never opened. `"2g"` was exactly that for one wave — the sibling of
 * `"slow-2g"`, and executed by no test in the tree.
 *
 * So every branch is named here, including the ones that say yes.
 */
import { describe, it, expect, afterEach } from "vitest";
import { onSlowConnection, savingData } from "./connection";

/** jsdom has no `navigator.connection`; every case installs its own. */
function connectionReports(info: Record<string, unknown> | undefined): void {
  Object.defineProperty(navigator, "connection", {
    value: info,
    configurable: true,
  });
}

afterEach(() => connectionReports(undefined));

describe("savingData", () => {
  it("is true when the reader has asked for less traffic", () => {
    connectionReports({ saveData: true });

    expect(savingData()).toBe(true);
  });

  it("is false when the reader has not", () => {
    connectionReports({ saveData: false });

    expect(savingData()).toBe(false);
  });

  it("is false when the browser does not report the flag", () => {
    connectionReports({ effectiveType: "4g" });

    expect(savingData()).toBe(false);
  });

  it("is false where there is no connection object at all", () => {
    // Safari and Firefox, which is most of the desktop audience: the absence
    // of the API must read as "no objection", never as "hold everything".
    connectionReports(undefined);

    expect(savingData()).toBe(false);
  });
});

describe("onSlowConnection", () => {
  // Named one by one rather than looped: a loop over the same two literals the
  // implementation uses would pass whichever pair of strings both sides agreed
  // on, including a misspelt one.
  it("is true on 2g", () => {
    connectionReports({ effectiveType: "2g" });

    expect(onSlowConnection()).toBe(true);
  });

  it("is true on slow-2g", () => {
    connectionReports({ effectiveType: "slow-2g" });

    expect(onSlowConnection()).toBe(true);
  });

  it("is false on 3g, which is the audience the head start is for", () => {
    connectionReports({ effectiveType: "3g" });

    expect(onSlowConnection()).toBe(false);
  });

  it("is false on 4g", () => {
    connectionReports({ effectiveType: "4g" });

    expect(onSlowConnection()).toBe(false);
  });

  it("is false when the browser does not grade the connection", () => {
    connectionReports({ saveData: false });

    expect(onSlowConnection()).toBe(false);
  });

  it("is false where there is no connection object at all", () => {
    connectionReports(undefined);

    expect(onSlowConnection()).toBe(false);
  });
});
