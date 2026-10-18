/**
 * @vitest-environment node
 */

/**
 * Every modal, one behaviour. useDialogShell is where that behaviour lives —
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
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** Files that declare a modal without owning its behaviour, and why. */
const NOT_A_SHELL: Record<string, string> = {
  "shared/ui/Layout/Dialog.vue":
    "the vue-final-modal tier: the library owns the focus trap, and the attribute here is a prop forwarded to it. Escape and the click beside the card are caught by the window itself, which may decline them. The library's scroll lock is forwarded too and changes nothing on this site, because Reset.sass already holds html, body and #app at overflow: hidden; the page is held by usePageScrollLock, as in every window",
};

/**
 * The windows of the site, each of which holds the page still while it is
 * open. The side column of a narrow screen is a modal and not a window: it
 * holds the page with the windows' lock, but does not close with the session.
 * The picture viewer opened from a post is a window over the page as well.
 */
const WINDOWS = [
  "shared/ui/Layout/Dialog.vue",
  "shared/ui/ConfirmDialog/ConfirmDialog.vue",
  "shared/ui/ImageViewer/ImageViewer.vue",
];

/**
 * The two roles a modal may announce itself with. "alertdialog" is the one for
 * a window that interrupts the page to state a consequence and take a decision:
 * a screen reader announces it together with the text inside it, which is the
 * whole of what a confirmation has to say. Anything else is "dialog".
 */
const MODAL_ROLES = ['role="dialog"', 'role="alertdialog"'];

/**
 * The self-rolled modals, so an empty walk cannot pass for a clean one. The
 * pinned-topics overlay left the list with the window itself: the order is
 * edited on the listing now (pages/forum/TopicsList.vue). So did the insert
 * window the markup editor had: a link, an image, a spoiler caption
 * and a hidden block's readers are asked in a panel under the editor's button
 * (shared/ui/BBCodeEditor/InsertPanel.vue), which dims nothing.
 */
const KNOWN = [
  "shared/ui/ConfirmDialog/ConfirmDialog.vue",
  "shared/ui/Drawer/MobileDrawer.vue",
  "shared/ui/ImageViewer/ImageViewer.vue",
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
      .filter((one) => !MODAL_ROLES.some((role) => one.tag.includes(role)))
      .map(
        (one) =>
          `${one.path}: aria-modal without role="dialog" or role="alertdialog"`,
      );
    expect(offenders).toEqual([]);
  });

  it("holds the page still under every window", () => {
    // The page scrolls in .main and not in body, so a lock aimed at body holds
    // nothing; and a lock that takes the scrollbar without keeping its place
    // shifts the page sideways under the dimming. usePageScrollLock does both,
    // and a window reaches it directly or through the shell's holdPage.
    const offenders = WINDOWS.filter((path) => {
      const source = readFileSync(join(CLIENT_SRC, path), "utf8");
      return !/usePageScrollLock\(|holdPage:\s*true/.test(source);
    }).map(
      (path) =>
        `${path}: a window that leaves the page under it free to scroll and to jump`,
    );
    expect(offenders).toEqual([]);
  });

  it("asks the editor's insertions in a panel under the button, not in a window", () => {
    // The insertions went from a window to the panel and back to a window
    // (27 September 2026), and the owner found the window too much for a line
    // of text (28 September 2026). A window again is a decision to take, not a
    // component to swap in.
    const read = (path: string) =>
      readFileSync(join(CLIENT_SRC, "shared/ui/BBCodeEditor", path), "utf8");
    const editor = read("BBCodeEditor.vue");
    expect(editor).toMatch(/^import InsertPanel\b/m);
    expect(editor).not.toMatch(/<(InputDialog|Dialog)\b/);
    expect(modalTag(read("InsertPanel.vue"))).toBeNull();
  });

  it("keeps the vue-final-modal tier inside the viewport", () => {
    // Eleven windows open a form on the fixed 580 and 920 measures, moderation
    // forms among them. On a 375px screen an uncapped 580 runs off both edges.
    // Read off the compiled sheet: the cap is written once, in the card of
    // _DialogSurface.sass, and reaches the window through the mixin.
    const card = ruleMap(
      componentCss(join(CLIENT_SRC, "shared/ui/Layout/Dialog.vue")),
    ).get(".dialog");
    expect(card?.["max-width"]).toMatch(/^calc\(100vw/);
  });
});
