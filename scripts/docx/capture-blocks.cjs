/* Снимает контентные блоки (раздел 4.2.2 спеки) вырезками по селектору
   компонента, а не целыми страницами: в документе рядом стоят кадры блоков той
   же плотности, и снимок во всю полосу увеличил бы текст блока вдвое.

   Разрушающие действия не выполняются: режим редактирования закрывается
   кнопкой "Отмена", пост не отправляется, готовящийся бросок снимается.

   Учетные данные - сидовые, лежат в открытую в e2e/fixtures/auth.ts. */
const {
  chromium,
  request,
} = require("../../src/DM.Web.Client/node_modules/playwright");
const fs = require("fs");
const path = require("path");

const API = "http://localhost:5000";
const APP = "http://localhost:5174";
const OUT = process.env.SHOTS_DIR || path.join(__dirname, "shots");

/* Пауза между кадрами: глобальный лимитер - 300 запросов в минуту на адрес. */
const PACE_MS = Number(process.env.SHOT_PACE_MS || 1500);

const report = { ok: [], skipped: [], failed: [], toasts: [] };

/* Аргументы - номера кадров, которые надо переснять ("98", "104"). */
const only = process.argv.slice(2);
const want = (...names) =>
  !only.length || names.some((n) => only.some((a) => n.startsWith(a)));

async function login(email, password) {
  const ctx = await request.newContext();
  const r = await ctx.post(API + "/v1/account/login", {
    data: { email, password },
  });
  if (!r.ok()) throw new Error("login " + r.status() + ": " + (await r.text()));
  const { user } = await r.json();
  const state = await ctx.storageState();
  await ctx.dispose();
  state.origins = [
    {
      origin: APP,
      localStorage: [{ name: "user", value: JSON.stringify(user) }],
    },
  ];
  return state;
}

async function apiGet(ctx, url) {
  try {
    const r = await ctx.get(API + url);
    if (!r.ok()) return null;
    return await r.json();
  } catch {
    return null;
  }
}

/* Отказы лимитера и сетевые сбои попадали в кадр и уезжали в документ как
   содержимое страницы. Кадр снимается только с чистой страницы. */
const REFUSAL_MARKS = [
  "Слишком много запросов",
  "Не удалось загрузить",
  "Что-то пошло не так",
  "Повторить попытку",
];

async function dismissToasts(page, name) {
  const items = page.locator(".toast-item");
  for (let i = (await items.count()) - 1; i >= 0; i--) {
    const item = items.nth(i);
    const text = (await item.innerText().catch(() => ""))
      .replace(/\s+/g, " ")
      .trim();
    await item
      .locator('button[aria-label="Закрыть"]')
      .click()
      .catch(() => {});
    if (text) report.toasts.push(name + ": " + text);
  }
  if (await items.count()) await page.waitForTimeout(400);
}

async function pageIsClean(page) {
  const hits = await page.evaluate((marks) => {
    const text = document.body ? document.body.innerText : "";
    return marks.filter((m) => text.includes(m));
  }, REFUSAL_MARKS);
  return hits.length === 0;
}

async function open(page, url, attempt = 1) {
  await page
    .goto(APP + url, { waitUntil: "networkidle", timeout: 25000 })
    .catch(() => page.waitForTimeout(1500));
  await page.waitForTimeout(900);
  await dismissToasts(page, url);

  const text = await page.evaluate(() =>
    document.body ? document.body.innerText : "",
  );
  const refused = REFUSAL_MARKS.some((m) => text.includes(m));
  const blank = text.replace(/\s/g, "").length < 200;
  if (!refused && !blank) return true;
  if (attempt >= 4) return false;
  await page.waitForTimeout(refused ? attempt * 30000 : 4000);
  return open(page, url, attempt + 1);
}

/* Кадр элемента. Курсор уводится в угол, чтобы подсказка нажатой кнопки не
   висела поверх блока. */
async function snap(page, name, target) {
  await dismissToasts(page, name);
  if (!(await pageIsClean(page))) {
    report.failed.push(name + ": отказ на странице");
    return false;
  }
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);
  await target.scrollIntoViewIfNeeded().catch(() => {});
  await page.waitForTimeout(300);
  const box = await target.boundingBox();
  await target.screenshot({ path: path.join(OUT, name + ".png") });
  report.ok.push(
    name + (box ? ` (${Math.round(box.width)}x${Math.round(box.height)})` : ""),
  );
  await page.waitForTimeout(PACE_MS);
  return true;
}

/* Кадр по объединению рамок нескольких элементов - для блока, часть которого
   рендерит оболочка страницы (заголовок блога живет в шапке, а не в самом
   блоке). Элементы должны стоять в одной колонке друг под другом, иначе в
   вырезку попадут соседи. */
async function snapUnion(page, name, locators) {
  await dismissToasts(page, name);
  if (!(await pageIsClean(page))) {
    report.failed.push(name + ": отказ на странице");
    return false;
  }
  await page.mouse.move(2, 2);
  await locators[0].scrollIntoViewIfNeeded().catch(() => {});
  await page.waitForTimeout(400);
  const boxes = [];
  for (const l of locators) {
    const b = await l.boundingBox();
    if (b) boxes.push(b);
  }
  if (!boxes.length) {
    report.skipped.push(name + ": нет рамок элементов");
    return false;
  }
  const x = Math.min(...boxes.map((b) => b.x));
  const y = Math.min(...boxes.map((b) => b.y));
  const right = Math.max(...boxes.map((b) => b.x + b.width));
  const bottom = Math.max(...boxes.map((b) => b.y + b.height));
  await page.screenshot({
    path: path.join(OUT, name + ".png"),
    clip: { x, y, width: right - x, height: bottom - y },
  });
  report.ok.push(name + ` (${Math.round(right - x)}x${Math.round(bottom - y)})`);
  await page.waitForTimeout(PACE_MS);
  return true;
}

/* Кадр попапа, который появляется по наведению на плитку. Попап телепортируется
   в body, поэтому снимается своим элементом, а не вырезкой вокруг плитки:
   попап шире плитки, и общая вырезка затянула бы в кадр соседей по решетке. */
async function snapTooltip(page, name, trigger) {
  await dismissToasts(page, name);
  if (!(await pageIsClean(page))) {
    report.failed.push(name + ": отказ на странице");
    return false;
  }
  await trigger.scrollIntoViewIfNeeded().catch(() => {});
  await page.waitForTimeout(300);
  await trigger.hover();
  await page.waitForTimeout(900);
  const tip = page.locator('[role="tooltip"]').first();
  if (!(await tip.count())) {
    report.skipped.push(name + ": попап не появился");
    return false;
  }
  // Попап позиционирован fixed: свой кадр элемента playwright снимает после
  // повторной прокрутки, и в кадр съезжает полоса страницы из-под попапа.
  // Вырезка по рамке снимается без прокрутки и попадает точно.
  const box = await tip.boundingBox();
  if (!box) {
    report.skipped.push(name + ": у попапа нет рамки");
    return false;
  }
  await page.screenshot({ path: path.join(OUT, name + ".png"), clip: box });
  report.ok.push(name + ` (${Math.round(box.width)}x${Math.round(box.height)})`);
  await page.mouse.move(2, 2);
  await page.waitForTimeout(PACE_MS);
  return true;
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });

  const adminState = await login("admin@test.local", "Test123!");
  const authApi = await request.newContext({ storageState: adminState });
  const guestApi = await request.newContext();

  // ── Разрешение сущностей через API ──────────────────────────────────────
  // Комната игры, где снимающий - мастер: там доступен композер с бросками.
  const rooms =
    (await apiGet(guestApi, "/v1/games/aaaaa/rooms"))?.resources ?? [];
  const diceRoom = rooms.find(
    (r) =>
      r.settings?.diceEnabled &&
      !String(r.type ?? "")
        .toLowerCase()
        .includes("chat"),
  );

  // Пост во всем составе блока: рейтинг с оценками плюс метаигровой текст.
  // Длинный пост в кадре блока бесполезен - он показывает не состав блока, а
  // чужой текст на страницу, поэтому берется первый короткий подходящий.
  const isChat = (r) =>
    String(r.type ?? "")
      .toLowerCase()
      .includes("chat");
  let rich = null;
  const games = (await apiGet(guestApi, "/v1/games?take=50"))?.resources ?? [];
  search: for (const game of games) {
    const rs =
      (await apiGet(guestApi, `/v1/games/${game.publicId}/rooms`))?.resources ??
      [];
    for (const r of rs.filter((x) => !isChat(x))) {
      const posts =
        (await apiGet(guestApi, `/v1/rooms/${r.id}/posts?size=50`))
          ?.resources ?? [];
      // Аватар персонажа в условие не входит: в сиде посты с картинкой
      // персонажа - это витринные простыни на две тысячи знаков, и кадр блока
      // из такого поста показывает чужой текст, а не состав блока.
      const hit = posts.find(
        (x) =>
          x.reviewCount >= 2 &&
          x.metagameText &&
          (x.gameText || "").length < 300,
      );
      if (hit) {
        rich = { game: game.publicId, room: r.roomNumber, post: hit.id };
        break search;
      }
    }
  }

  // Комната, где у поста есть результаты бросков.
  let rolledRoomNum = null;
  let rolledGame = null;
  outer: for (const g of ["aaabp", "aaaaa"]) {
    const rs = (await apiGet(guestApi, `/v1/games/${g}/rooms`))?.resources ?? [];
    for (const r of rs) {
      const posts = (await apiGet(guestApi, `/v1/rooms/${r.id}/posts?size=50`))
        ?.resources;
      if ((posts ?? []).some((p) => (p.diceRolls ?? []).length)) {
        rolledGame = g;
        rolledRoomNum = r.roomNumber;
        break outer;
      }
    }
  }

  // Блог автора-админа: у него есть описание, рубрики и публикации.
  const blogs = (await apiGet(guestApi, "/v1/blogs?take=50"))?.resources ?? [];
  const ownBlog =
    blogs.find((b) => b.author?.username === "SolohinLex") ?? blogs[0];
  const blogId = ownBlog?.publicId || ownBlog?.id;

  // Обращение с ответом модерации: одно закрывает и 4.2.2.23, и 4.2.2.24.
  const tickets =
    (await apiGet(authApi, "/v1/moderation/tickets?take=50"))?.resources ?? [];
  const answered = tickets.find((t) => t.answer);
  const anyTicket = answered ?? tickets[0];

  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 1200 },
    storageState: adminState,
    reducedMotion: "reduce",
  });
  const p = await ctx.newPage();

  // ── 98/99/103: игровой пост, его редактирование и одна оценка ───────────
  if (
    want("98-game-post", "99-game-post-edit", "103-post-review") &&
    rich &&
    (await open(p, `/game/${rich.game}/rooms/${rich.room}`))
  ) {
    const byId = p.locator(`.game-post[data-id="${rich.post}"]`);
    const target = (await byId.count()) ? byId.first() : null;

    if (!target) {
      report.skipped.push("98/99/103: в комнате нет постов");
    } else {
      if (want("98-game-post")) await snap(p, "98-game-post", target);

      // 103: оценки раскрываются кнопкой рейтинга, кадр - одна карточка оценки.
      if (want("103-post-review")) {
        const ratingBtn = target.locator("button.rating-value").first();
        if (await ratingBtn.count()) {
          await ratingBtn.click();
          await p.waitForTimeout(1600);
          const review = target.locator(".review-item").first();
          if (await review.count()) {
            await snap(p, "103-post-review", review);
          } else {
            report.skipped.push("103-post-review: оценки не отрисовались");
          }
          await ratingBtn.click().catch(() => {});
          await p.waitForTimeout(600);
        } else {
          report.skipped.push("103-post-review: у поста нет оценок");
        }
      }

      // 99: inline-редактирование. Изменения не сохраняются - выход "Отмена".
      if (want("99-game-post-edit")) {
        const editBtn = target
          .locator("button.post-action-btn", { hasText: "Редактировать" })
          .first();
        if (await editBtn.count()) {
          await editBtn.click();
          await p.waitForTimeout(1200);
          if (await target.locator(".post-edit").count()) {
            await snap(p, "99-game-post-edit", target);
          } else {
            report.skipped.push("99-game-post-edit: режим не открылся");
          }
          await target
            .locator("button.edit-btn", { hasText: "Отмена" })
            .first()
            .click()
            .catch(() => {});
          await p.waitForTimeout(700);
        } else {
          report.skipped.push("99-game-post-edit: нет кнопки редактирования");
        }
      }
    }
  } else if (want("98-game-post", "99-game-post-edit", "103-post-review")) {
    report.skipped.push("98/99/103: пост с оценками и метатекстом не нашелся");
  }

  // ── 100/101: форма нового поста и форма бросков внутри нее ──────────────
  // Пост не отправляется, готовящийся бросок снимается крестиком.
  if (
    want("100-game-post-create", "101-dice-form") &&
    diceRoom &&
    (await open(p, `/game/aaaaa/rooms/${diceRoom.roomNumber}`))
  ) {
    const composer = p.locator("section.composer").first();
    if (await composer.count()) {
      if (want("100-game-post-create")) {
        await snap(p, "100-game-post-create", composer);
      }
      if (want("101-dice-form")) {
        const dice = composer.locator(".dice").first();
        if (await dice.count()) {
          await dice.locator(".dice-input-count").fill("2");
          await dice.locator(".dice-input-sides").fill("6");
          await dice.locator(".dice-input-bonus").first().fill("3");
          await dice
            .locator(".dice-input-comment")
            .fill("Атака по гоблину");
          await dice.locator(".dice-add-btn").click();
          await p.waitForTimeout(700);
          await snap(p, "101-dice-form", dice);
          await dice
            .locator(".dice-chip-remove")
            .first()
            .click()
            .catch(() => {});
          await p.waitForTimeout(400);
        } else {
          report.skipped.push("101-dice-form: в комнате броски выключены");
        }
      }
    } else {
      report.skipped.push("100/101: композер недоступен");
    }
  } else if (want("100-game-post-create", "101-dice-form")) {
    report.skipped.push("100/101: комната с бросками не открылась");
  }

  // ── 102: контейнер бросков в опубликованном посте ────────────────────────
  // В документ этот кадр не идет и в MAPPING его нет: клиент читает бросок как
  // {dice, result}, а сервер отдает {edges, rolls, results[]}, и контейнер
  // рисует "dundefined: undefined +7 = NaN". Кадр остается уликой на диске -
  // документировать дефект как состав блока нельзя.
  if (want("102-dice-rolls")) {
    if (
      rolledRoomNum &&
      (await open(p, `/game/${rolledGame}/rooms/${rolledRoomNum}`))
    ) {
      const rolls = p.locator(".dice-rolls").first();
      if (await rolls.count()) {
        await snap(p, "102-dice-rolls", rolls);
      } else {
        report.skipped.push("102-dice-rolls: бросков в комнате не видно");
      }
    } else {
      report.skipped.push("102-dice-rolls: поста с бросками нет в сиде");
    }
  }

  // ── 104: информация блога ────────────────────────────────────────────────
  if (want("104-blog-details") && blogId && (await open(p, `/blogs/${blogId}`))) {
    const details = p.locator(".blog-details").first();
    const header = p.locator(".blog-header").first();
    if (await details.count()) {
      // Название блога рендерит шапка зоны, а не сам блок: в кадр входят оба.
      if (await header.count()) {
        await snapUnion(p, "104-blog-details", [header, details]);
      } else {
        await snap(p, "104-blog-details", details);
      }
    } else {
      report.skipped.push("104-blog-details: блок не найден");
    }
  } else if (want("104-blog-details")) {
    report.skipped.push("104-blog-details: страница блога не открылась");
  }

  // ── 105: публикация в ленте блога ────────────────────────────────────────
  if (
    want("105-publication") &&
    blogId &&
    (await open(p, `/blogs/${blogId}/feed`))
  ) {
    const pub = p.locator(".topic").first();
    if (await pub.count()) {
      await snap(p, "105-publication", pub);
    } else {
      report.skipped.push("105-publication: в ленте нет публикаций");
    }
  } else if (want("105-publication")) {
    report.skipped.push("105-publication: лента блога не открылась");
  }

  // ── 106/107: обращение и ответ в обращении ───────────────────────────────
  if (
    want("106-ticket", "107-ticket-answer") &&
    anyTicket &&
    (await open(p, `/moderation/tickets/${anyTicket.id}`))
  ) {
    const card = p.locator(".ticket-card").first();
    if (want("106-ticket")) {
      if (await card.count()) {
        await snap(p, "106-ticket", card);
      } else {
        report.skipped.push("106-ticket: карточка не найдена");
      }
    }
    if (want("107-ticket-answer")) {
      const answer = p.locator(".ticket-answer").first();
      if (await answer.count()) {
        await snap(p, "107-ticket-answer", answer);
      } else {
        report.skipped.push("107-ticket-answer: у обращения нет ответа");
      }
    }
  } else if (want("106-ticket", "107-ticket-answer")) {
    report.skipped.push("106/107: обращение не открылось");
  }

  // ── 108: рецензия на игру ────────────────────────────────────────────────
  if (want("108-game-review") && (await open(p, "/game/aaaaa/reviews"))) {
    const card = p.locator(".bubble-card").first();
    if (await card.count()) {
      await snap(p, "108-game-review", card);
    } else {
      report.skipped.push("108-game-review: рецензий у игры нет");
    }
  } else if (want("108-game-review")) {
    report.skipped.push("108-game-review: вкладка рецензий не открылась");
  }

  // ── 109: рекомендация пользователю ───────────────────────────────────────
  if (
    want("109-endorsement") &&
    (await open(p, "/users/SolohinLex/received-endorsements"))
  ) {
    const card = p.locator(".bubble-card").first();
    if (await card.count()) {
      await snap(p, "109-endorsement", card);
    } else {
      report.skipped.push("109-endorsement: рекомендаций нет");
    }
  } else if (want("109-endorsement")) {
    report.skipped.push("109-endorsement: список рекомендаций не открылся");
  }

  // ── 110-113: награда и достижение в профиле ──────────────────────────────
  // Вкладки профиля - клиентское состояние, адресом не открываются: "Зал
  // славы" включается кнопкой вкладки. Плитка и попап снимаются раздельно:
  // попап шире плитки и телепортируется в body, так что общая вырезка
  // затягивает в кадр соседние плитки решетки.
  if (
    want("110-award-tile", "111-award-popup", "112-achievement-tile", "113-achievement-popup") &&
    (await open(p, "/users/SolohinLex"))
  ) {
    const hallTab = p.locator('button[role="tab"]', { hasText: "Зал славы" });
    if (await hallTab.count()) {
      await hallTab.first().click();
      await p.waitForTimeout(1800);
    } else {
      report.skipped.push('110-113: вкладки "Зал славы" нет в профиле');
    }

    // Плитка с угловым бейджем серии: у нее попап показывает и строку
    // конкурса, и ссылки на топики - состав блока целиком.
    const withSeries = p.locator(".award:has(.award-series)").first();
    const award = (await withSeries.count())
      ? withSeries
      : p.locator(".award").first();
    if (await award.count()) {
      if (want("110-award-tile")) await snap(p, "110-award-tile", award);
      if (want("111-award-popup")) await snapTooltip(p, "111-award-popup", award);
    } else {
      report.skipped.push("110/111: наград у пользователя нет");
    }

    // Взятая цепочка: у нее есть цвет уровня, римская цифра и прогресс.
    const earned = p.locator(".chain:not(.chain--locked)").first();
    const chain = (await earned.count()) ? earned : p.locator(".chain").first();
    if (await chain.count()) {
      if (want("112-achievement-tile")) await snap(p, "112-achievement-tile", chain);
      if (want("113-achievement-popup"))
        await snapTooltip(p, "113-achievement-popup", chain);
    } else {
      report.skipped.push("112/113: достижений нет");
    }
  } else if (
    want("110-award-tile", "111-award-popup", "112-achievement-tile", "113-achievement-popup")
  ) {
    report.skipped.push("110-113: профиль не открылся");
  }

  await ctx.close();
  await browser.close();
  await guestApi.dispose();
  await authApi.dispose();

  console.log(
    `снято: ${report.ok.length}, пропущено: ${report.skipped.length}, ошибок: ${report.failed.length}`,
  );
  for (const s of report.ok) console.log("OK   " + s);
  for (const t of report.toasts) console.log("TOAST " + t);
  for (const s of report.skipped) console.log("SKIP " + s);
  for (const f of report.failed) console.log("FAIL " + f);
})();
