/**
 * @vitest-environment jsdom
 */

/**
 * The shell asks one question of the window, and every part of it gets the
 * same answer: the columns, the floating buttons, the menu that moves into the
 * sliding column. The number it answers by is the scale's, written a second
 * time because a script cannot read a Sass variable, so the first check here is
 * that the two copies agree.
 */
import { describe, it, expect, afterEach, vi } from "vitest";
import { effectScope } from "vue";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { SHELL_WIDTHS, useShellMode } from "./useShellMode";

const SCALE = join(
  dirname(fileURLToPath(import.meta.url)),
  "../../../assets/styles/_Breakpoints.sass",
);

/** A window of the given width whose media queries can be resized. */
function windowOf(width: number) {
  const lists: { query: string; listeners: Set<() => void> }[] = [];
  let current = width;
  const evaluate = (query: string) => {
    const max = /max-width:\s*(\d+)px/.exec(query);
    const min = /min-width:\s*(\d+)px/.exec(query);
    return (
      (!max || current <= Number(max[1])) && (!min || current >= Number(min[1]))
    );
  };
  vi.stubGlobal("matchMedia", (query: string) => {
    const entry = { query, listeners: new Set<() => void>() };
    lists.push(entry);
    return {
      media: query,
      get matches() {
        return evaluate(query);
      },
      addEventListener: (_: string, listener: () => void) =>
        entry.listeners.add(listener),
      removeEventListener: (_: string, listener: () => void) =>
        entry.listeners.delete(listener),
    };
  });
  return {
    resize(next: number) {
      current = next;
      lists.forEach((entry) =>
        entry.listeners.forEach((listener) => listener()),
      );
    },
    listening: () =>
      lists.reduce((count, entry) => count + entry.listeners.size, 0),
  };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("the widths of the shell", () => {
  it("are the steps of the breakpoint scale", () => {
    const scale = readFileSync(SCALE, "utf8");
    const step = (name: string) =>
      Number(new RegExp(`^\\$${name}:\\s*(\\d+)px`, "m").exec(scale)?.[1]);

    expect(SHELL_WIDTHS.shell).toBe(step("bp-shell"));
  });
});

describe("useShellMode", () => {
  const narrowAt = (width: number) => {
    windowOf(width);
    const scope = effectScope();
    const narrow = scope.run(() => useShellMode().narrow.value);
    scope.stop();
    return narrow;
  };

  it("reads each width the way the styles do", () => {
    expect(narrowAt(1366)).toBe(false);
    expect(narrowAt(1001)).toBe(false);
    // `max-width: $bp-shell` takes the step itself in.
    expect(narrowAt(1000)).toBe(true);
    // A phone is narrow the same way: no width below the shell has a shell of
    // its own.
    expect(narrowAt(768)).toBe(true);
    expect(narrowAt(390)).toBe(true);
  });

  it("follows the window as it changes", () => {
    const window = windowOf(1366);
    const scope = effectScope();
    const shell = scope.run(() => useShellMode())!;

    expect(shell.narrow.value).toBe(false);
    window.resize(812);
    expect(shell.narrow.value).toBe(true);
    window.resize(375);
    expect(shell.narrow.value).toBe(true);
    window.resize(1280);
    expect(shell.narrow.value).toBe(false);

    scope.stop();
  });

  it("stops listening with the scope that asked", () => {
    const window = windowOf(1366);
    const scope = effectScope();
    scope.run(() => useShellMode());
    expect(window.listening()).toBeGreaterThan(0);

    scope.stop();
    expect(window.listening()).toBe(0);
  });

  it("reads as wide where there is no matchMedia", () => {
    vi.stubGlobal("matchMedia", undefined);
    const scope = effectScope();
    expect(scope.run(() => useShellMode().narrow.value)).toBe(false);
    scope.stop();
  });
});
