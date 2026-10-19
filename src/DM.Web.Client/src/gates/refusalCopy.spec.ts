/**
 * @vitest-environment node
 */

/**
 * The wording of a refusal belongs to the server: the API refuses with a
 * sentence of its own and answers an authorization refusal with the one title
 * of DM.Domain.Core.Exceptions.RefusalMessage, which the response interceptor
 * relays. A client copy of that title ends up on the screen beside the server's
 * own wording of the same event. The check holds that one sentence; comments
 * count (a copy in prose is the draft of the next copy in code), spec files do
 * not. The interceptor tells a general refusal by the server's mark (general),
 * not by its words, so no file spells the access refusal; the title of a 500
 * stays in generalRefusal.ts alone, said of a 5xx that came with no document
 * (generalRefusal.spec.ts holds it equal to the server's).
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { relative, resolve } from "path";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

// src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(CLIENT_SRC, "..", "..", "..");

const ACCESS_REFUSAL = "Недостаточно прав";
const SERVER_ERROR_TITLE = "Ошибка сервера";
const CARRIER = "src/DM.Web.Client/src/shared/lib/errors/generalRefusal.ts";

const isSource = (name: string): boolean =>
  /\.(ts|vue)$/.test(name) && !/\.(spec|test)\.ts$/.test(name);

describe("the wording of a refusal is the server's", () => {
  it("keeps no copy of the general access refusal", () => {
    const files = collect(CLIENT_SRC, isSource);
    // A walk that finds nothing passes the assertion under it.
    expect(files.length).toBeGreaterThan(100);

    const carriers = files
      .filter((file) => readFileSync(file, "utf8").includes(ACCESS_REFUSAL))
      .map((file) => relative(REPO_ROOT, file).split("\\").join("/"))
      .sort();

    expect(carriers).toEqual([]);
  });

  // The title of a 500, held to the middleware's by generalRefusal.spec.ts.
  it("keeps the general server error in one place, the server's copy", () => {
    const carriers = collect(CLIENT_SRC, isSource)
      .filter((file) => readFileSync(file, "utf8").includes(SERVER_ERROR_TITLE))
      .map((file) => relative(REPO_ROOT, file).split("\\").join("/"))
      .sort();

    expect(carriers).toEqual([CARRIER]);
  });
});

/**
 * What the viewer may not do is not offered, and no sentence stands in its
 * place; nothing closes the reading of a discussion or turns its comments off.
 * Narrow on purpose: it holds the removed props and the very sentences, not the
 * words "нельзя" or "только" that honest copy uses as well. The plaque saying
 * why a discussion has no form answers no action and is not on the list.
 */
const REMOVED_REFUSALS = [
  "reviewNotice",
  "review-notice",
  "Черновик обсуждать нельзя",
  "Нельзя оценивать собственный пост",
  "Вы уже оценили этот пост",
  "Новичкам доступна только нейтральная оценка",
  "Ваша рецензия на эту игру уже опубликована",
  "Сейчас рекомендацию написать нельзя",
  "Писать могут только его участники",
  "Комментарии в этом блоге отключены",
  "Комментарии к этой публикации отключены",
  "Комментарии доступны только участникам игры",
  "canViewComments",
];

describe("what may not be done is not offered", () => {
  it("keeps the refusals the 05.10 audit took off the screen out of the client", () => {
    const files = collect(CLIENT_SRC, isSource);
    // A walk that finds nothing passes the assertion under it.
    expect(files.length).toBeGreaterThan(100);

    const found = files.flatMap((file) => {
      const text = readFileSync(file, "utf8").toLowerCase();
      return REMOVED_REFUSALS.filter((line) =>
        text.includes(line.toLowerCase()),
      ).map((line) => `${asPath(file)}: ${line}`);
    });

    expect(found).toEqual([]);
  });
});
