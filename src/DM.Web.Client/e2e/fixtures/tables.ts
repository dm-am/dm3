/**
 * The tables of the site (DataTable) as a reader finds them, one rule for
 * every spec that reads a table.
 *
 * Every column stands at every width, a phone included: none is left out of
 * the header or of a row. A word of a cell stays whole: a cell breaks its text
 * between words only, never inside a word and never with a hyphen. Where the
 * column of the page has the room, the table fits its frame. Where even the
 * least width of the table, every word whole, is wider than the frame, the
 * table keeps that least width and its frame scrolls sideways under it, and
 * only there: a frame that scrolls a table that would fit, or a table wider
 * than both its frame and its least width, is a fault.
 *
 * Scrolling in the frame rather than breaking words inside to fit is the
 * choice that stands while the owner's answer on tables wider than the column
 * is open (the sheet of 13 October 2026). It is how DataTable behaved before
 * the wave of 5 October (2b7a7e8a); words broken to fit cut a title of /games
 * at 1366 into "Королевс" and "тв".
 */

/** What the tables inside a part of the page show, and what is wrong there. */
export interface TableReading {
  /** The headers of each table drawn there, in order. */
  headers: string[][];
  problems: string[];
}

/**
 * Reads every DataTable drawn inside `within` (a selector). `words` adds the
 * reading of the words of every cell: whole, and not hyphenated. Read in the
 * page, so it is one function with no outside names.
 */
export function readTables({
  within,
  words,
}: {
  within: string;
  words: boolean;
}): TableReading {
  const headers: string[][] = [];
  const problems: string[] = [];
  const root = document.querySelector(within);
  if (!root) return { headers, problems: [`nothing stands at ${within}`] };

  const drawn = (el: Element) =>
    el.getClientRects().length > 0 &&
    getComputedStyle(el).visibility !== "hidden";
  const label = (el: Element) =>
    (el.textContent ?? "").trim().replace(/\s+/g, " ");
  const text = (el: Element) => label(el).slice(0, 40);
  const name = (el: Element) =>
    `${el.tagName.toLowerCase()}${[...el.classList].map((c) => `.${c}`).join("")}`;

  /** The words of a cell's text that stand on more than one line. */
  const brokenWords = (cell: Element) => {
    const broken: string[] = [];
    const walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (!node.parentElement || !drawn(node.parentElement)) continue;
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

  for (const table of root.querySelectorAll<HTMLTableElement>(
    "table.data-table",
  )) {
    if (!drawn(table)) continue;
    const ths = [...table.querySelectorAll<HTMLElement>("thead th")];
    headers.push(ths.map(label));

    // Every column, in the header and in every row.
    for (const th of ths) {
      if (!drawn(th) || th.getBoundingClientRect().width < 1)
        problems.push(`the column "${label(th)}" is left out`);
    }
    for (const row of table.querySelectorAll("tr.table-row")) {
      const cells = [...row.children];
      if (cells.length !== ths.length) {
        problems.push(
          `a row of ${name(table)} has ${cells.length} cells under ${ths.length} headers`,
        );
        continue;
      }
      cells.forEach((cell, index) => {
        if (!drawn(cell))
          problems.push(
            `a cell of the column "${label(ths[index])}" is left out`,
          );
      });
    }

    if (words)
      for (const cell of table.querySelectorAll<HTMLElement>("th, td")) {
        if (!drawn(cell)) continue;
        if (getComputedStyle(cell).hyphens !== "none")
          problems.push(
            `${name(cell)} "${text(cell)}" hyphenates: ${getComputedStyle(cell).hyphens}`,
          );
        for (const word of brokenWords(cell))
          problems.push(`${name(cell)} breaks "${word}"`);
      }

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
      problems.push(
        `the frame of ${name(table)} scrolls (${frame.scrollWidth} > ${room}) though the table holds every word whole in ${Math.round(least)}`,
      );
    if (width > Math.max(room, least) + 1)
      problems.push(
        `${name(table)} is ${Math.round(width)} wide where ${Math.round(Math.max(room, least))} holds every word whole`,
      );
  }
  return { headers, problems };
}
