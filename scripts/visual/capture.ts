import { expect, type Page, type TestInfo } from "@playwright/test";
import { spawnSync } from "child_process";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "fs";
import { dirname } from "path";
import type { Mask } from "./masks";

/**
 * What is pinned in the page before the first script of the application runs.
 *
 * Math.random: the interface draws element ids from it (a dozen components)
 * and the home page picks the testimonial it shows. The values here are tiny
 * and grow by one step per call, so every "pick one of n" lands on the first
 * of them whatever the order of the calls was, and every id is still unique.
 * The ids themselves depend on the order components mounted in, which async
 * data decides; the DOM channel writes them all as one token (RANDOM below).
 *
 * crypto.randomUUID: keys of the attribute schema editor, same reasoning.
 */
export function pinRandomness() {
  let calls = 0;
  Math.random = () => {
    calls = (calls % 99_999) + 1;
    return calls * 1e-6;
  };
  let uuids = 0;
  const hex = (n: number) => n.toString(16).padStart(12, "0");
  if (globalThis.crypto) {
    Object.defineProperty(globalThis.crypto, "randomUUID", {
      value: () => `00000000-0000-4000-8000-${hex(++uuids)}`,
    });
  }
}

/** The tokens pinRandomness hands out, as `Math.random().toString(36).slice(2, 9)` cuts them. */
const RANDOM_TOKENS = Array.from({ length: 20_000 }, (_, i) =>
  ((i + 1) * 1e-6).toString(36).slice(2, 9),
);

/**
 * Waits until the page has nothing left to load: no request in flight, fonts
 * and images decoded, and two frames painted after that. toHaveScreenshot then
 * adds its own wait for two identical frames.
 *
 * A lazy image below the window never starts loading, so only the ones in the
 * window are waited for; the window is stretched over the page before the
 * capture, and the settle after that brings the rest in.
 */
export async function settle(page: Page) {
  await page.waitForLoadState("networkidle");
  await page.evaluate(async () => {
    await document.fonts.ready;
    const inWindow = (image: HTMLImageElement) => {
      const box = image.getBoundingClientRect();
      return (
        box.bottom >= 0 &&
        box.top <= innerHeight &&
        box.right >= 0 &&
        box.left <= innerWidth
      );
    };
    await Promise.all(
      Array.from(document.images)
        .filter(
          (image) =>
            !image.complete && (image.loading !== "lazy" || inWindow(image)),
        )
        .map(
          (image) =>
            new Promise((done) => {
              image.addEventListener("load", done, { once: true });
              image.addEventListener("error", done, { once: true });
            }),
        ),
    );
    await new Promise((done) =>
      requestAnimationFrame(() => requestAnimationFrame(done)),
    );
  });
  await page.waitForLoadState("networkidle");
}

/**
 * Waits until the tree has not changed for a while. A page runs short-lived
 * effects on timers of its own - the post a room opens on wears the arrival
 * pulse for two seconds (useScrollToElement) - and a capture inside that
 * window or after it depended on how fast the machine was. The page's clock is
 * fixed and its timers are not, so the quiet is counted in ticks of a timer
 * rather than read off a clock.
 */
async function quiet(page: Page, still = 2_500, longest = 15_000) {
  const tick = 100;
  await page.evaluate(
    ({ ticksStill, ticksLongest, tick }) =>
      new Promise<void>((done) => {
        let sinceChange = 0;
        let total = 0;
        const observer = new MutationObserver(() => {
          sinceChange = 0;
        });
        observer.observe(document.body, {
          subtree: true,
          childList: true,
          attributes: true,
          characterData: true,
        });
        const check = () => {
          sinceChange++;
          total++;
          if (sinceChange >= ticksStill || total >= ticksLongest) {
            observer.disconnect();
            done();
          } else setTimeout(check, tick);
        };
        setTimeout(check, tick);
      }),
    { ticksStill: still / tick, ticksLongest: longest / tick, tick },
  );
}

/** The attribute the masked elements carry while a state is captured. */
const MASKED = "data-visual-mask";

/**
 * The dates of the run's own day as the interface writes them (dd.mm.yyyy),
 * the day before and after included: the zone the page writes them in is the
 * account's, which may be a day away from the machine's.
 */
function runDates(): string[] {
  return [-1, 0, 1].map((days) => {
    const day = new Date(Date.now() + days * 24 * 60 * 60_000);
    const two = (n: number) => String(n).padStart(2, "0");
    return `${two(day.getUTCDate())}.${two(day.getUTCMonth() + 1)}.${day.getUTCFullYear()}`;
  });
}

/**
 * Marks the elements the masks of a state cover, so the three channels agree
 * on what is hidden: the frame paints them over, the tree writes them as
 * "[masked]", the styles skip them. A mask restricted to the run's dates
 * covers an element only while its text holds one.
 */
async function markMasks(page: Page, masks: Mask[]) {
  await page.evaluate(
    ({ masks, dates, marker }) => {
      for (const mask of masks) {
        document.querySelectorAll(mask.selector).forEach((element) => {
          const text = element.textContent ?? "";
          if (!mask.runDate || dates.some((date) => text.includes(date))) {
            element.setAttribute(marker, "");
          }
        });
      }
    },
    {
      masks: masks.map((m) => ({ selector: m.selector, runDate: !!m.runDate })),
      dates: runDates(),
      marker: MASKED,
    },
  );
}

/**
 * The DOM channel: the body as a tree, one node per line, attributes sorted,
 * without the scoped-style hashes (data-v-*), comments (v-if anchors) and the
 * random ids. Text nodes are quoted so that a space gained or lost between two
 * inline elements is a line of the diff; form controls carry their live value,
 * which outerHTML does not.
 */
async function domOf(page: Page): Promise<string> {
  return page.evaluate(
    ({ tokens, marker }) => {
      const random = new Set(tokens);
      const unrandom = (value: string) =>
        value.replace(/[0-9a-z]{7}/g, (piece) =>
          random.has(piece) ? "RANDOM" : piece,
        );
      const lines: string[] = [];
      const walk = (node: Node, depth: number) => {
        const pad = "  ".repeat(depth);
        if (node.nodeType === Node.TEXT_NODE) {
          const text = node.textContent ?? "";
          if (
            text.trim() ||
            (text && node.parentElement?.closest("pre, textarea"))
          )
            lines.push(pad + JSON.stringify(text));
          else if (text)
            lines.push(pad + JSON.stringify(text.includes("\n") ? "\n" : " "));
          return;
        }
        if (node.nodeType !== Node.ELEMENT_NODE) return;
        const element = node as Element;
        const tag = element.tagName.toLowerCase();
        if (tag === "script" || tag === "style" || tag === "noscript") return;
        const attributes = Array.from(element.attributes)
          .filter((a) => !a.name.startsWith("data-v-") && a.name !== marker)
          .map((a) => [a.name, unrandom(a.value)] as const);
        if (element instanceof HTMLInputElement) {
          if (element.type === "checkbox" || element.type === "radio")
            attributes.push([":checked", String(element.checked)]);
          else attributes.push([":value", element.value]);
        } else if (
          element instanceof HTMLTextAreaElement ||
          element instanceof HTMLSelectElement
        ) {
          attributes.push([":value", element.value]);
        }
        attributes.sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
        const open = [
          tag,
          ...attributes.map(
            ([name, value]) => `${name}=${JSON.stringify(value)}`,
          ),
        ].join(" ");
        if (element.hasAttribute(marker)) {
          lines.push(`${pad}<${tag}> [masked]`);
          return;
        }
        lines.push(`${pad}<${open}>`);
        element.childNodes.forEach((child) => walk(child, depth + 1));
        if (element.shadowRoot)
          element.shadowRoot.childNodes.forEach((child) =>
            walk(child, depth + 1),
          );
      };
      walk(document.body, 0);
      return `${lines.join("\n")}\n`;
    },
    { tokens: RANDOM_TOKENS, marker: MASKED },
  );
}

/** What of an element's computed style the style channel records. */
const PROPERTIES = [
  "display",
  "position",
  "top",
  "right",
  "bottom",
  "left",
  "z-index",
  "box-sizing",
  "width",
  "height",
  "min-width",
  "max-width",
  "min-height",
  "margin-top",
  "margin-right",
  "margin-bottom",
  "margin-left",
  "padding-top",
  "padding-right",
  "padding-bottom",
  "padding-left",
  "border-top",
  "border-right",
  "border-bottom",
  "border-left",
  "border-radius",
  "outline",
  "box-shadow",
  "background-color",
  "background-image",
  "opacity",
  "visibility",
  "color",
  "font-family",
  "font-size",
  "font-weight",
  "font-style",
  "font-variant-caps",
  "line-height",
  "letter-spacing",
  "word-spacing",
  "text-transform",
  "text-decoration-line",
  "text-align",
  "text-indent",
  "white-space",
  "word-break",
  "hyphens",
  "vertical-align",
  "overflow-x",
  "overflow-y",
  "text-overflow",
  "cursor",
  "user-select",
  "transform",
  "flex-direction",
  "flex-wrap",
  "align-items",
  "justify-content",
  "gap",
  "grid-template-columns",
  "list-style-type",
  "table-layout",
  "border-collapse",
];

/**
 * The style channel: the computed style of the key nodes of a state and of
 * their ::before and ::after. Nodes are found by tag, landmark and role rather
 * than by class, so a class renamed in a clean-up still finds its node and the
 * diff says what moved, not that something went missing.
 */
async function stylesOf(page: Page, keys: string[]): Promise<string> {
  return page.evaluate(
    ({ keys, properties, marker }) => {
      const out: string[] = [];
      const inMask = (element: Element) => element.closest(`[${marker}]`);
      for (const key of keys) {
        const found = Array.from(document.querySelectorAll(key))
          .filter((e) => !inMask(e))
          .slice(0, 4);
        if (found.length === 0) {
          out.push(`${key}: none`);
          continue;
        }
        found.forEach((element, index) => {
          // The node's own words, without those of a masked descendant.
          const copy = element.cloneNode(true) as Element;
          copy.querySelectorAll(`[${marker}]`).forEach((m) => m.remove());
          const text = (copy.textContent ?? "")
            .replace(/\s+/g, " ")
            .trim()
            .slice(0, 40);
          out.push(
            `${key} #${index} <${element.tagName.toLowerCase()}> ${JSON.stringify(text)}`,
          );
          const style = getComputedStyle(element);
          for (const property of properties)
            out.push(`  ${property}: ${style.getPropertyValue(property)}`);
          for (const pseudo of ["::before", "::after"]) {
            const content = getComputedStyle(element, pseudo).getPropertyValue(
              "content",
            );
            if (content && content !== "none" && content !== "normal") {
              const ps = getComputedStyle(element, pseudo);
              out.push(
                `  ${pseudo}: content ${content}; color ${ps.color}; font-size ${ps.fontSize}; width ${ps.width}; height ${ps.height}; background-color ${ps.backgroundColor}; border ${ps.borderTop}`,
              );
            }
          }
        });
      }
      return `${out.join("\n")}\n`;
    },
    { keys, properties: PROPERTIES, marker: MASKED },
  );
}

/**
 * A text channel written in a snapshot run and compared in a diff run. The
 * actual file and a unified diff go next to the report, so the difference can
 * be read without rerunning anything.
 */
function compareText(info: TestInfo, name: string, actual: string): boolean {
  const base = info.snapshotPath(name);
  if (process.env.VISUAL_MODE !== "diff") {
    mkdirSync(dirname(base), { recursive: true });
    writeFileSync(base, actual);
    return true;
  }
  if (existsSync(base) && readFileSync(base, "utf8") === actual) return true;
  const actualPath = info.outputPath(name.replace(/(\.\w+)$/, ".actual$1"));
  mkdirSync(dirname(actualPath), { recursive: true });
  writeFileSync(actualPath, actual);
  if (existsSync(base)) {
    const diff = spawnSync(
      "git",
      ["diff", "--no-index", "--no-color", "--text", base, actualPath],
      { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
    );
    writeFileSync(info.outputPath(`${name}.diff`), diff.stdout);
  }
  return false;
}

/** Whether two DOM texts differ only in their class attributes. */
function onlyClasses(base: string, actual: string): boolean {
  const strip = (text: string) => text.replace(/ class="(?:[^"\\]|\\.)*"/g, "");
  return strip(base) === strip(actual);
}

/**
 * The three channels of one state: the frame, the tree and the styles.
 * Soft, so one channel that differs does not hide whether the other two do;
 * each difference is also recorded by channel for the report.
 */
export async function captureState(
  page: Page,
  info: TestInfo,
  name: string,
  keys: string[],
  masks: Mask[],
) {
  const differs = (channel: string, detail = "") =>
    info.annotations.push({
      type: "differs",
      description: detail ? `${channel} (${detail})` : channel,
    });

  await quiet(page);
  await markMasks(page, masks);
  const errorsBefore = info.errors.length;
  await expect.soft(page).toHaveScreenshot(`${name}.png`, {
    fullPage: true,
    mask: [page.locator(`[${MASKED}]`)],
    timeout: 60_000,
  });
  if (info.errors.length > errorsBefore) differs("png");

  const dom = await domOf(page);
  if (!compareText(info, `${name}.dom.html`, dom)) {
    const basePath = info.snapshotPath(`${name}.dom.html`);
    const detail =
      existsSync(basePath) && onlyClasses(readFileSync(basePath, "utf8"), dom)
        ? "только class"
        : "";
    differs("dom", detail);
    expect.soft(false, `DOM of ${name} differs from the base`).toBe(true);
  }

  const styles = await stylesOf(page, keys);
  if (!compareText(info, `${name}.style.txt`, styles)) {
    differs("style");
    expect
      .soft(false, `computed style of ${name} differs from the base`)
      .toBe(true);
  }
}
