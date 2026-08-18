/* Снимает диалоговые окна и BBCode-редактор (разделы 4.2.4-4.2.5 спеки).
   Окно открывается тем же путем, что и у пользователя: переход на страницу,
   клик по кнопке, кадр с открытым окном и затемнением. Разрушающие действия
   не подтверждаются - окно подтверждения закрывается по Esc.

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
const PACE_MS = Number(process.env.SHOT_PACE_MS || 2000);

const report = { ok: [], skipped: [], failed: [], toasts: [] };

/* Аргументы - номера кадров, которые надо переснять ("91", "97"). Без них
   снимаются все: правка в одном окне не должна стоить полного прогона. */
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

/* Всплывающее уведомление - это не содержимое страницы, но его текст попадает
   в проверку на отказ и рубит кадр целиком. Уведомления закрываются перед
   проверкой, а их текст уходит в отчет: молча снимать кадр поверх ошибки
   нельзя, читатель документа должен узнать о ней от нас, а не из кадра. */
async function dismissToasts(page, name) {
  const items = page.locator(".toast-item");
  for (let i = (await items.count()) - 1; i >= 0; i--) {
    const item = items.nth(i);
    const text = (await item.innerText().catch(() => "")).replace(/\s+/g, " ").trim();
    await item.locator('button[aria-label="Закрыть"]').click().catch(() => {});
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

/* Переход на страницу с проверкой на отказ и пересъемкой после паузы.
   Пустая страница - тоже отказ: дев-сервер отдает белый экран, пока
   пересобирает зависимости, и такой кадр не отличить от сломанной верстки. */
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
  // Окно лимитера - минута, ждем его целиком; белый экран уходит быстрее.
  await page.waitForTimeout(refused ? attempt * 30000 : 4000);
  return open(page, url, attempt + 1);
}

async function snap(page, name, target) {
  await dismissToasts(page, name);
  if (!(await pageIsClean(page))) {
    report.failed.push(name + ": отказ на странице");
    return;
  }
  // Курсор остается на нажатой кнопке, и ее подсказка висит поверх окна в
  // кадре. Уводим указатель в угол и даем подсказке погаснуть.
  await page.mouse.move(2, 2);
  await page.waitForTimeout(500);
  await (target || page).screenshot({ path: path.join(OUT, name + ".png") });
  report.ok.push(name);
  await page.waitForTimeout(PACE_MS);
}

const SAMPLE =
  "[b]Жирный[/b], [i]курсив[/i], [u]подчеркнутый[/u].\n" +
  "[quote]Цитата из соседнего поста[/quote]\n" +
  "[ul][li]первый пункт[/li][li]второй пункт[/li][/ul]";

(async () => {
  fs.mkdirSync(OUT, { recursive: true });

  const adminState = await login("admin@test.local", "Test123!");
  const guestApi = await request.newContext();

  const chars =
    (await apiGet(guestApi, "/v1/games/aaaaa/characters"))?.resources ?? [];
  const anyChar = chars[0];

  const blogs = (await apiGet(guestApi, "/v1/blogs?take=50"))?.resources ?? [];
  const ownBlog =
    blogs.find((b) => b.author?.username === "SolohinLex") ?? blogs[0];
  const blogId = ownBlog?.publicId || ownBlog?.id;

  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 800 },
    storageState: adminState,
    reducedMotion: "reduce",
  });
  const p = await ctx.newPage();

  // ── 91/92: предупреждение и бан из панели модерации профиля ─────────────
  if (want("91-modal-warning", "92-modal-ban") && (await open(p, "/users/TestUser"))) {
    const modHeader = p.locator("button.mod-header");
    if (await modHeader.count()) {
      await modHeader.first().click();
      await p.waitForTimeout(800);

      for (const [name, label] of [
        ["91-modal-warning", "Вынести предупреждение"],
        ["92-modal-ban", "Забанить"],
      ]) {
        const trigger = p.locator("button", { hasText: label }).first();
        if (!(await trigger.count())) {
          report.skipped.push(name + ': нет кнопки "' + label + '"');
          continue;
        }
        await trigger.click();
        await p.waitForTimeout(900);
        if (!(await p.locator('[role="dialog"]').count())) {
          report.skipped.push(name + ": окно не открылось");
          continue;
        }
        await snap(p, name);
        await p.keyboard.press("Escape");
        await p.waitForTimeout(600);
      }
    } else {
      report.skipped.push("91-modal-warning, 92-modal-ban: нет панели модерации");
    }
  } else if (want("91-modal-warning", "92-modal-ban")) {
    report.skipped.push("91-modal-warning, 92-modal-ban: профиль не открылся");
  }

  // ── 94: окно подтверждения удаления игры (кнопка "Модерации игры") ──────
  if (want("94-confirm-delete-game") && (await open(p, "/game/aaaaa"))) {
    const del = p.locator("button.strip-action", { hasText: "Удалить игру" });
    if (await del.count()) {
      await del.first().click();
      await p.waitForTimeout(700);
      await snap(p, "94-confirm-delete-game");
      // Разрушающее действие не подтверждается: окно уходит по Esc.
      await p.keyboard.press("Escape");
      await p.waitForTimeout(500);
    } else {
      report.skipped.push('94-confirm-delete-game: нет кнопки "Удалить игру"');
    }
  } else if (want("94-confirm-delete-game")) {
    report.skipped.push("94-confirm-delete-game: страница игры не открылась");
  }

  // ── 95/96/93: редактор в двух режимах и справка по BBCode ───────────────
  if (
    want("93-bbcode-help", "95-editor-wysiwyg", "96-editor-bbcode") &&
    blogId &&
    (await open(p, `/blogs/${blogId}/feed/create`))
  ) {
    const wrap = p.locator(".bbcode-editor-wrapper").first();
    if (await wrap.count()) {
      await p.locator('button[aria-label="Режим BBCode"]').first().click();
      await p.waitForTimeout(400);
      await p.locator("textarea.bbcode-textarea").first().fill(SAMPLE);
      await p.waitForTimeout(700);
      await snap(p, "96-editor-bbcode", wrap);

      await p.locator('button[aria-label="Визуальный редактор"]').first().click();
      await p.waitForTimeout(900);
      await snap(p, "95-editor-wysiwyg", wrap);

      await p.locator('button[aria-label="Показать справку"]').first().click();
      await p.waitForTimeout(800);
      if (await p.locator(".help-dialog").count()) {
        await snap(p, "93-bbcode-help");
        await p.locator('.help-dialog button[aria-label="Закрыть"]').click();
      } else {
        report.skipped.push("93-bbcode-help: окно справки не открылось");
      }
    } else {
      report.skipped.push("93/95/96: редактор не найден на странице");
    }
  } else if (want("93-bbcode-help", "95-editor-wysiwyg", "96-editor-bbcode")) {
    report.skipped.push("93/95/96: страница создания публикации не открылась");
  }

  // ── 97: страница персонажа (4.2.3.5.13) ─────────────────────────────────
  if (want("97-game-character") && anyChar) {
    if (await open(p, `/game/aaaaa/characters/${anyChar.id}`)) {
      await snap(p, "97-game-character");
    } else {
      report.skipped.push("97-game-character: страница не открылась");
    }
  } else if (want("97-game-character")) {
    report.skipped.push("97-game-character: персонаж не разрешился");
  }

  await ctx.close();
  await browser.close();
  await guestApi.dispose();

  console.log(
    `снято: ${report.ok.length}, пропущено: ${report.skipped.length}, ошибок: ${report.failed.length}`,
  );
  for (const s of report.ok) console.log("OK   " + s);
  for (const t of report.toasts) console.log("TOAST " + t);
  for (const s of report.skipped) console.log("SKIP " + s);
  for (const f of report.failed) console.log("FAIL " + f);
})();
