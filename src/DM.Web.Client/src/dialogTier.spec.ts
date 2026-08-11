/**
 * @vitest-environment node
 */

/**
 * Four modals, one behaviour. useDialogShell is where that behaviour lives —
 * focus moves in when the dialog opens, cannot leave while it is open, comes
 * back where it was on close, and Escape closes — and its own header says so.
 * The copies that never took it each lost a different part of it: the
 * off-canvas drawer kept a second implementation of the same trap, its own
 * focusables() included; the pinned-topics overlay announced itself as ordinary
 * page content, let Tab walk onto the page underneath it, and never gave the
 * caret back to the button that opened it.
 *
 * The trap had a hole of its own, and it was in the two dialogs that DID take
 * the shell. The Escape listener sits on the backdrop, and a click on the
 * dialog's own text — a heading, a sentence — moves document focus to <body>.
 * The keydown then fires on body, which is the backdrop's ANCESTOR, so it never
 * reaches the listener: the reader clicks "Вы уверены?", presses Escape, and
 * nothing happens. A container that can hold focus itself takes that click
 * instead, and the event has a path back down. That is the whole job of
 * tabindex="-1" here, and it is one attribute away from being lost again.
 *
 * Read from the sources: these dialogs never coexist in one DOM, and what is
 * checked is a missing attribute rather than a wrong rendering.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** Files that declare a modal without owning its behaviour, and why. */
const NOT_A_SHELL: Record<string, string> = {
  "shared/ui/Layout/Dialog.vue":
    "the vue-final-modal tier: the library owns focus and Escape, and the attribute here is a prop forwarded to it. It also forwards a scroll lock, and that one changes nothing on this site, because Reset.sass already holds html, body and #app at overflow: hidden and there is no page scroll under a dialog to take away. The prop stays as the library's own contract, not as what keeps the page still",
};

/** The self-rolled modals, so an empty walk cannot pass for a clean one. */
const KNOWN = [
  "pages/forum/PinnedTopicsManager.vue",
  "shared/ui/BBCodeEditor/InputDialog.vue",
  "shared/ui/ConfirmDialog/ConfirmDialog.vue",
  "shared/ui/Drawer/MobileDrawer.vue",
];

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (full.endsWith(".vue")) {
      out.push(full);
    }
  }
  return out;
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** The opening tag that carries aria-modal, attributes included. */
function modalTag(source: string): string | null {
  const at = source.indexOf('aria-modal="true"');
  if (at < 0) return null;
  const opens = source.lastIndexOf("<", at);
  const closes = source.indexOf(">", at);
  if (opens < 0 || closes < 0) return null;
  return source.slice(opens, closes + 1);
}

interface Shell {
  path: string;
  source: string;
  tag: string;
}

const shells = (): Shell[] => {
  const found: Shell[] = [];
  for (const file of collect(CLIENT_SRC)) {
    const source = readFileSync(file, "utf8");
    const tag = modalTag(source);
    if (!tag) continue;
    const path = asPath(file);
    if (path in NOT_A_SHELL) continue;
    found.push({ path, source, tag });
  }
  return found;
};

describe("the dialog tier", () => {
  it("finds every self-rolled modal there is", () => {
    expect(
      shells()
        .map((one) => one.path)
        .sort(),
    ).toEqual(KNOWN);
  });

  it("takes its behaviour from one place", () => {
    const offenders = shells()
      .filter((one) => !one.source.includes("useDialogShell"))
      .map(
        (one) =>
          `${one.path}: a modal with a focus trap, an Escape handler and a focus return of its own — the three that drifted apart last time`,
      );
    expect(offenders).toEqual([]);
  });

  it("gives the container a focus of its own", () => {
    const offenders = shells()
      .filter((one) => !one.tag.includes('tabindex="-1"'))
      .map(
        (one) =>
          `${one.path}: without tabindex="-1" a click on the dialog's own text drops focus to <body>, and Escape stops closing it`,
      );
    expect(offenders).toEqual([]);
  });

  it("names the container to a screen reader", () => {
    const offenders = shells()
      .filter((one) => !one.tag.includes('role="dialog"'))
      .map((one) => `${one.path}: aria-modal without role="dialog"`);
    expect(offenders).toEqual([]);
  });

  it("keeps the vue-final-modal tier inside the viewport", () => {
    // Seventeen files open a dialog on the fixed 580/380 tiers, moderation
    // forms among them. On a 375px screen an uncapped 580 runs off both edges.
    const source = readFileSync(
      join(CLIENT_SRC, "shared/ui/Layout/Dialog.vue"),
      "utf8",
    );
    expect(source).toMatch(/max-width:\s*calc\(100vw/);
  });
});
