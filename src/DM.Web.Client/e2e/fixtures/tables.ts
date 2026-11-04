/**
 * The tables of the site (DataTable) as a reader finds them, one rule for
 * every spec that reads a table.
 *
 * Every column stands at every width: none is left out of the header or of a
 * row. A word of a cell stays whole: a cell breaks its text
 * between words only, never inside a word and never with a hyphen. Where the
 * column of the page has the room, the table fits its frame. Where even the
 * least width of the table, every word whole, is wider than the frame, the
 * table keeps that least width and its frame scrolls sideways under it, and
 * only there: a frame that scrolls a table that would fit, or a table wider
 * than both its frame and its least width, is a fault.
 *
 * Each reader below runs in the page, so it is one function with no outside
 * names; readTables runs them together at one moment (fixtures/inPage.ts).
 */

import type { Page } from "@playwright/test";
import { readInPage } from "./inPage";

/** What the tables inside a part of the page show, and what is wrong there. */
export interface TableReading {
  /** The headers of each table drawn there, in order. */
  headers: string[][];
  problems: string[];
}

/** The part of the page the tables are read in, a selector. */
interface Within {
  within: string;
}

/**
 * The text under visibility: hidden in a cell, read by the words and by the
 * headers and the cells as well: it is laid out like the text seen, and takes
 * the width of its column the same.
 */
interface Hidden {
  withHidden?: boolean;
}

/** What a reader finds wrong with each table drawn in the part, in order. */
type EachTable = string[][];

/**
 * The columns: the headers of each table, and every column there, in the
 * header and in every row. Null where nothing stands at `within`.
 */
function readColumns({
  within,
}: Within): { headers: string[][]; problems: EachTable } | null {
  const root = document.querySelector(within);
  if (!root) return null;
  const drawn = (el: Element) =>
    el.getClientRects().length > 0 &&
    getComputedStyle(el).visibility !== "hidden";
  const label = (el: Element) =>
    (el.textContent ?? "").trim().replace(/\s+/g, " ");
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;

  const headers: string[][] = [];
  const problems: EachTable = [];
  for (const table of root.querySelectorAll("table.data-table")) {
    if (!drawn(table)) continue;
    const found: string[] = [];
    const ths = [...table.querySelectorAll<HTMLElement>("thead th")];
    headers.push(ths.map(label));
    for (const th of ths) {
      if (!drawn(th) || th.getBoundingClientRect().width < 1)
        found.push(`the column "${label(th)}" is left out`);
    }
    for (const row of table.querySelectorAll("tr.table-row")) {
      const cells = [...row.children];
      if (cells.length !== ths.length) {
        found.push(
          `a row of ${name(table)} has ${cells.length} cells under ${ths.length} headers`,
        );
        continue;
      }
      cells.forEach((cell, index) => {
        if (!drawn(cell))
          found.push(`a cell of the column "${label(ths[index])}" is left out`);
      });
    }
    problems.push(found);
  }
  return { headers, problems };
}

/** The words of the cells: each whole, and none hyphenated. */
function readWords({ within, withHidden }: Within & Hidden): EachTable {
  const root = document.querySelector(within);
  if (!root) return [];
  const drawn = (el: Element) =>
    el.getClientRects().length > 0 &&
    getComputedStyle(el).visibility !== "hidden";
  const read = (el: Element) =>
    withHidden ? el.getClientRects().length > 0 : drawn(el);
  const text = (el: Element) =>
    (el.textContent ?? "").trim().replace(/\s+/g, " ").slice(0, 40);
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;

  /** The words of a cell's text that stand on more than one line. */
  const brokenWords = (cell: Element) => {
    const broken: string[] = [];
    const walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (!withHidden && !(node.parentElement && drawn(node.parentElement)))
        continue;
      // A word, with the break after a hyphen or a slash allowed.
      const pattern = /[^\s\-‐-―/]+[-‐-―/]?/g;
      for (const match of (node as Text).data.matchAll(pattern)) {
        if (match[0].length < 2) continue;
        const range = document.createRange();
        range.setStart(node, match.index!);
        range.setEnd(node, match.index! + match[0].length);
        const lines = new Set(
          [...range.getClientRects()]
            .filter((r) => r.width > 0.5)
            .map((r) => Math.round(r.top)),
        );
        if (lines.size > 1) broken.push(match[0]);
      }
    }
    return broken;
  };

  const tables = [...root.querySelectorAll("table.data-table")].filter(drawn);
  return tables.map((table) => {
    const found: string[] = [];
    for (const cell of table.querySelectorAll<HTMLElement>("th, td")) {
      if (!read(cell)) continue;
      if (getComputedStyle(cell).hyphens !== "none")
        found.push(
          `${name(cell)} "${text(cell)}" hyphenates: ${getComputedStyle(cell).hyphens}`,
        );
      for (const word of brokenWords(cell))
        found.push(`${name(cell)} breaks "${word}"`);
    }
    return found;
  });
}

/**
 * The frame: it scrolls only a table wider than it at the table's least
 * width, every word whole, and no table is wider than both.
 */
function readFrame({ within }: Within): EachTable {
  const root = document.querySelector(within);
  if (!root) return [];
  const drawn = (el: Element) =>
    el.getClientRects().length > 0 &&
    getComputedStyle(el).visibility !== "hidden";
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;

  const tables = [
    ...root.querySelectorAll<HTMLTableElement>("table.data-table"),
  ].filter(drawn);
  return tables.map((table) => {
    const found: string[] = [];
    // The least width of the table, every word whole: an auto table is never
    // narrower than what its columns need, so at no width of its own it takes
    // exactly that. Read and put back at once, before anything is drawn.
    const frame = table.closest(".data-table-scroll") ?? table.parentElement!;
    const style = table.getAttribute("style");
    table.style.setProperty("width", "0px", "important");
    table.style.setProperty("min-width", "0px", "important");
    const least = table.getBoundingClientRect().width;
    if (style === null) table.removeAttribute("style");
    else table.setAttribute("style", style);
    const width = table.getBoundingClientRect().width;
    const room = frame.clientWidth;
    if (frame.scrollWidth > room + 1 && least <= room + 1)
      found.push(
        `the frame of ${name(table)} scrolls (${frame.scrollWidth} > ${room}) though the table holds every word whole in ${Math.round(least)}`,
      );
    if (width > Math.max(room, least) + 1)
      found.push(
        `${name(table)} is ${Math.round(width)} wide where ${Math.round(Math.max(room, least))} holds every word whole`,
      );
    return found;
  });
}

/**
 * The headers and the cells: the text of each header stays inside its own
 * cell, so no two of them meet, and no cell holds more than its width.
 */
function readHeads({ within, withHidden }: Within & Hidden): EachTable {
  const root = document.querySelector(within);
  if (!root) return [];
  const shown = (el: Element) =>
    el.getClientRects().length > 0 &&
    getComputedStyle(el).visibility !== "hidden";
  const read = (el: Element) =>
    withHidden ? el.getClientRects().length > 0 : shown(el);
  const text = (el: HTMLElement) =>
    el.innerText.trim().replace(/\s+/g, " ").slice(0, 40);
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}.${[...el.classList].join(".")}`;
  /** The boxes of the text a cell draws, one per line. */
  const textBoxes = (cell: Element) => {
    const range = document.createRange();
    range.selectNodeContents(cell);
    return [...range.getClientRects()].filter((r) => r.width > 0.5);
  };
  const meet = (a: DOMRect, b: DOMRect) =>
    a.left < b.right - 0.5 &&
    b.left < a.right - 0.5 &&
    a.top < b.bottom - 0.5 &&
    b.top < a.bottom - 0.5;

  const tables = [...root.querySelectorAll("table.data-table")].filter(shown);
  return tables.map((table) => {
    const found: string[] = [];
    const heads = [...table.querySelectorAll<HTMLElement>("thead th")]
      .filter(read)
      .map((th) => ({ th, boxes: textBoxes(th) }));
    for (const { th, boxes } of heads) {
      const cell = th.getBoundingClientRect();
      if (
        boxes.some(
          (r) => r.left < cell.left - 0.5 || r.right > cell.right + 0.5,
        )
      )
        found.push(`the header "${text(th)}" runs out of its cell`);
    }
    for (let i = 0; i < heads.length; i++) {
      for (let j = i + 1; j < heads.length; j++) {
        if (heads[i].boxes.some((a) => heads[j].boxes.some((b) => meet(a, b))))
          found.push(
            `the headers "${text(heads[i].th)}" and "${text(heads[j].th)}" run into each other`,
          );
      }
    }
    // Content running past the cell's own edge, clipped or not.
    for (const cell of table.querySelectorAll<HTMLElement>("th, td")) {
      if (read(cell) && cell.scrollWidth > cell.clientWidth + 1)
        found.push(
          `${name(cell)} "${text(cell)}" overflows: ${cell.scrollWidth} > ${cell.clientWidth}`,
        );
    }
    return found;
  });
}

/** A reader left out of the reading: it finds nothing in any table. */
const unread = (): EachTable => [];

/**
 * Reads every DataTable drawn inside `within` (a selector): its columns and
 * its frame. `words` adds the reading of the words of every cell, whole and
 * not hyphenated, and `heads` that of the headers and the cells, each inside
 * its own cell; `withHidden` reads the text under visibility: hidden there
 * too. All of it is read at one moment, and what is wrong is told table after
 * table, in that order.
 */
export async function readTables(
  page: Page,
  {
    within,
    words,
    heads = false,
    withHidden = false,
  }: Within & Hidden & { words: boolean; heads?: boolean },
): Promise<TableReading> {
  const [columns, wordsOf, frames, headsOf] = await readInPage(
    page,
    [
      readColumns,
      words ? readWords : unread,
      readFrame,
      heads ? readHeads : unread,
    ],
    { within, withHidden },
  );
  if (!columns)
    return { headers: [], problems: [`nothing stands at ${within}`] };
  const problems = columns.problems.flatMap((found, index) => [
    ...found,
    ...(wordsOf[index] ?? []),
    ...(frames[index] ?? []),
    ...(headsOf[index] ?? []),
  ]);
  return { headers: columns.headers, problems };
}
