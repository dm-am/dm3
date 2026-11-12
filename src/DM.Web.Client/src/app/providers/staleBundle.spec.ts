/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";

import {
  installPreloadErrorReload,
  isStaleBundleError,
  cancelPendingReload,
  reloadOnce,
} from "./staleBundle";

describe("a failed import of a renamed chunk", () => {
  it.each([
    [
      "Chromium",
      new TypeError(
        "Failed to fetch dynamically imported module: http://localhost:5173/assets/AboutPage-1a2b3c.js",
      ),
    ],
    ["Safari", new TypeError("Importing a module script failed.")],
    [
      "Vite's CSS preload",
      new Error("Unable to preload CSS for /assets/AboutPage-1a2b3c.css"),
    ],
    [
      "Firefox",
      new TypeError(
        "error loading dynamically imported module: http://localhost:5173/assets/AboutPage-1a2b3c.js",
      ),
    ],
    [
      "webpack",
      Object.assign(new Error("Loading chunk 7 failed."), {
        name: "ChunkLoadError",
      }),
    ],
  ])("is recognised as %s words it", (_browser, error) => {
    expect(isStaleBundleError(error)).toBe(true);
  });

  it.each([
    ["an unrelated error", new TypeError("x is not a function")],
    ["nothing", undefined],
  ])("is not confused with %s", (_case, error) => {
    expect(isStaleBundleError(error)).toBe(false);
  });
});

describe("the reload after a failed import", () => {
  const reload = vi.fn();
  const assign = vi.fn();
  let teardown: () => void;

  beforeEach(() => {
    vi.useFakeTimers();
    sessionStorage.clear();
    reload.mockClear();
    assign.mockClear();
    vi.stubGlobal("location", { ...window.location, reload, assign });
    teardown = installPreloadErrorReload();
  });

  afterEach(() => {
    teardown();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  function failImport() {
    window.dispatchEvent(new Event("vite:preloadError"));
    vi.runAllTimers();
  }

  it("reloads the page once, outside a navigation", () => {
    failImport();

    expect(reload).toHaveBeenCalledTimes(1);
    expect(sessionStorage.getItem("dm_chunk_reload")).not.toBeNull();
  });

  it("does not reload again while the flag stands", () => {
    failImport();
    failImport();

    expect(reload).toHaveBeenCalledTimes(1);
  });

  it("leaves a failed navigation to the router, which opens its target", () => {
    window.dispatchEvent(new Event("vite:preloadError"));
    reloadOnce("/about");
    vi.runAllTimers();

    expect(assign).toHaveBeenCalledExactlyOnceWith("/about");
    expect(reload).not.toHaveBeenCalled();
  });

  it("keeps the page when the caller handles the failure itself", () => {
    window.dispatchEvent(new Event("vite:preloadError"));
    cancelPendingReload();
    vi.runAllTimers();

    expect(reload).not.toHaveBeenCalled();
    expect(sessionStorage.getItem("dm_chunk_reload")).toBeNull();
  });

  it("stops listening once torn down", () => {
    teardown();
    failImport();

    expect(reload).not.toHaveBeenCalled();
  });
});
