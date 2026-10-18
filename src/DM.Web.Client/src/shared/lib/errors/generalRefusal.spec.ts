/**
 * @vitest-environment node
 */

/**
 * One wording per fact, and the retired ones do not come back.
 *
 * The three sentences in generalRefusal.ts are what a screen says when the
 * server named nothing. Six wordings used to stand in the tree for those three
 * facts, and a reader could meet two of them at once: a form wrote its own line
 * into its strip while the interceptor raised a differently worded toast over
 * the top of it. Four of the six were two sentences with a full stop apiece,
 * which is the rule sentencePeriods.spec.ts holds for a line on a plate.
 *
 * Three things are checked, and the scan is the one that earns its keep: a
 * retired sentence is not deleted once, it is deleted and then typed again by
 * the next screen that needs one.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

import {
  ACCESS_DENIED,
  LETTER_NOT_SENT,
  REQUEST_FAILED,
  SERVER_ERROR,
} from "./generalRefusal";
import { GENERAL_REASONS } from "./generalPlate";

const HERE = dirname(fileURLToPath(import.meta.url));
// shared/lib/errors -> shared -> src -> DM.Web.Client -> src -> repository root
const CLIENT_SRC = resolve(HERE, "..", "..", "..");
const REPO_ROOT = resolve(CLIENT_SRC, "..", "..", "..");

/** Where the API keeps the general title of a refused authorization check. */
const REFUSALS = join(
  REPO_ROOT,
  "src",
  "DM.Domain.Core",
  "Exceptions",
  "RefusalMessage.cs",
);

/** Where the API titles a 500. The client keeps the same words for a 5xx that
 * never reached the middleware, and the two must not drift apart. */
const MIDDLEWARE = join(
  REPO_ROOT,
  "src",
  "DM.Web.API",
  "Middleware",
  "ExceptionProblemDetailsFactoryExtensions.cs",
);

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/**
 * The wordings taken out, and what replaced each. A spec quotes them in order
 * to look for them, so spec files are not scanned; nothing else in the tree has
 * a reason to spell one out, in code or in prose, because a copy of a retired
 * sentence in a comment is the draft of the next copy in code.
 */
const RETIRED: readonly { text: string; instead: string }[] = [
  { text: "Произошла ошибка. Попробуйте позже.", instead: "SERVER_ERROR" },
  {
    text: "Не удалось отправить письмо. Попробуйте позже.",
    instead: "LETTER_NOT_SENT",
  },
  { text: "Ошибка сервера. Попробуйте позже.", instead: "SERVER_ERROR" },
  {
    text: "Внутренняя ошибка сервера. Попробуйте позже",
    instead: "SERVER_ERROR",
  },
  {
    text: "Не удалось создать отзыв. Попробуйте позже",
    instead: "REQUEST_FAILED",
  },
];

const sources = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      sources(full, out);
      continue;
    }
    if (!/\.(ts|vue)$/.test(name)) continue;
    if (/\.(spec|test)\.ts$/.test(name)) continue;
    out.push(full);
  }
  return out;
};

const where = (file: string): string =>
  relative(REPO_ROOT, file).split("\\").join("/");

describe("the general refusals", () => {
  it("are one sentence each and carry no period", () => {
    for (const text of [SERVER_ERROR, REQUEST_FAILED, LETTER_NOT_SENT]) {
      expect(text.endsWith("."), `"${text}" заканчивается точкой`).toBe(false);
      expect(
        /[.!?]\s/.test(text),
        `"${text}" это два предложения в одной строке`,
      ).toBe(false);
      expect(text, `"${text}" пишется через "е"`).not.toMatch(/[ёЁ]/);
    }
  });

  it("say a 5xx in the words the API titles one with", () => {
    // Two copies on purpose: the middleware is not always reached, and a toast
    // over a bodiless 502 still has to speak. Equal on purpose too, or the
    // strip of a form and the toast above it name one failure twice.
    const middleware = readFileSync(MIDDLEWARE, "utf8");

    expect(
      middleware.includes(`"${SERVER_ERROR}"`),
      `клиент говорит "${SERVER_ERROR}", а middleware титулует 500 иначе`,
    ).toBe(true);
  });

  it("know the general title of a 403 in the words the API gives it", () => {
    // The interceptor tells this title apart from the refusals that name a
    // reason. A copy that drifted from the server's would be taken for one of
    // those and relayed whole, the call's phrase dropped.
    const refusals = readFileSync(REFUSALS, "utf8");

    expect(
      refusals.includes(`AccessDenied = "${ACCESS_DENIED}";`),
      `клиент узнает 403 по "${ACCESS_DENIED}", а сервер титулует его иначе`,
    ).toBe(true);
  });

  it("end a general plate with the same words a 5xx is titled with", () => {
    // The tail of "Не удалось сохранить пост: ошибка сервера" and the strip of
    // a form that quotes the server whole say one thing, one of them after a
    // colon.
    expect(GENERAL_REASONS.server).toBe(
      SERVER_ERROR[0].toLowerCase() + SERVER_ERROR.slice(1),
    );
  });

  it("leave no retired wording anywhere in the client", () => {
    const files = sources(CLIENT_SRC);
    // A walk that finds nothing passes the assertion under it.
    expect(files.length).toBeGreaterThan(100);

    const offenders: string[] = [];
    for (const file of files) {
      const source = readFileSync(file, "utf8");
      for (const { text, instead } of RETIRED) {
        if (source.includes(text)) {
          offenders.push(`${where(file)}: "${text}" -> ${instead}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("knows a retired wording when it reads one", () => {
    // The canary: the scan above passes when it finds nothing, and it finds
    // nothing just as readily when the list is empty or the walk is broken.
    const fake = `const message = "${RETIRED[0].text}";`;

    expect(RETIRED.filter(({ text }) => fake.includes(text))).toHaveLength(1);
  });
});
