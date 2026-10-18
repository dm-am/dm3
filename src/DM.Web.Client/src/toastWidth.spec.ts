/**
 * @vitest-environment node
 */

/**
 * Every toast is one line, and its box is as wide as that line.
 *
 * A toast hugs its own words: the stack is right-aligned and each panel takes
 * the width of its sentence. That holds only while the sentence fits the cap.
 * Past the cap the words wrap, the box stays at the cap, and the reader gets a
 * panel with half a line of nothing down its right side - the defect the owner
 * asked to end for good on 20 September 2026.
 *
 * The cap in ToastContainer.vue is set above the longest line any message can
 * be, so on a desktop nothing wraps. This check is the other half of that: it
 * holds the messages to the length the cap was set for. Without it the cap is
 * a number that the next message quietly outgrows.
 *
 * THE NUMBER. Toast text is 14px PT Sans, measured on a running stand: the
 * longest message the site can raise is 421 pixels wide at 64 characters, so
 * Russian copy runs about 6.6 pixels to the character. Taking 8.0 as the upper
 * bound for a line of unusually wide letters, MAX characters is 512 pixels of
 * text; the panel adds 56 for its padding, the gap and the dismiss button, and
 * the 620 pixel box in ToastContainer.vue holds that with room over.
 *
 * WHAT COUNTS AS THE LINE. The brackets that mark the press are not on screen,
 * so they do not count. A value dropped into the message is counted at the
 * widest thing it can be, declared in WIDEST or WIDEST_KIND - a value nobody
 * declared fails
 * the check, because an undeclared value is exactly how a message grows past
 * the cap without anyone noticing.
 *
 * WHERE THE LINE IS WRITTEN. A call does not always spell its sentence out. It
 * may hand over a constant, declared at the top of the file or imported from a
 * module of shared refusals; a table read by key; a computed that puts the noun
 * of the screen into a template; a prop, whose words are written by every
 * screen that draws the component. The check follows the argument to those
 * words. A call it cannot follow fails it, for the same reason an undeclared
 * value does: the previous version skipped such calls quietly, and the
 * refusals of the status buttons, the notepads, the chat events, the
 * character sheet and the activation letter went unmeasured behind exactly
 * these forms. The few calls that really cannot be followed are in PASSED_ON,
 * each with what it says and why.
 *
 * ONE SENTENCE, THREE LINES. A call of notifyFailure puts its sentence on a
 * plate by itself only where the server named nothing. On a refusal the server
 * named only in general, "no rights" or "server error", the same sentence is
 * the head of the interceptor's plate, "Не удалось сохранить пост: ошибка
 * сервера" (generalPlate.ts), and where the server refused fields the call put
 * labels on, the plate names those fields instead. All three lines are
 * measured, each against the same cap.
 */
import { describe, expect, it } from "vitest";
import { describeRateLimit } from "@/shared/lib/errors/describeRateLimit";
import {
  GENERAL_REASONS,
  generalPlate,
  UNNAMED_CALL,
} from "@/shared/lib/errors/generalPlate";
import { FIELD_REASONS } from "@/shared/lib/errors/validationErrors";
import {
  disk,
  scanOf,
  sites,
  treeOf,
  type Scan,
  type Tree,
} from "@/shared/lib/testing/toastScan";

/** The longest line a toast may hold, in characters. */
const MAX = 64;

/**
 * A value a message drops in, and the widest it can be.
 *
 * Keyed by the expression exactly as the source writes it. The site addresses
 * are the two in shared/config/site.ts; an offset is at most six characters
 * ("UTC+12", "UTC-11"), whatever the zone database does next. A prop is not
 * declared here: its values are read at every screen that hands it over.
 */
const WIDEST: Record<string, string> = {
  "other.name": "Основной сервер",
  "offsetOf(account)": "UTC+12",
  "offsetOf(device)": "UTC+12",
  games: "играх",
  // The half of the connection sentence that is said on its own first, and
  // then again with the other address added to it.
  UNREACHABLE: "Сайт не отвечает по этому адресу",
};

/**
 * Kinds of value rather than single expressions, for the ones that turn up in
 * message after message. A name is a name wherever it is read from, and the
 * column holds twenty characters of it; a count of anything the site counts
 * runs out of digits long before four.
 */
const WIDEST_KIND: [RegExp, string][] = [
  [/(^|\.)\w*[Uu]sername$/, "п".repeat(20)],
  [/[Cc]ount$/, "9999"],
];

/** The widest a value can be, or undefined when nobody has said. */
const widestOf = (expression: string): string | undefined =>
  WIDEST[expression] ??
  WIDEST_KIND.find(([kind]) => kind.test(expression))?.[1];

/** A value dropped into a template literal. */
const INTERPOLATION = /\$\{([^}]*)\}/g;

/** The words marked as the press. */
const MARKED = /\[([^\]]+)\]/;

/**
 * A call the scan cannot follow, and what it can say.
 *
 * The call is named by its file and a piece of its argument as the source
 * writes it. What it says is measured like any other plate, and an entry whose
 * call has left the code fails the check, so the list cannot outlive what it
 * excuses.
 */
interface PassedOn {
  file: string;
  argument: string;
  says: (scan: Scan) => string[];
  why: string;
}

const PASSED_ON: PassedOn[] = [
  {
    file: "shared/lib/errors/notifyFailure.ts",
    argument: "describeFailure(error, fallback",
    says: () => [],
    why: "The helper itself. It raises the fallback its caller handed it, and every caller is read on its own as a call of notifyFailure.",
  },
  {
    file: "shared/api/client.ts",
    argument: "said",
    says: () => [],
    why: "A 403 or a 5xx the server titled with a sentence of its own, relayed as it stands: the words are the server's, one of the refusals refusalCopy.spec.ts holds to naming their subject. The general ones are raised through generalPlate.ts and measured there.",
  },
  {
    file: "shared/lib/errors/generalPlate.ts",
    argument: "generalPlate(phrase",
    says: () =>
      Object.values(GENERAL_REASONS).map((reason) =>
        generalPlate(UNNAMED_CALL, reason),
      ),
    why: "The general plate, its head the phrase of the call that named it or UNNAMED_CALL for a request nobody named, its tail one of the reasons the interceptor names. The unnamed plate is measured here with each reason; the phrase of every call of notifyFailure is measured with every reason on its own, in the check for general refusals below.",
  },
  {
    file: "shared/lib/composables/useDialogAction.ts",
    argument: "headed",
    says: () => [],
    why: "A refusal of a form or a question on the page whose place the host took away while it waited: the phrase of the action the form was handed, then the server's own sentence. The phrases are the failure props of the forms and questions, each read where it is written; the server's words are its own, as with client.ts above.",
  },
  {
    file: "shared/lib/composables/useDialogAction.ts",
    argument: "waited",
    says: () => [
      describeRateLimit(),
      describeRateLimit(22),
      describeRateLimit(22 * 60),
    ],
    why: "The wait the server asked for, said the way the interceptor says it, for the same form or question with no place left to count it down.",
  },
  {
    file: "shared/api/client.ts",
    argument: "describeRateLimit(",
    // Built the way the interceptor builds it: with no number, with two
    // digits of seconds and the widest noun, and with two digits of minutes.
    // The limiter's own window is a minute, and the longest wait the server
    // names is the account lockout, AccountLockoutDurationMinutes, thirty by
    // default.
    says: () => [
      describeRateLimit(),
      describeRateLimit(22),
      describeRateLimit(22 * 60),
    ],
    why: "The sentence is built from the seconds the server asked for, so it has no words of its own until a refusal arrives.",
  },
];

/** A plate a tree raises: where, what kind, and every line it can say. */
interface Plate {
  where: string;
  kind: string;
  says: string[];
  via: "toast" | "notifyFailure";
  /** The labels a call of notifyFailure names refused fields by. */
  labels: string[];
}

/**
 * Every plate a tree raises, with what the scan could not know: the calls it
 * could not follow, the screens that leave a prop out, and the entries of
 * PASSED_ON whose call is gone.
 */
const platesOf = (tree: Tree, passedOn: PassedOn[]) => {
  const scan = scanOf(tree);
  const plates: Plate[] = [];
  const unread: string[] = [];
  const excused = new Set<PassedOn>();

  for (const file of tree.files) {
    for (const site of sites(scan.read(file))) {
      const where = `${file}:${site.line}`;
      const written = site.argument.replace(/\s+/g, " ").trim();
      const entries = passedOn.filter(
        (entry) => entry.file === file && written.includes(entry.argument),
      );
      entries.forEach((entry) => excused.add(entry));
      const says = entries.length
        ? entries.flatMap((entry) => entry.says(scan))
        : scan.valuesOf(site.argument, file);
      const labels = site.labels ? scan.valuesOf(site.labels, file) : [];
      if (site.labels && !labels.length)
        unread.push(`${where}: labels ${site.labels.replace(/\s+/g, " ")}`);
      if (entries.length || says.length)
        plates.push({ where, kind: site.kind, says, via: site.via, labels });
      else unread.push(`${where}: ${site.kind}(${written})`);
    }
  }

  const stale = passedOn
    .filter((entry) => !excused.has(entry))
    .map((entry) => `${entry.file}: ${entry.argument}`);
  return { plates, unread, gaps: [...new Set(scan.gaps)], stale };
};

/** The line a reader sees, or what is missing before it can be known. */
const rendered = (
  message: string,
): { line: string } | { undeclared: string } => {
  let line = message;
  for (const [whole, expression] of message.matchAll(INTERPOLATION)) {
    const widest = widestOf(expression.trim());
    if (widest === undefined) return { undeclared: expression.trim() };
    line = line.replace(whole, widest);
  }
  return { line: line.replace(MARKED, "$1") };
};

/** Every line past the cap, and every value nobody declared the width of. */
const overflows = (plates: Plate[]): string[] => {
  const offenders: string[] = [];
  for (const { where, says } of plates) {
    for (const message of says) {
      const read = rendered(message);
      if ("undeclared" in read)
        offenders.push(
          `${where}: undeclared value \${${read.undeclared}} - declare its widest form in WIDEST or WIDEST_KIND`,
        );
      else if (read.line.length > MAX)
        offenders.push(
          `${where}: ${read.line.length} characters, ${MAX} is the cap - ${read.line}`,
        );
    }
  }
  return offenders;
};

/**
 * The other lines a call of notifyFailure can end up on: its sentence at the
 * head of each general refusal, and, where it labels the fields it sent, all
 * of them refused alike, "Название, Система, Сеттинг: …", for each rule a field
 * can break. Fields that break different rules are two parts of one line and
 * are not measured together: the gate holds the longest part.
 */
const composed = (plates: Plate[]): Plate[] =>
  plates
    .filter((plate) => plate.via === "notifyFailure")
    .map((plate) => ({
      ...plate,
      says: [
        ...plate.says.flatMap((message) =>
          Object.values(GENERAL_REASONS).map((reason) =>
            generalPlate(message, reason),
          ),
        ),
        ...(plate.labels.length
          ? Object.values(FIELD_REASONS).map(
              (reason) => `${plate.labels.join(", ")}: ${reason}`,
            )
          : []),
      ],
    }));

/** Every line a tree can put in a plate. */
const said = (tree: Tree): string[] =>
  platesOf(tree, []).plates.flatMap((plate) => plate.says);

describe("a toast", () => {
  const site = platesOf(disk(), PASSED_ON);

  it("holds one line the box can hug", () => {
    expect(overflows(site.plates)).toEqual([]);
  });

  it("holds one line at the head of a general refusal, and naming its fields", () => {
    expect(overflows(composed(site.plates))).toEqual([]);
  });

  // A call whose words cannot be found is a plate nobody measures. The fix is
  // to hand the words over in a form the scan follows - a sentence, a
  // constant, a table, a computed, a prop - or, for a call that truly cannot,
  // an entry in PASSED_ON that says what it can put on screen.
  it("is followed to its words wherever they are written", () => {
    expect([...site.unread, ...site.gaps]).toEqual([]);
  });

  it("is excused only while the call is still there", () => {
    expect(site.stale).toEqual([]);
  });

  it("is read the same way the screen reads it", () => {
    // The canary and the two rules, so a green run means the check ran.
    const long = "я".repeat(MAX + 1);
    expect(rendered(long)).toEqual({ line: long });
    expect(long.length).toBeGreaterThan(MAX);

    expect(rendered("попробуйте [RU-прокси]")).toEqual({
      line: "попробуйте RU-прокси",
    });
    expect(rendered("на устройстве ${offsetOf(device)}")).toEqual({
      line: "на устройстве UTC+12",
    });
    expect(rendered("в ${somethingNobodyDeclared}")).toEqual({
      undeclared: "somethingNobodyDeclared",
    });
  });

  it("is found however the file calls it", () => {
    const source = [
      `const { warning } = useToast();`,
      `const { error: showError } = useToast();`,
      `const toast = useToast();`,
      `warning("первое");`,
      `showError("второе");`,
      `toast.info("третье");`,
      `useToast().success("четвертое");`,
      `console.error("не тост");`,
    ].join("\n");

    expect(said(treeOf({ "a.ts": source })).sort()).toEqual([
      "второе",
      "первое",
      "третье",
      "четвертое",
    ]);
  });

  /**
   * The two forms the first version walked past. Both were live in the tree
   * when this was written, and neither was measured by anything.
   */
  it("reads a message that is chosen or looked up", () => {
    const choice = [
      `const toast = useToast();`,
      `toast.success(c.isNpc ? "Персонаж мастера удален" : "Персонаж удален");`,
    ].join("\n");

    expect(said(treeOf({ "a.ts": choice })).sort()).toEqual([
      "Персонаж мастера удален",
      "Персонаж удален",
    ]);

    const table = [
      `const done: Record<string, string> = { Kill: "Персонаж погиб", Leave: "Персонаж ушел" };`,
      `const toast = useToast();`,
      `toast.success(done[transition]);`,
    ].join("\n");

    expect(said(treeOf({ "a.ts": table })).sort()).toEqual([
      "Персонаж погиб",
      "Персонаж ушел",
    ]);

    const fallback = [
      `const { error } = await api.save();`,
      `if (error) notifyFailure(error, "Не удалось сохранить запись");`,
    ].join("\n");

    expect(said(treeOf({ "a.ts": fallback }))).toEqual([
      "Не удалось сохранить запись",
    ]);
  });

  /**
   * The forms the second version walked past, one line past the cap behind
   * each. All of them were live in the tree: StatusButtons, CharacterEdit,
   * ChatEventActions, NotepadBoard and the activation page. A scan that loses
   * any of these ways loses its line here.
   */
  it("follows a line to wherever its words are written", () => {
    const long = (where: string) => `Не удалось ${where} ` + "о".repeat(MAX);
    const tree = treeOf({
      "shared/copy/index.ts": [
        `export * from "./refusals";`,
        `export { NAMED } from "./named";`,
      ].join("\n"),
      "shared/copy/refusals.ts": `export const STARRED = "${long("звезда")}";`,
      "shared/copy/named.ts": `export const NAMED = "${long("имя")}";`,
      "pages/Own.vue": [
        `const OWN = "${long("свое")}";`,
        `notifyFailure(error, OWN);`,
      ].join("\n"),
      "pages/Starred.vue": [
        `import { STARRED } from "@/shared/copy";`,
        `notifyFailure(error, STARRED);`,
      ].join("\n"),
      "pages/Named.vue": [
        `import { NAMED as SAID } from "../shared/copy";`,
        `notifyFailure(error, SAID);`,
      ].join("\n"),
      "pages/Table.vue": [
        `const FAILED = { join: "Не удалось войти", end: "${long("таблица")}" };`,
        `const kind = action.value;`,
        `notifyFailure(error, FAILED[kind]);`,
      ].join("\n"),
      "pages/Computed.vue": [
        `const failure = computed(() => "${long("вычисленное")}");`,
        `notifyFailure(error, failure.value);`,
      ].join("\n"),
      "widgets/Board.vue": `notifyFailure(error, props.loadErrorText);`,
      "pages/Written.vue": `<Board load-error-text="${long("атрибут")}" />`,
      "pages/Bound.vue": [
        `<script setup>const TEXT = "${long("привязка")}";</script>`,
        `<board :load-error-text="TEXT"></board>`,
      ].join("\n"),
      "widgets/Status.vue": [
        "const failure = computed(() => `Не удалось изменить статус ${props.subject}`);",
        `notifyFailure(error, failure.value);`,
      ].join("\n"),
      "pages/StatusPage.vue": `<Status subject="${"о".repeat(MAX)}" />`,
    });

    const scanned = platesOf(tree, []);
    expect(scanned.unread).toEqual([]);
    expect(scanned.gaps).toEqual([]);
    expect(
      overflows(scanned.plates)
        .map((offender) => offender.split(" - ").pop())
        .sort(),
    ).toEqual(
      [
        long("свое"),
        long("звезда"),
        long("имя"),
        long("таблица"),
        long("вычисленное"),
        long("атрибут"),
        long("привязка"),
        "Не удалось изменить статус " + "о".repeat(MAX),
      ].sort(),
    );
  });

  // A sentence beside a name at the call stopped the scan at the sentence:
  // the short line was measured, and the long one behind the name passed
  // unread. The same held for a prop beside a sentence.
  it("reads every branch of an argument that spells one of its own", () => {
    const long = "Не удалось " + "о".repeat(MAX);
    const scanned = platesOf(
      treeOf({
        "pages/Mixed.vue": [
          `const message = "${long}";`,
          `notifyFailure(error, flag ? "Коротко" : message);`,
        ].join("\n"),
        "widgets/Board.vue": `notifyFailure(error, flag ? "Коротко" : props.loadErrorText);`,
        "pages/Written.vue": `<Board load-error-text="${long} атрибут" />`,
      }),
      [],
    );

    expect(scanned.unread).toEqual([]);
    expect(
      overflows(scanned.plates)
        .map((offender) => offender.split(" - ").pop())
        .sort(),
    ).toEqual([long, `${long} атрибут`].sort());
  });

  // The name that picks one of two sentences says neither, and a flag bound
  // to a boolean has no words to follow it to.
  it("takes the condition of a choice for no sentence", () => {
    const scanned = platesOf(
      treeOf({
        "widgets/Toggle.vue": `notifyFailure(error, props.on ? "Не удалось выключить" : "Не удалось включить");`,
        "pages/Page.vue": `<Toggle :on="state.enabled" />`,
      }),
      [],
    );

    expect([...scanned.unread, ...scanned.gaps]).toEqual([]);
    expect(scanned.plates.flatMap((plate) => plate.says).sort()).toEqual([
      "Не удалось включить",
      "Не удалось выключить",
    ]);
  });

  // A sentence that fits by itself can still be the head of a longer line:
  // "Не удалось сохранить порядок закрепленных топиков" was 49 characters on
  // its own and 68 with ": недостаточно прав" after it. The fields a call
  // labels are a line of their own too.
  it("measures a call's sentence at the head of a general refusal", () => {
    const head = "Не удалось " + "о".repeat(40);
    const label = "п".repeat(40);
    const scanned = platesOf(
      treeOf({
        "pages/Head.vue": `notifyFailure(error, "${head}");`,
        "pages/Fields.vue": `notifyFailure(error, "Коротко", { text: "${label}" });`,
      }),
      [],
    );

    expect(scanned.unread).toEqual([]);
    expect(overflows(scanned.plates)).toEqual([]);
    const offenders = overflows(composed(scanned.plates)).map((offender) =>
      offender.split(" - ").pop(),
    );
    expect(offenders).toEqual(
      expect.arrayContaining([
        `${head}: недостаточно прав`,
        `${head}: ошибка сервера`,
        `${label}: слишком длинное значение`,
      ]),
    );
    expect(offenders.some((line) => line?.startsWith("Коротко"))).toBe(false);
  });

  it("fails a call whose field labels it cannot follow", () => {
    const scanned = platesOf(
      treeOf({
        "pages/Lost.vue": `notifyFailure(error, "Коротко", fromSomewhere);`,
      }),
      [],
    );

    expect(scanned.unread).toEqual(["pages/Lost.vue:1: labels fromSomewhere"]);
  });

  it("fails a call it cannot follow to its words", () => {
    const scanned = platesOf(
      treeOf({
        "pages/Lost.vue": `notifyFailure(error, fromSomewhere);`,
        "widgets/Board.vue": `notifyFailure(error, props.loadErrorText);`,
        "pages/Bare.vue": `<Board />`,
      }),
      [],
    );

    expect(scanned.unread).toEqual([
      "pages/Lost.vue:1: error(fromSomewhere)",
      "widgets/Board.vue:1: error(props.loadErrorText)",
    ]);
    expect(scanned.gaps).toEqual([
      "pages/Bare.vue: <Board> is drawn without load-error-text",
    ]);
  });

  it("takes the word of PASSED_ON only while its call is there", () => {
    const entry = (argument: string, says: string[]): PassedOn => ({
      file: "a.ts",
      argument,
      says: () => says,
      why: "canary",
    });
    const kept = entry("build(", ["Сказано вручную"]);
    const gone = entry("gone(", []);
    const scanned = platesOf(
      treeOf({ "a.ts": `notifyFailure(error, build(seconds));` }),
      [kept, gone],
    );

    expect(scanned.plates.flatMap((plate) => plate.says)).toEqual([
      "Сказано вручную",
    ]);
    expect(scanned.unread).toEqual([]);
    expect(scanned.stale).toEqual(["a.ts: gone("]);
  });
});
