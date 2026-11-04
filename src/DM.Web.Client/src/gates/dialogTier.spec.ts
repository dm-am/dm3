/**
 * @vitest-environment node
 */

/**
 * Every modal, one behaviour: useDialogShell moves focus in when the dialog
 * opens, keeps it in while it is open, gives it back on close, and closes on
 * Escape. The dialog container holds focus itself (tabindex="-1"): a click on
 * the dialog's own text otherwise moves focus to <body>, an ancestor of the
 * backdrop, and Escape never reaches the listener. Read from the sources: what
 * is checked is a missing attribute rather than a wrong rendering.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** Files that declare a modal without owning its behaviour, and why. */
const NOT_A_SHELL: Record<string, string> = {
  "shared/ui/Layout/Dialog.vue":
    "the vue-final-modal tier: the library owns the focus trap, and the attribute here is a prop forwarded to it. Escape and the click beside the card are caught by the window itself, which may decline them. The library's scroll lock is forwarded too and changes nothing on this site, because Reset.sass already holds html, body and #app at overflow: hidden; the page is held by usePageScrollLock, as in every window",
};

/**
 * The windows of the site, each of which holds the page still while it is
 * open. The picture viewer opened from a post is a window over the page as well.
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

/** The self-rolled modals, so an empty walk cannot pass for a clean one. */
const KNOWN = [
  "shared/ui/ConfirmDialog/ConfirmDialog.vue",
  "shared/ui/ImageViewer/ImageViewer.vue",
];

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
  for (const file of collect(CLIENT_SRC, (name) => name.endsWith(".vue"))) {
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
    // and a window reaches it directly or through its dialog shell.
    const offenders = WINDOWS.filter((path) => {
      const source = readFileSync(join(CLIENT_SRC, path), "utf8");
      return !/usePageScrollLock\(|useDialogShell\(/.test(source);
    }).map(
      (path) =>
        `${path}: a window that leaves the page under it free to scroll and to jump`,
    );
    expect(offenders).toEqual([]);
  });

  it("asks the editor's insertions in a panel under the button, not in a window", () => {
    // A window is too much for a line of text: going back to one is a
    // decision to take, not a component to swap in.
    const read = (path: string) =>
      readFileSync(join(CLIENT_SRC, "shared/ui/BBCodeEditor", path), "utf8");
    const editor = read("BBCodeEditor.vue");
    expect(editor).toMatch(/^import InsertPanel\b/m);
    expect(editor).not.toMatch(/<(InputDialog|Dialog)\b/);
    expect(modalTag(read("InsertPanel.vue"))).toBeNull();
  });

  it("keeps the vue-final-modal tier inside the viewport", () => {
    // Windows open a form on the fixed 580 and 920 measures, and on a 375px
    // screen an uncapped 580 runs off both edges. The cap is written once, in
    // the card of _DialogSurface.sass.
    const card = ruleMap(
      componentCss(join(CLIENT_SRC, "shared/ui/Layout/Dialog.vue")),
    ).get(".dialog");
    expect(card?.["max-width"]).toMatch(/^calc\(100vw/);
  });
});
