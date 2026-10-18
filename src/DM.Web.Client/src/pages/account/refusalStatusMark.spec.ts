/**
 * @vitest-environment node
 */

/**
 * The mark over a refusal card, and the one thing that decides it.
 *
 * Every page a letter opens ends on a card of the same shape: a mark, a
 * heading, a line, a button. The mark was picked by hand and there was no rule
 * behind it, so two screens that end the same way could wear different signs
 * and the reader had to learn each screen separately.
 *
 * The rule is written where the two marks are defined, at .auth-status--warn in
 * assets/styles/AuthSurface.sass, and it is about what the site can still do
 * rather than about how bad the event is. The warning sign stands where there
 * is a next step and the card itself offers it - every "Ссылка устарела" does,
 * whether the step is a new letter, the recovery dialog or the settings. The
 * cross stands where there is none and the reader has to start from somewhere
 * else: "Ссылка недействительна", "Почта занята".
 *
 * Read out of the templates, because that is where a mark is chosen. The pairs
 * are built by walking the source: a heading takes the mark that stands above
 * it with no other heading in between, so a branch that lost its mark reports
 * none rather than borrowing the mark of the branch before it.
 */
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SHEET = resolve(HERE, "..", "..", "assets", "styles", "AuthSurface.sass");

/** The heading of every refusal the site can take the next step for. */
const NEXT_STEP = "Ссылка устарела";

/** The headings of the refusals it cannot. */
const DEAD_END = ["Ссылка недействительна", "Почта занята"];

/** What each of the two is drawn as. */
const EXPECTED = new Map<string, string>([
  [NEXT_STEP, "warn"],
  ...DEAD_END.map((title) => [title, "error"] as [string, string]),
]);

/** A mark of the guest surface, and a heading of a card, in source order. */
const TOKEN =
  /auth-status--(ok|warn|error)|<h1 class="auth-title">\s*([^<]*?)\s*<\/h1>/g;

type Card = { title: string; mark: string };

/**
 * Every heading of a file with the mark that stands over it. A heading closes
 * the pair, so the next one starts from nothing: two headings in a row mean the
 * second carries no mark of its own.
 */
const cardsIn = (source: string): Card[] => {
  const cards: Card[] = [];
  let pending = "нет знака";

  for (const [, mark, title] of source.matchAll(TOKEN)) {
    if (mark) {
      pending = mark;
      continue;
    }
    cards.push({ title, mark: pending });
    pending = "нет знака";
  }
  return cards;
};

/** Where a card of this file breaks the rule, in words. */
const offendersIn = (source: string, file: string): string[] =>
  cardsIn(source)
    .filter(
      ({ title, mark }) => EXPECTED.has(title) && EXPECTED.get(title) !== mark,
    )
    .map(
      ({ title, mark }) =>
        `${file}: "${title}" несет ${mark}, а по правилу ${EXPECTED.get(title)}`,
    );

const pages = (): { source: string; file: string }[] =>
  readdirSync(HERE)
    .filter((name) => name.endsWith(".vue"))
    .map((name) => ({
      source: readFileSync(join(HERE, name), "utf8"),
      file: name,
    }));

describe("the mark over a refusal card follows the rule", () => {
  const all = pages().flatMap(({ source, file }) =>
    cardsIn(source)
      .filter(({ title }) => EXPECTED.has(title))
      .map((card) => ({ ...card, file })),
  );

  it("finds the cards it judges", () => {
    // A renamed class or a moved folder would leave the assertions below
    // looking at nothing and passing for it. Three screens run out of time and
    // seven answer a link that opens nothing, counting both ends of the mailed
    // removal and both refusals of the address change.
    expect(all.filter(({ title }) => title === NEXT_STEP)).toHaveLength(3);
    expect(all.filter(({ title }) => DEAD_END.includes(title))).toHaveLength(7);
  });

  it("gives every screen the mark its way forward earns", () => {
    expect(
      pages().flatMap(({ source, file }) => offendersIn(source, file)),
    ).toEqual([]);
  });

  it("would catch a screen that broke the rule", () => {
    // The same reading over a card of each kind with the marks exchanged. A
    // green run with a parser that finds nothing would otherwise mean nothing.
    const broken = `
      <div class="auth-status auth-status--error" aria-hidden="true" />
      <h1 class="auth-title">Ссылка устарела</h1>
      <div class="auth-status auth-status--warn" aria-hidden="true">!</div>
      <h1 class="auth-title">Почта занята</h1>
      <h1 class="auth-title">Ссылка недействительна</h1>
    `;

    expect(offendersIn(broken, "выдуманная страница")).toEqual([
      'выдуманная страница: "Ссылка устарела" несет error, а по правилу warn',
      'выдуманная страница: "Почта занята" несет warn, а по правилу error',
      'выдуманная страница: "Ссылка недействительна" несет нет знака, а по правилу error',
    ]);
  });

  it("keeps the rule written where the mark is chosen", () => {
    // Every card of the zone points at one place for the rule, so the rule has
    // to be there. Only the comment block that touches the selector is read,
    // and not the sheet above it: these headings stand elsewhere in the tree,
    // and a search of the whole file would find them after the rule itself had
    // been deleted.
    const lines = readFileSync(SHEET, "utf8").split("\n");
    const at = lines.indexOf(".auth-surface .auth-status--warn");
    expect(at, "в листе больше нет предупреждающего знака").toBeGreaterThan(0);

    const above: string[] = [];
    for (let line = at - 1; line >= 0; line -= 1) {
      if (!lines[line].startsWith("///")) break;
      above.push(lines[line]);
    }
    const rule = above.join("\n");

    for (const title of [NEXT_STEP, ...DEAD_END]) {
      expect(
        rule.includes(`"${title}"`),
        `правило над знаком не называет "${title}"`,
      ).toBe(true);
    }
  });
});
