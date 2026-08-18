/* Снимает каркас страницы - хэдер и футер (разделы 4.2.1.1 и 4.2.1.2).

   Хэдер снимается дважды: гостю он показывает кнопки входа и регистрации,
   вошедшему - приветствие, счетчики и меню пользователя, и документ описывает
   оба состояния отдельными мокапами.

   Вошедший здесь - обычный пользователь, а не администратор: у администратора
   в меню есть пункт "Модерация", которого у читателя этого мокапа не будет.

   Учетные данные - сидовые, лежат в открытую в e2e/fixtures/auth.ts.
   Разрушающих действий нет, только чтение главной страницы. */
const {
  chromium,
  request,
} = require("../../src/DM.Web.Client/node_modules/playwright");
const fs = require("fs");
const path = require("path");

const API = process.env.API_URL || "http://localhost:5000";
const APP = process.env.APP_URL || "http://localhost:5174";
const OUT = process.env.SHOTS_DIR || path.join(__dirname, "shots");

async function login(email, password) {
  const ctx = await request.newContext();
  const response = await ctx.post(API + "/v1/account/login", {
    data: { email, password },
  });
  if (!response.ok()) {
    throw new Error("login " + response.status() + ": " + (await response.text()));
  }
  const { user } = await response.json();
  const state = await ctx.storageState();
  await ctx.dispose();
  return { cookies: state.cookies, user };
}

const only = process.argv.slice(2);
const want = (name) => !only.length || only.some((a) => name.includes(a));

async function snap(page, selector, shot, report) {
  const target = page.locator(selector).first();
  if (!(await target.count())) {
    report.skipped.push(`${shot}: ${selector} на странице нет`);
    return;
  }
  await target.scrollIntoViewIfNeeded();
  await page.mouse.move(2, 2);
  await page.waitForTimeout(400);
  await target.screenshot({ path: path.join(OUT, shot + ".png") });
  const box = await target.boundingBox();
  report.ok.push(`${shot} (${Math.round(box.width)}x${Math.round(box.height)})`);
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();
  const report = { ok: [], skipped: [] };

  try {
    // Гость: отдельный контекст без куки, иначе сессия протекла бы в кадр.
    const guest = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const guestPage = await guest.newPage();
    await guestPage.goto(APP, { waitUntil: "networkidle" });
    if (want("chrome-header-guest")) {
      await snap(guestPage, "header.header", "chrome-header-guest", report);
    }
    if (want("chrome-footer")) {
      await snap(guestPage, "footer.footer", "chrome-footer", report);
    }
    await guest.close();

    if (want("chrome-header-user")) {
      const { cookies, user } = await login("user@test.local", "Test123!");
      const authed = await browser.newContext({ viewport: { width: 1280, height: 900 } });
      await authed.addCookies(cookies);
      const page = await authed.newPage();
      await page.goto(APP);
      await page.evaluate(
        (payload) => localStorage.setItem("user", payload),
        JSON.stringify(user),
      );
      await page.goto(APP, { waitUntil: "networkidle" });
      await snap(page, "header.header", "chrome-header-user", report);
      await authed.close();
    }
  } finally {
    await browser.close();
  }

  report.ok.forEach((line) => console.log("OK  ", line));
  report.skipped.forEach((line) => console.log("--  ", line));
  console.log(`снято: ${report.ok.length}, пропущено: ${report.skipped.length}`);
})();
