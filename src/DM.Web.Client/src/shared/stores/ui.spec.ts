/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { nextTick } from "vue";
import { Theme } from "@/shared/api/models/personal";
import { MESSAGE_LAYOUTS, useUiStore } from "./ui";

const MESSAGE_LAYOUT_KEY = "dm_message_layout";
/**
 * Written out rather than imported: the store keeps the key private. That this
 * is still the key it writes — and the one the anti-FOUC script of index.html
 * reads before the bundle loads — is what themeBoot.spec.ts holds.
 */
const THEME_KEY = "dm_theme";

describe("useUiStore message layout", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
  });

  // Driven by the array rather than by a written-out list: a layout the store
  // offers but refuses to read back drops the choice the user already made,
  // and it fails silently, so only a test that enumerates the set catches it.
  for (const layout of MESSAGE_LAYOUTS) {
    it(`restores "${layout}" saved by a previous visit`, () => {
      localStorage.setItem(MESSAGE_LAYOUT_KEY, layout);

      expect(useUiStore().messageLayout).toBe(layout);
    });
  }

  it("falls back to full when the stored value is not a layout", () => {
    localStorage.setItem(MESSAGE_LAYOUT_KEY, "cozy");

    expect(useUiStore().messageLayout).toBe("full");
  });
});

/**
 * The theme belongs to the device: what the switch in the settings panel does
 * is the whole answer, and the account only fills in a device that has never
 * answered. Every case below is about which of the three sources wins.
 */
describe("useUiStore theme", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  /** Replaces window.matchMedia with one that answers the given verdict. */
  function answerPrefersDark(dark: boolean) {
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-color-scheme: dark") && dark,
      media: query,
    }));
  }

  it("restores the theme saved by a previous visit", () => {
    localStorage.setItem(THEME_KEY, Theme.Dark);
    answerPrefersDark(false);

    expect(useUiStore().theme).toBe(Theme.Dark);
  });

  it("prefers the choice of the device to the system preference", () => {
    localStorage.setItem(THEME_KEY, Theme.Light);
    answerPrefersDark(true);

    expect(useUiStore().theme).toBe(Theme.Light);
  });

  it("follows the system preference while the device has no choice", () => {
    answerPrefersDark(true);

    expect(useUiStore().theme).toBe(Theme.Dark);
  });

  it("falls back to Light with no choice and no system preference", () => {
    answerPrefersDark(false);

    expect(useUiStore().theme).toBe(Theme.Light);
  });

  it("records what the switch does", async () => {
    answerPrefersDark(false);
    const ui = useUiStore();

    ui.toggleTheme();
    await nextTick();

    expect(ui.theme).toBe(Theme.Dark);
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Dark);
  });

  it("takes the account theme on a device that has never chosen", () => {
    answerPrefersDark(false);
    const ui = useUiStore();

    ui.adoptAccountTheme(Theme.Dark);

    expect(ui.theme).toBe(Theme.Dark);
    // Recorded on the spot rather than through the watcher: the anti-FOUC
    // script reads localStorage and nothing else, so an unrecorded theme would
    // arrive only after the first paint on every later load.
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Dark);
  });

  it("records the account theme even when it changes nothing on screen", () => {
    answerPrefersDark(false);
    const ui = useUiStore();

    ui.adoptAccountTheme(Theme.Light);

    expect(ui.theme).toBe(Theme.Light);
    // The watcher fires on a change and this is not one. Without a write of its
    // own the device would stay unchosen and take the account theme again at
    // every later sign-in, which is the dictation this store stopped.
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Light);
  });

  it("keeps the choice of the device when the account offers another", () => {
    localStorage.setItem(THEME_KEY, Theme.Light);
    answerPrefersDark(true);
    const ui = useUiStore();

    ui.adoptAccountTheme(Theme.Dark);

    expect(ui.theme).toBe(Theme.Light);
    expect(localStorage.getItem(THEME_KEY)).toBe(Theme.Light);
  });

  it("leaves the device alone when the account carries no theme", () => {
    answerPrefersDark(true);
    const ui = useUiStore();

    ui.adoptAccountTheme(undefined);

    expect(ui.theme).toBe(Theme.Dark);
    expect(localStorage.getItem(THEME_KEY)).toBeNull();
  });
});
