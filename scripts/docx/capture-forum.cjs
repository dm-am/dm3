/* Снимает контентные блоки форума и модерации: топик в двух режимах (4.2.2.17),
   комментарий (4.2.2.18) и предупреждение (4.2.2.21).

   Предупреждение снимается со страницы модерации, а не с публичного лога:
   /warnings пока заглушка "В разработке" и блоков не рисует.

   Учетные данные - сидовые, лежат в открытую в e2e/fixtures/auth.ts.
   Разрушающих действий нет: форма создания топика открывается и не
   отправляется, ничего не сохраняется. */
const {
  chromium,
  request,
} = require("../../src/DM.Web.Client/node_modules/playwright");
const fs = require("fs");
const path = require("path");

const API = process.env.API_URL || "http://localhost:5000";
const APP = process.env.APP_URL || "http://localhost:5174";
const OUT = process.env.SHOTS_DIR || path.join(__dirname, "shots");

/* Раздел с топиками и топик, у которого есть обсуждение. */
const BOARD = "/forum/newbies";
const TOPIC = "/forum/newbies/3";

const only = process.argv.slice(2);
const want = (name) => !only.length || only.some((a) => name.includes(a));

async function login(email, password) {
  const ctx = await request.newContext();
  const response = await ctx.post(API + "/v1/account/login", {
    data: { email, password },
  });
  if (!response.ok()) throw new Error("login " + email + " " + response.status());
  const { user } = await response.json();
  const state = await ctx.storageState();
  await ctx.dispose();
  return { cookies: state.cookies, user };
}

async function open(browser, url, email) {
  const context = await browser.newContext({
    viewport: { width: 1280, height: 1000 },
  });
  if (email) {
    const { cookies, user } = await login(email, "Test123!");
    await context.addCookies(cookies);
    const warm = await context.newPage();
    await warm.goto(APP);
    await warm.evaluate(
      (payload) => localStorage.setItem("user", payload),
      JSON.stringify(user),
    );
    await warm.close();
  }
  const page = await context.newPage();
  await page.goto(APP + url, { waitUntil: "networkidle" });
  return { context, page };
}

async function snap(page, selector, shot, report) {
  const target = page.locator(selector).first();
  if (!(await target.count())) {
    report.skipped.push(`${shot}: ${selector} на странице нет`);
    return false;
  }
  await target.scrollIntoViewIfNeeded();
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);
  await target.screenshot({ path: path.join(OUT, shot + ".png") });
  const box = await target.boundingBox();
  report.ok.push(`${shot} (${Math.round(box.width)}x${Math.round(box.height)})`);
  return true;
}

/* Кадр по объединенной рамке нескольких элементов: часть блоков документа
   описана как одно целое, а в разметке разнесена по соседям. */
async function snapUnion(page, selectors, shot, report) {
  const parts = selectors.map((selector) => page.locator(selector).first());
  for (let i = 0; i < parts.length; i++) {
    if (!(await parts[i].count())) {
      report.skipped.push(`${shot}: ${selectors[i]} на странице нет`);
      return false;
    }
  }

  await parts[0].scrollIntoViewIfNeeded();
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);

  const boxes = [];
  for (const part of parts) boxes.push(await part.boundingBox());
  const clip = {
    x: Math.min(...boxes.map((b) => b.x)),
    y: Math.min(...boxes.map((b) => b.y)),
    width: 0,
    height: 0,
  };
  clip.width = Math.max(...boxes.map((b) => b.x + b.width)) - clip.x;
  clip.height = Math.max(...boxes.map((b) => b.y + b.height)) - clip.y;

  await page.screenshot({ path: path.join(OUT, shot + ".png"), clip });
  report.ok.push(`${shot} (${Math.round(clip.width)}x${Math.round(clip.height)})`);
  return true;
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();
  const report = { ok: [], skipped: [] };

  try {
    if (want("119-topic")) {
      // Блок топика живет на странице топика: в списке раздела строки другие.
      const view = await open(browser, TOPIC);
      await snap(view.page, ".topic", "119-topic", report);
      await view.context.close();
    }

    if (want("120-topic-create")) {
      // Вошедшим: гостю кнопки создания топика не показывают, а режим просмотра
      // от входа не меняется.
      const board = await open(browser, BOARD, "user@test.local");
      {
        const button = board.page.locator(".create-topic-button").first();
        if (await button.count()) {
          await button.click();
          await board.page.waitForTimeout(800);
          // Форма лежит не внутри полосы с кнопкой, а в соседнем блоке
          // раскрытия: снимок одной полосы дал бы кадр с кнопкой "Отмена".
          await snapUnion(
            board.page,
            [".create-topic-section", ".create-topic-section + .expand-zone"],
            "120-topic-create",
            report,
          );
        } else {
          report.skipped.push("120-topic-create: кнопки создания нет");
        }
      }
      await board.context.close();
    }

    if (want("121-comment")) {
      const topic = await open(browser, TOPIC);
      await snap(topic.page, ".comment", "121-comment", report);
      await topic.context.close();
    }

    if (want("122-warning")) {
      const moderation = await open(browser, "/moderation/warnings", "mod@test.local");
      await snap(moderation.page, ".warning-card", "122-warning", report);
      await moderation.context.close();
    }
  } finally {
    await browser.close();
  }

  report.ok.forEach((line) => console.log("OK  ", line));
  report.skipped.forEach((line) => console.log("--  ", line));
  console.log(`снято: ${report.ok.length}, пропущено: ${report.skipped.length}`);
})();
