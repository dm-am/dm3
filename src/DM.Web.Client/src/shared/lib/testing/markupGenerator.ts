/**
 * fast-check arbitraries from the shared alphabet (markupAlphabet.ts): a soup
 * of every kind of piece and markup the grammar accepts whole; and the run of
 * one generated check with its seed and report. Spec-only;
 * docs/guides/TESTING.md, "Генеративные проверки разметки".
 */
import fc from "fast-check";
import { type BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";
import { MARKUP_ALPHABET } from "./markupAlphabet";
import { asBothSidesRead } from "./markupCorpus";

const ALPHABET = MARKUP_ALPHABET;

/** The piece with its placeholders filled: a list of names, a surrogate with no partner. */
function expand(piece: string): fc.Arbitrary<string> {
  const filled = piece
    .replace("{high-surrogate}", "\uD800")
    .replace("{low-surrogate}", "\uDC00");
  return filled.includes("{names}")
    ? fc
        .constantFrom(...ALPHABET.names)
        .map((names) => filled.replace("{names}", names))
    : fc.constant(filled);
}

/** One of the pieces, its placeholders filled. */
function oneOf(pieces: readonly string[]): fc.Arbitrary<string> {
  return fc.oneof(...pieces.map(expand));
}

/** A word: Q, four digits, Q. */
const markupWord: fc.Arbitrary<string> = fc
  .integer({ min: 1, max: 9999 })
  .map((n) => `Q${String(n).padStart(4, "0")}Q`);

const MAX_DEPTH = 4;

/**
 * Pieces of every kind, tags in pairs and now and then left open.
 *
 * @param hiding false to leave out every piece that spells [private], for a
 *   surface where the save path refuses any of it.
 */
export function markupSoup(hiding = true): fc.Arbitrary<string> {
  const allowed = (text: string) =>
    hiding || !asBothSidesRead(text).toLowerCase().includes("private");
  const piece = oneOf(ALPHABET.pieces.filter(allowed));
  const repeat = fc
    .constantFrom(...ALPHABET.repeats.filter(([text]) => allowed(text)))
    .map(([text, times]) => text.repeat(times));
  const pairs = ALPHABET.pairs.filter(([open, close]) => allowed(open + close));
  const leaf = fc
    .tuple(
      fc.constantFrom(...ALPHABET.leaves),
      fc.constantFrom(...ALPHABET.addresses),
    )
    .map(([[open, close], address]) => open + address + close);

  const level = (depth: number): fc.Arbitrary<string> => {
    const items: Array<fc.WeightedArbitrary<string>> = [
      { arbitrary: markupWord, weight: 20 },
      { arbitrary: piece, weight: 40 },
      { arbitrary: repeat, weight: 2 },
      { arbitrary: leaf, weight: 5 },
    ];
    if (depth < MAX_DEPTH) {
      const inner = level(depth + 1);
      items.push({
        weight: 30,
        arbitrary: fc
          .tuple(
            fc.oneof(
              ...pairs.map(([open, close]) =>
                expand(open).map((filled) => [filled, close] as const),
              ),
            ),
            inner,
            fc.integer({ min: 0, max: 7 }),
          )
          .map(
            ([[open, close], held, left]) =>
              open + held + (left === 0 ? "" : close),
          ),
      });
    }
    return fc
      .array(fc.oneof(...items), { minLength: 1, maxLength: 5 })
      .map((parts) => parts.join(""));
  };
  return level(0);
}

/**
 * Markup the grammar accepts whole in the context: pairs closed, lists of
 * items, [mod] where the context has it, [private] in a game post one level
 * deep, [noparse] around anything but its own closing tag.
 */
export function markupValid(context: BBCodeContext): fc.Arbitrary<string> {
  const valid = ALPHABET.valid;
  const hasMod = context === "common" || context === "message";
  const hasPrivate = context === "post";
  const exampleable = ALPHABET.pieces.filter(
    (piece) => !asBothSidesRead(piece).toLowerCase().includes("[/noparse"),
  );
  const example = fc
    .array(fc.oneof(markupWord, oneOf(exampleable)), {
      minLength: 1,
      maxLength: 4,
    })
    .map((parts) => `[noparse]${parts.join("")}[/noparse]`);

  const memo = new Map<string, fc.Arbitrary<string>>();
  const level = (depth: number, inPrivate: boolean): fc.Arbitrary<string> => {
    const key = `${depth}:${inPrivate}`;
    const known = memo.get(key);
    if (known) return known;

    const items: Array<fc.WeightedArbitrary<string>> = [
      { arbitrary: markupWord, weight: 24 },
      { arbitrary: fc.constantFrom(...valid.text), weight: 28 },
      { arbitrary: fc.constantFrom(...valid.leaves), weight: 8 },
      { arbitrary: example, weight: 4 },
    ];
    if (depth < MAX_DEPTH) {
      const inner = level(depth + 1, inPrivate);
      const around = (pairs: Array<[string, string]>, weight: number) =>
        items.push({
          weight,
          arbitrary: fc
            .tuple(fc.constantFrom(...pairs), inner)
            .map(([[open, close], held]) => open + held + close),
        });
      around(valid.pairs, 22);
      const item = fc
        .tuple(fc.constantFrom(...valid.items), inner, fc.boolean())
        .map(
          ([[open, close], held, apart]) =>
            (apart ? "\n" : "") + open + held + close,
        );
      items.push({
        weight: 5,
        arbitrary: fc
          .tuple(
            fc.constantFrom(...valid.lists),
            fc.array(item, { minLength: 1, maxLength: 3 }),
            fc.boolean(),
          )
          .map(
            ([[open, close], held, apart]) =>
              open + held.join("") + (apart ? "\n" : "") + close,
          ),
      });
      if (hasMod) around(valid.mod, 3);
      if (hasPrivate && !inPrivate) {
        items.push({
          weight: 6,
          arbitrary: fc
            .tuple(
              oneOf(valid.private),
              level(depth + 1, true),
              fc.constantFrom(...valid.privateClosings),
            )
            .map(([open, held, close]) => open + held + close),
        });
      }
    }
    const made = fc
      .array(fc.oneof(...items), { minLength: 1, maxLength: 4 })
      .map((parts) => parts.join(""));
    memo.set(key, made);
    return made;
  };
  return level(0, false);
}

/** A seed from a name, the same on every run: FNV-1a over its code units. */
function seedOf(name: string): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < name.length; i++) {
    hash ^= name.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash | 0;
}

/**
 * The parameters of one check: a seed of its own, so every run tries the same
 * texts, and the run a report asks for when DM_MARKUP_SEED (with
 * DM_MARKUP_PATH) is set. DM_MARKUP_RUNS sets how many texts a check tries.
 */
export function markupRuns(name: string, runs: number): fc.Parameters<unknown> {
  const env = process.env;
  const seed = env.DM_MARKUP_SEED ? Number(env.DM_MARKUP_SEED) : seedOf(name);
  return {
    seed,
    numRuns: env.DM_MARKUP_RUNS ? Number(env.DM_MARKUP_RUNS) : runs,
    ...(env.DM_MARKUP_PATH ? { path: env.DM_MARKUP_PATH } : {}),
  };
}

/** A string as a literal that reads back as the same string, for a report. */
export function literal(text: string): string {
  return JSON.stringify(text).replace(
    /[^\x20-\x7eА-я]/g,
    (ch) => `\\u${ch.charCodeAt(0).toString(16).padStart(4, "0")}`,
  );
}
