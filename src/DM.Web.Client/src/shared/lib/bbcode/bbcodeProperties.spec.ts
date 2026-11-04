/**
 * @vitest-environment jsdom
 */

/**
 * The editor's converter held to the server on texts nobody wrote down: texts
 * generated from the shared alphabet (markupAlphabet.ts) and a corpus with the
 * server's answers (markupCorpus.golden.json, written by BbMarkupCorpusShould).
 * The preview shows what the page shows, the check names refusals where the
 * save refuses, every text comes back from the preview as typed, and reading
 * is linear. The allowed differences are those in
 * the header of bbcodeToHtml.ts. A failure names the smallest text and its
 * seed (docs/guides/TESTING.md, "Генеративные проверки разметки").
 */

import { describe, expect, it } from "vitest";
import fc from "fast-check";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { htmlToBbcode } from "./htmlToBbcode";
import { validateBBCode } from "./bbcodeWarnings";
import { type BBCodeContext } from "./bbcodeTagSet";
import {
  CONTEXT_OF,
  asBothSidesRead,
  readAddressVerdicts,
  readCorpus,
  refusesSave,
  type CorpusEntry,
} from "@/shared/lib/testing/markupCorpus";
import {
  inThePreview,
  onThePage,
  opensAnItemInsideAnItem,
  rebuiltByTheBrowser,
} from "@/shared/lib/testing/markupReading";
import {
  literal,
  markupRuns,
  markupSoup,
  markupValid,
} from "@/shared/lib/testing/markupGenerator";

const corpus = readCorpus();

const CONTEXTS = Object.values(CONTEXT_OF);

/** What the preview shows, read into runs. */
function preview(input: string, context: BBCodeContext) {
  return inThePreview(bbcodeToHtml(input, { context }));
}

/** The line of the text both sides read that the character, counted from 1, stands on. */
function lineOf(input: string, character: number): number {
  return asBothSidesRead(input)
    .slice(0, character - 1)
    .split("\n").length;
}

/** The entries of the corpus with the id each is reported by. */
function entries(
  keep: (entry: CorpusEntry) => boolean,
): Array<[string, CorpusEntry]> {
  return corpus
    .map((entry, index): [string, CorpusEntry] => [
      `#${index} ${entry.surface} ${literal(entry.input).slice(0, 60)}`,
      entry,
    ])
    .filter(([, entry]) => keep(entry));
}

describe("the corpus the server wrote", () => {
  it("is read, with texts of every surface and of both kinds", () => {
    expect(corpus.length).toBeGreaterThanOrEqual(200);
    expect(new Set(corpus.map((entry) => entry.surface)).size).toBe(5);
    expect(corpus.some((entry) => entry.valid)).toBe(true);
    expect(corpus.some((entry) => entry.refusal !== null)).toBe(true);
  });

  // A hidden block on a surface that has none is the author's own text in the
  // preview and erased from the page, and a text too deep to render is shown
  // to its author as typed: the two are left out, and everything else of the
  // text is compared.
  it.each(
    entries(
      (entry) =>
        entry.display !== null &&
        (entry.surface === "GamePost" || entry.refusal === null),
    ),
  )("%s: the preview shows what the page shows", (_id, entry) => {
    const page = entry.display as string;
    if (rebuiltByTheBrowser(page)) {
      // The browser reads the page into another tree than the server wrote.
      // The one known way, an item opened inside another item with no list
      // between, is an open question (docs/guides/TESTING.md); any other fails.
      expect(
        opensAnItemInsideAnItem(page),
        `the browser rebuilds the page: ${page}`,
      ).toBe(true);
      return;
    }
    expect(preview(entry.input, CONTEXT_OF[entry.surface])).toEqual(
      onThePage(page),
    );
  });

  it.each(entries(() => true))(
    "%s: the check names a refusal where the save path refuses",
    (_id, entry) => {
      const issues = validateBBCode(entry.input, CONTEXT_OF[entry.surface]);
      const refusals = issues.filter(refusesSave);
      // Too deep to render: the limit of nesting refuses it on save, whatever
      // else is in it, and the check reads no further than the depth it met.
      if (entry.display === null) {
        expect(issues.map((issue) => issue.reason)).toContain("too-deep");
        return;
      }
      if (entry.refusal === null) {
        expect(
          refusals.map((issue) => issue.message),
          "the save path takes it",
        ).toEqual([]);
      } else {
        expect(
          refusals.length,
          `the save path refuses: ${entry.refusal}`,
        ).toBeGreaterThan(0);
        expect(entry.refusalAt, `${entry.refusal} names no place`).not.toBe(
          null,
        );
        const line = lineOf(entry.input, entry.refusalAt as number);
        expect(
          refusals.map((issue) => issue.line),
          `${entry.refusal} is on line ${line}`,
        ).toContain(line);
      }
      if (entry.valid) expect(issues).toEqual([]);
    },
  );
});

describe("the addresses the server takes", () => {
  const verdicts = readAddressVerdicts();

  it("are read, each of the alphabet", () => {
    expect(verdicts.length).toBeGreaterThanOrEqual(60);
  });

  // The editor reads an address by the browser's URL, the server by .NET's
  // Uri, and the two take different shapes: "https:///a.com/" was a link in
  // the preview and text on the page.
  it.each(verdicts.map((verdict) => [literal(verdict.address), verdict]))(
    "%s: the preview draws the tags the page draws",
    (_address, verdict) => {
      const drawn = (tag: string) =>
        bbcodeToHtml(`[${tag}]${verdict.address}[/${tag}]`, {
          context: "common",
        }).includes(`data-bb-tag="${tag}"`);
      expect({
        link: drawn("link"),
        img: drawn("img"),
        mention: drawn("mention"),
      }).toEqual({
        link: verdict.link,
        img: verdict.img,
        mention: verdict.mention,
      });
    },
  );
});

describe("generated texts", () => {
  it("come back from the preview as typed, in every context", () => {
    fc.assert(
      fc.property(
        markupSoup(),
        fc.constantFrom(...CONTEXTS),
        (text, context) => {
          // A CR is a line break the way the editor holds text: a textarea
          // and the HTML parser both turn it into LF.
          const typed = asBothSidesRead(text).replace(/\r\n?/g, "\n");
          const back = htmlToBbcode(bbcodeToHtml(text, { context }));
          // Line breaks at the very end are nothing a reader sees, and the
          // visual mode keeps none there.
          if (back !== typed && back !== typed.replace(/\n+$/, "")) {
            throw new Error(
              `${literal(text)} in ${context} comes back as ${literal(back)}`,
            );
          }
        },
      ),
      markupRuns("preview round trip", 400),
    );
  });

  it("never make the converter or the check throw", () => {
    fc.assert(
      fc.property(
        markupSoup(),
        fc.constantFrom(...CONTEXTS),
        (text, context) => {
          try {
            validateBBCode(text, context);
            htmlToBbcode(bbcodeToHtml(text, { context }));
          } catch (error) {
            throw new Error(
              `${literal(text)} in ${context}: ${String(error)}`,
              {
                cause: error,
              },
            );
          }
        },
      ),
      markupRuns("no throw", 400),
    );
  });

  it.each(CONTEXTS)(
    "that the grammar accepts whole pass the check in silence, in %s",
    (context) => {
      fc.assert(
        fc.property(markupValid(context), (text) => {
          expect(validateBBCode(text, context), literal(text)).toEqual([]);
        }),
        markupRuns(`valid in ${context}`, 200),
      );
    },
  );
});

/**
 * The steepest step in cost across three sizes four times apart: about 4 for
 * linear, 16 for a square; best of rounds, sizes in turn, as BbParserCostShould.
 */
function steepestStep(work: (text: string) => void, texts: string[]): number {
  const best = texts.map(() => Number.POSITIVE_INFINITY);
  texts.forEach((text) => work(text));
  for (let round = 0; round < 5; round++) {
    texts.forEach((text, size) => {
      const start = performance.now();
      for (let repetition = 0; repetition < 3; repetition++) work(text);
      best[size] = Math.min(best[size], performance.now() - start);
    });
  }
  let steepest = 0;
  for (let size = 1; size < best.length; size++) {
    steepest = Math.max(steepest, best[size] / Math.max(best[size - 1], 0.01));
  }
  return steepest;
}

describe("the cost of reading", () => {
  // Generated soups, each written out again and again until the text is some
  // tens of kilobytes. One that leaves tags open is passed over: repeated, it
  // nests past the limit of depth, and the limit is then what gets measured.
  // The reading is what is timed, the check and the preview; the way back is
  // the parse of the DOM, which is the browser's cost and not this module's.
  it("grows in proportion to the text, for generated texts written out again and again", () => {
    const chunks: string[] = [];
    for (const chunk of fc.sample(markupSoup(), {
      seed: 20260926,
      numRuns: 200,
    })) {
      if (chunks.length === 2) break;
      if (chunk.length < 40) continue;
      const largest = chunk.repeat(Math.ceil(96000 / chunk.length));
      const tooDeep = validateBBCode(largest, "post").some(
        (issue) => issue.reason === "too-deep",
      );
      if (!tooDeep) chunks.push(chunk);
    }
    expect(chunks).toHaveLength(2);
    for (const chunk of chunks) {
      const times = Math.max(1, Math.floor(6000 / chunk.length));
      const texts = [times, times * 4, times * 16].map((n) => chunk.repeat(n));
      const step = steepestStep((text) => {
        validateBBCode(text, "post");
        bbcodeToHtml(text, { context: "post" });
      }, texts);
      expect(
        step,
        `the cost of ${literal(chunk)} written out again and again`,
      ).toBeLessThan(8);
    }
    // Five timed rounds over texts of up to a hundred kilobytes: seconds alone,
    // past the 15s default under a coverage run that saturates every core. The
    // ceiling bounds how long it may take, the ratio above is the check.
  }, 120_000);
});
