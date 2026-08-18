/* Снимает панели сайдбаров (разделы 4.2.1.x) вырезками по корневому <li>
   панели: в документе рядом стоят кадры той же плотности, и снимок во всю
   полосу увеличил бы текст вдвое.

   Панель зависит от того, кто смотрит. Списки игр и блогов описаны в документе
   гостевыми - у вошедшего на их месте другие панели. Панель игры описана тремя
   мокапами, и это три разных зрителя: участник, мастер этой игры и модератор,
   который ее не ведет.

   Учетные данные - сидовые, лежат в открытую в e2e/fixtures/auth.ts.
   Разрушающих действий нет: только чтение страниц. */
const {
  chromium,
  request,
} = require("../../src/DM.Web.Client/node_modules/playwright");
const fs = require("fs");
const path = require("path");

const API = process.env.API_URL || "http://localhost:5000";
const APP = process.env.APP_URL || "http://localhost:5174";
const OUT = process.env.SHOTS_DIR || path.join(__dirname, "shots");

/* Игра, на которой снимаются панели игры: у нее есть мастер и участники,
   и модератор в ней посторонний. */
const GAME = "a6e5ca85-77af-6bb2-999b-8e91ddb5b240";

/* Панель адресуется идентификатором своего переключателя: SidebarBlock кладет
   в него token, и это единственная стабильная зацепка - у корня класса нет. */
const PANELS = [
  // Раздел 4.2.1.9 описывает панели набора и активных игр вместе, и подпись под
  // кадром называет обе: снимок одной из них подписи бы не соответствовал.
  { tokens: ["RecruitingGames", "ActiveGames"], shot: "sb-active-games" },
  { tokens: ["ActiveBlogs"], shot: "sb-active-blogs" },
  { tokens: ["SiteAddresses"], shot: "sb-site-addresses" },
  { tokens: ["SupportUs"], shot: "sb-support" },
  {
    tokens: ["GamePanel"],
    shot: "sb-game-panel",
    url: `/game/${GAME}`,
    as: "player1@test.local",
  },
  {
    tokens: ["GamePanel"],
    shot: "sb-game-panel-master",
    url: `/game/${GAME}`,
    as: "experienced@test.local",
  },
  {
    tokens: ["GamePanel"],
    shot: "sb-game-panel-moderator",
    url: `/game/${GAME}`,
    as: "mod@test.local",
  },
];

const only = process.argv.slice(2);
const want = (name) => !only.length || only.some((a) => name.includes(a));

async function login(email, password) {
  const ctx = await request.newContext();
  const response = await ctx.post(API + "/v1/account/login", {
    data: { email, password },
  });
  if (!response.ok()) {
    throw new Error("login " + email + " " + response.status());
  }
  const { user } = await response.json();
  const state = await ctx.storageState();
  await ctx.dispose();
  return { cookies: state.cookies, user };
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch();
  const report = { ok: [], skipped: [] };

  try {
    for (const panel of PANELS) {
      if (!want(panel.shot)) continue;

      // Свой контекст на каждый кадр: сессия предыдущего зрителя в следующем
      // кадре сменила бы панель, и разница читалась бы как правка интерфейса.
      const context = await browser.newContext({
        viewport: { width: 1280, height: 1000 },
      });
      const page = await context.newPage();

      if (panel.as) {
        const { cookies, user } = await login(panel.as, "Test123!");
        await context.addCookies(cookies);
        await page.goto(APP);
        await page.evaluate(
          (payload) => localStorage.setItem("user", payload),
          JSON.stringify(user),
        );
      }

      await page.goto(APP + (panel.url || "/"), { waitUntil: "networkidle" });

      const blocks = panel.tokens.map((token) =>
        page.locator(`li:has(#sidebar-toggle-${token})`).first(),
      );

      const missing = [];
      for (let i = 0; i < blocks.length; i++) {
        if (!(await blocks[i].count())) missing.push(panel.tokens[i]);
      }
      if (missing.length) {
        report.skipped.push(`${panel.shot}: нет панелей ${missing.join(", ")}`);
        await context.close();
        continue;
      }

      await blocks[0].scrollIntoViewIfNeeded();
      // Курсор уводится с панели: заголовок показывает свернуть/развернуть по
      // наведению, и кадр с этой кнопкой отличался бы от того, что видит читатель.
      await page.mouse.move(2, 2);
      await page.waitForTimeout(400);

      const boxes = [];
      for (const block of blocks) boxes.push(await block.boundingBox());
      const clip = {
        x: Math.min(...boxes.map((b) => b.x)),
        y: Math.min(...boxes.map((b) => b.y)),
        width: 0,
        height: 0,
      };
      clip.width = Math.max(...boxes.map((b) => b.x + b.width)) - clip.x;
      clip.height = Math.max(...boxes.map((b) => b.y + b.height)) - clip.y;

      await page.screenshot({ path: path.join(OUT, panel.shot + ".png"), clip });
      report.ok.push(
        `${panel.shot} (${Math.round(clip.width)}x${Math.round(clip.height)})`,
      );
      await context.close();
    }
  } finally {
    await browser.close();
  }

  report.ok.forEach((line) => console.log("OK  ", line));
  report.skipped.forEach((line) => console.log("--  ", line));
  console.log(`снято: ${report.ok.length}, пропущено: ${report.skipped.length}`);
})();
