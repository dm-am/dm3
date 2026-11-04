/**
 * @vitest-environment jsdom
 */

/**
 * Every branch of the two traffic questions, the yes ones included: a typo in
 * a literal breaks nothing visible, the reader only pays for unasked pages.
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
