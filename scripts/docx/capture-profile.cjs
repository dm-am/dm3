/* Снимает контентные блоки, живущие на странице профиля: сам блок профиля
   (4.2.2.2), личную заметку (4.2.2.3) и модераторскую заметку (4.2.2.20).

   Выделены в отдельный скрипт, потому что зависят от того, кто смотрит: личная
   заметка видна вошедшему на ЧУЖОМ профиле, модераторская - только модерации.
   Каждый блок описан двумя режимами, просмотра и редактирования, и второй
   получается нажатием на кнопку правки в первом.

   Учетные данные - сидовые, лежат в открытую в e2e/fixtures/auth.ts.
   Разрушающих действий нет: редактор открывается и закрывается отменой,
   заметки не сохраняются и не удаляются. */
const {
  chromium,
  request,
} = require("../../src/DM.Web.Client/node_modules/playwright");
const fs = require("fs");
const path = require("path");

const API = process.env.API_URL || "http://localhost:5000";
const APP = process.env.APP_URL || "http://localhost:5174";
const OUT = process.env.SHOTS_DIR || path.join(__dirname, "shots");

/* Чужой профиль: на своем личной заметки нет по замыслу. */
const PROFILE = process.env.PROFILE_PATH || "/users/SolohinLex";

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

async function openAs(browser, email) {
  const { cookies, user } = await login(email, "Test123!");
  const context = await browser.newContext({
    viewport: { width: 1280, height: 1000 },
  });
  await context.addCookies(cookies);
  const page = await context.newPage();
  await page.goto(APP);
  await page.evaluate(
    (payload) => localStorage.setItem("user", payload),
    JSON.stringify(user),
  );
  await page.goto(APP + PROFILE, { waitUntil: "networkidle" });
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

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();
  const report = { ok: [], skipped: [] };

  try {
    const reader = await openAs(browser, "user@test.local");
    if (want("114-profile-block")) {
      await snap(reader.page, "section.identity", "114-profile-block", report);
    }
    if (want("115-profile-note")) {
      await snap(reader.page, ".profile-note", "115-profile-note", report);
    }
    if (want("116-profile-note-edit")) {
      const toggle = reader.page.locator(".profile-note .note-toggle").first();
      if (await toggle.count()) {
        await toggle.click();
        await reader.page.waitForTimeout(600);
        await snap(reader.page, ".profile-note", "116-profile-note-edit", report);
      } else {
        report.skipped.push("116-profile-note-edit: кнопки правки нет");
      }
    }
    await reader.context.close();

    const moderator = await openAs(browser, "mod@test.local");
    // Панель модерации - сворачиваемый блок, и по умолчанию она свернута:
    // заметок нет в разметке, пока панель не раскрыта.
    const panel = moderator.page.locator("button.mod-header").first();
    if (await panel.count()) {
      await panel.click();
      await moderator.page.waitForTimeout(800);
    } else {
      report.skipped.push("панель модерации: заголовка нет, профиль не открыт модерации");
    }
    if (want("117-mod-note")) {
      await snap(moderator.page, ".mod-note", "117-mod-note", report);
    }
    if (want("118-mod-note-edit")) {
      const edit = moderator.page.locator(".mod-note .mod-action").first();
      if (await edit.count()) {
        await edit.click();
        await moderator.page.waitForTimeout(600);
        await snap(moderator.page, ".mod-note", "118-mod-note-edit", report);
      } else {
        report.skipped.push("118-mod-note-edit: кнопки правки нет");
      }
    }
    await moderator.context.close();
  } finally {
    await browser.close();
  }

  report.ok.forEach((line) => console.log("OK  ", line));
  report.skipped.forEach((line) => console.log("--  ", line));
  console.log(`снято: ${report.ok.length}, пропущено: ${report.skipped.length}`);
})();
