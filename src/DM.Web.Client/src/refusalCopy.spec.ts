/**
 * @vitest-environment node
 */

/**
 * The wording of a refusal belongs to the server.
 *
 * A 403 is not one event. The API refuses with a sentence of its own in 68
 * places — "Вы в черном списке этого блога", "Аккаунт заблокирован", "Нельзя
 * подписаться на собственный блог" — and answers an authorization refusal with
 * the one constant title kept in DM.Domain.Core.Exceptions.RefusalMessage. The
 * response interceptor relays that title, so no module on the client has to
 * decide what a refusal says.
 *
 * When one decides anyway, its sentence and the server's end up on the same
 * screen: the poll form said "Недостаточно прав" in the form while the toast
 * above it said "Недостаточно прав для этого действия" — one event, two
 * wordings, which is the outcome CODE_STYLE names in "одно слово на одно
 * значение". Three forms carried such a copy, in the same shape, down to the
 * same shortened sentence.
 *
 * The check is textual and narrow on purpose: it holds the one refusal that had
 * four copies here. It cannot judge a module that invents some other sentence
 * for the same 403 — no scan can — but a copy is made from a copy in sight, and
 * this rule keeps the last one out of sight. Comments count: a copy of the
 * sentence in prose is the draft of the next copy in code. Spec files do not —
 * a test quotes the wording in order to assert on it (client.spec.ts does).
 *
 * The single place allowed to spell it is generalRefusal.ts, and it decides
 * nothing either: it keeps the server's title word for word, so the
 * interceptor can tell that refusal from the ones that name a reason, and
 * generalRefusal.spec.ts holds it equal to the server's. What the reader sees
 * for it is the general plate, the call's phrase and "недостаточно прав" after
 * it (generalPlate.ts).
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..");
const REPO_ROOT = resolve(CLIENT_ROOT, "..", "..");

const ACCESS_REFUSAL = "Недостаточно прав";
const CARRIER = "src/DM.Web.Client/src/shared/lib/errors/generalRefusal.ts";

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

const sources = (dir: string): string[] => {
  const found: string[] = [];
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      found.push(...sources(full));
      continue;
    }
    if (!/\.(ts|vue)$/.test(name)) continue;
    if (/\.(spec|test)\.ts$/.test(name)) continue;
    found.push(full);
  }
  return found;
};

describe("the wording of a refusal is the server's", () => {
  it("keeps the general access refusal in one place, the server's copy", () => {
    const files = sources(join(CLIENT_ROOT, "src"));
    // A walk that finds nothing passes the assertion under it.
    expect(files.length).toBeGreaterThan(100);

    const carriers = files
      .filter((file) => readFileSync(file, "utf8").includes(ACCESS_REFUSAL))
      .map((file) => relative(REPO_ROOT, file).split("\\").join("/"))
      .sort();

    expect(carriers).toEqual([CARRIER]);
  });
});

/**
 * What the viewer may not do is not offered, and no sentence stands in its
 * place. The owner, 05.10: "если пользователю что-то нельзя - НЕ ДАВАЙ ЕМУ
 * ВОЗМОЖНОСТЬ ЭТОГО СДЕЛАТЬ, А НЕ ВЫВОДИ ОШИБКИ". The audit of that day found
 * the sentences that stood where a form would be, said to somebody the client
 * already knew it would refuse, and they went together with the props that
 * carried them.
 *
 * Narrow on purpose, like the check above: it holds the removed props and the
 * very sentences, not the words "нельзя" or "только", which honest copy uses as
 * well ("нельзя отменить" in a confirmation, the rules of the site). Comments
 * count for the same reason as above, spec files do not.
 *
 * The refusals of a discussion are not on the list. A discussion the viewer
 * may read and not write in stays whole, and the plaque of a refusal stands
 * where the form would be, saying why there is none ("Писать в обсуждении
 * могут только участники игры", "Топик закрыт для комментариев", "Вы в черном
 * списке ..."). Such a plaque answers no action the viewer tried, which is why
 * it is not counted with the refusals above. The plaque under an open game
 * chat room the viewer reads and may not write in, saying who writes there,
 * is the same kind: openness is about reading. That a refusal is a plaque and
 * not a grey line is held in emptyStateAlignment.spec.ts.
 *
 * Nothing closes the reading of a discussion and nothing turns its comments
 * off, so the lines that said so are on the list: comments off in a blog or
 * under a publication, and a game discussion for its members alone, which
 * stood in place of the discussion itself.
 *
 * The draft of a publication stays on the list: it has no discussion to read,
 * and the line promised one. So does the server's sentence of a closed event
 * of the global chat ("Писать могут только его участники"): the line of the
 * event says it, and nothing stands in place of the form.
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
    const files = sources(join(CLIENT_ROOT, "src"));
    // A walk that finds nothing passes the assertion under it.
    expect(files.length).toBeGreaterThan(100);

    const found = files.flatMap((file) => {
      const text = readFileSync(file, "utf8").toLowerCase();
      return REMOVED_REFUSALS.filter((line) =>
        text.includes(line.toLowerCase()),
      ).map(
        (line) =>
          `${relative(join(CLIENT_ROOT, "src"), file).split("\\").join("/")}: ${line}`,
      );
    });

    expect(found).toEqual([]);
  });
});
