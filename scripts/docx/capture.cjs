/* Снимает все страницы сайта: гостем и под сид-аккаунтом SolohinLex (Admin).
   Учетные данные — сидовые, лежат в открытую в e2e/fixtures/auth.ts. */
const { chromium, request } = require("../../src/DM.Web.Client/node_modules/playwright");
const fs = require("fs");
const path = require("path");

const API = "http://localhost:5000";
const APP = "http://localhost:5174";
const OUT = process.env.SHOTS_DIR || require("path").join(__dirname, "shots");

/* Пауза между кадрами. Глобальный лимитер - 300 запросов в минуту на адрес,
   страница стоит десяток запросов, так что двух секунд хватает с запасом. */
const PACE_MS = Number(process.env.SHOT_PACE_MS || 2000);

const report = { ok: [], skipped: [], failed: [] };

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
   содержимое страницы. Кадр снимается только с чистой страницы: рендер
   проверяется на тексты отказов, и попавшийся кадр переснимается после паузы. */
const REFUSAL_MARKS = [
  "Слишком много запросов",
  "Не удалось загрузить",
  "Что-то пошло не так",
  "Повторить попытку",
];

async function pageIsClean(page) {
  const hits = await page.evaluate((marks) => {
    const text = document.body ? document.body.innerText : "";
    return marks.filter((m) => text.includes(m));
  }, REFUSAL_MARKS);
  return hits.length === 0;
}

async function shoot(page, name, url, attempt = 1) {
  try {
    await page
      .goto(APP + url, { waitUntil: "networkidle", timeout: 25000 })
      .catch(() => page.waitForTimeout(1500));
    await page.waitForTimeout(900);

    if (!(await pageIsClean(page))) {
      if (attempt >= 3) {
        report.failed.push(name + ": отказ на странице, три попытки");
        return;
      }
      // Окно лимитера - минута; ждем его целиком, а не полсекунды.
      await page.waitForTimeout(attempt * 30000);
      return shoot(page, name, url, attempt + 1);
    }
    const h = await page.evaluate(() => document.documentElement.scrollHeight);
    const opts =
      h <= 2400
        ? { fullPage: true }
        : { clip: { x: 0, y: 0, width: 1280, height: 2400 } };
    await page.screenshot({ path: path.join(OUT, name + ".png"), ...opts });
    report.ok.push(name + (attempt > 1 ? " (пересъемка)" : ""));
    await page.waitForTimeout(PACE_MS);
  } catch (e) {
    report.failed.push(name + ": " + String(e.message).split("\n")[0]);
  }
}

async function firstHref(page, url, selector) {
  try {
    await page.goto(APP + url, { waitUntil: "networkidle", timeout: 25000 });
    await page.waitForTimeout(700);
    return await page
      .locator(selector)
      .first()
      .getAttribute("href", { timeout: 4000 })
      .finally(() => page.waitForTimeout(PACE_MS));
  } catch {
    return null;
  }
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });

  const adminState = await login("admin@test.local", "Test123!");

  // ── Разрешение динамических идентификаторов через API ────────────────────
  const guestApi = await request.newContext();
  const authApi = await request.newContext({ storageState: adminState });

  const rooms = (await apiGet(guestApi, "/v1/games/aaaaa/rooms"))?.resources ?? [];
  const postRoom = rooms.find(
    (r) => !String(r.type ?? r.roomType ?? "").toLowerCase().includes("chat"),
  );
  const chatRoom = rooms.find((r) =>
    String(r.type ?? r.roomType ?? "").toLowerCase().includes("chat"),
  );

  const chars =
    (await apiGet(guestApi, "/v1/games/aaaaa/characters"))?.resources ?? [];
  const anyChar = chars[0];
  const ownChar =
    chars.find((c) => c.author?.username === "SolohinLex") ?? anyChar;

  const blogs = (await apiGet(guestApi, "/v1/blogs?take=50"))?.resources ?? [];
  const ownBlog =
    blogs.find((b) => b.author?.username === "SolohinLex") ?? blogs[0];
  const blogId = ownBlog?.publicId || ownBlog?.id;

  let pubId = null;
  for (const u of [
    `/v1/blogs/${blogId}/publications?take=1`,
    `/v1/blogs/${blogId}/feed?take=1`,
  ]) {
    const j = await apiGet(authApi, u);
    if (j?.resources?.length) {
      pubId = j.resources[0].id;
      break;
    }
  }

  const browser = await chromium.launch({ headless: true });

  const mk = async (state) =>
    browser.newContext({
      viewport: { width: 1280, height: 800 },
      storageState: state,
      reducedMotion: "reduce",
    });

  // ── Гость ────────────────────────────────────────────────────────────────
  const guestCtx = await mk(undefined);
  const g = await guestCtx.newPage();

  const guestPages = [
    ["01-home", "/"],
    ["02-about", "/about"],
    ["03-testimonials", "/testimonials"],
    ["04-polls", "/polls"],
    ["05-statistics", "/statistics"],
    ["06-community", "/community"],
    ["07-pulse", "/pulse"],
    ["08-global-chat", "/global-chat"],
    ["09-warnings", "/warnings"],
    ["10-privacy", "/privacy"],
    ["11-agreement", "/agreement"],
    ["12-support", "/support"],
    ["13-complaint", "/complaint"],
    ["14-support-track", "/support/track/demo-token"],
    ["15-profile", "/users/SolohinLex"],
    ["16-profile-about", "/users/SolohinLex/about"],
    ["17-profile-games", "/users/SolohinLex/games"],
    ["18-profile-blogs", "/users/SolohinLex/blogs"],
    ["19-profile-topics", "/users/SolohinLex/topics"],
    ["20-profile-achievements", "/users/SolohinLex/achievements"],
    ["21-received-reviews", "/users/SolohinLex/received-reviews"],
    ["22-given-reviews", "/users/SolohinLex/given-reviews"],
    ["23-received-endorsements", "/users/SolohinLex/received-endorsements"],
    ["24-given-endorsements", "/users/SolohinLex/given-endorsements"],
    ["25-received-game-reviews", "/users/SolohinLex/received-game-reviews"],
    ["26-given-game-reviews", "/users/SolohinLex/given-game-reviews"],
    ["27-profile-uploads", "/users/SolohinLex/uploads"],
    ["28-forum", "/forum"],
    ["29-forum-general", "/forum/general"],
    ["30-topic", "/forum/general/4"],
    ["31-games", "/games"],
    ["32-game", "/game/aaaaa"],
    postRoom && ["33-game-room", `/game/aaaaa/rooms/${postRoom.roomNumber}`],
    chatRoom && [
      "34-game-chat-room",
      `/game/aaaaa/chat-rooms/${chatRoom.roomNumber}`,
    ],
    ["35-game-characters", "/game/aaaaa/characters"],
    anyChar && ["36-game-character", `/game/aaaaa/characters/${anyChar.id}`],
    ["37-game-comments", "/game/aaaaa/comments"],
    ["38-game-reviews", "/game/aaaaa/reviews"],
    ["39-game-post-reviews", "/game/aaaaa/post-reviews"],
    ["40-blogs", "/blogs"],
    blogId && ["41-blog", `/blogs/${blogId}`],
    blogId && ["42-blog-feed", `/blogs/${blogId}/feed`],
    blogId && ["43-blog-comments", `/blogs/${blogId}/comments`],
    ["44-error-404", "/error/404"],
    ["45-error-401", "/error/401"],
    ["46-reset-password", "/reset-password"],
    ["47-confirm-email", "/confirm-email"],
    ["48-modal-login", "/?action=login"],
    ["49-modal-register", "/?action=register"],
    ["50-modal-recovery", "/?action=reset"],
  ].filter(Boolean);

  for (const [name, url] of guestPages) await shoot(g, name, url);
  await guestCtx.close();

  // ── Пользователь и модерация: SolohinLex, Admin ──────────────────────────
  const userCtx = await mk(adminState);
  const u = await userCtx.newPage();

  const chatHref = await firstHref(u, "/messenger", 'a[href^="/messenger/c/"]');
  const ticketHref = await firstHref(
    u,
    "/moderation/support",
    'a[href^="/moderation/tickets/"]',
  );
  const seriesHref = await firstHref(
    u,
    "/moderation/awards",
    'a[href^="/moderation/awards/series/"]',
  );

  const userPages = [
    ["51-account", "/account"],
    ["52-messenger", "/messenger"],
    chatHref && ["53-chat", chatHref],
    ["54-direct-message", "/messenger/user/TestUser"],
    ["55-notifications", "/notifications"],
    ["56-subscriptions", "/subscriptions"],
    ["57-notepad", "/notepad"],
    ["58-my-tickets", "/my-tickets"],
    ["59-rules", "/rules"],
    ["60-blogs-create", "/blogs/create"],
    blogId && ["61-pub-create", `/blogs/${blogId}/feed/create`],
    blogId &&
      pubId && ["62-pub-edit", `/blogs/${blogId}/feed/${pubId}/edit`],
    ["63-game-notes", "/game/aaaaa/notes"],
    ["64-game-settings", "/game/aaaaa/settings"],
    blogId && ["65-blog-notes", `/blogs/${blogId}/notes`],
    blogId && ["66-blog-settings", `/blogs/${blogId}/settings`],
    ["67-char-create", "/game/aaaaa/characters/create"],
    ownChar && ["68-char-edit", `/game/aaaaa/characters/${ownChar.id}/edit`],
    ["69-games-create", "/games/create"],
    ["70-mod-overview", "/moderation"],
    ["71-mod-moderators", "/moderation/moderators"],
    ["72-mod-games", "/moderation/games"],
    ["73-mod-blogs", "/moderation/blogs"],
    ["74-mod-bans", "/moderation/bans"],
    ["75-mod-warnings", "/moderation/warnings"],
    ["76-mod-rated-posts", "/moderation/rated-posts"],
    ["77-mod-new-users", "/moderation/new-users"],
    ["78-mod-violators", "/moderation/violators"],
    ["79-mod-support", "/moderation/support"],
    ["80-mod-complaints", "/moderation/complaints"],
    ticketHref && ["81-mod-ticket", ticketHref],
    ["82-mod-uploads", "/moderation/uploads"],
    ["83-mod-username-changes", "/moderation/username-changes"],
    ["84-mod-tags", "/moderation/tags"],
    ["85-mod-awards", "/moderation/awards"],
    seriesHref && ["86-mod-awards-series", seriesHref],
    ["87-mod-award-types", "/moderation/award-types"],
    ["88-mod-achievements", "/moderation/achievements"],
    ["89-mod-fundraising", "/moderation/fundraising"],
  ].filter(Boolean);

  const wanted = new Set([
    "33-game-room",
    "34-game-chat-room",
    "36-game-character",
    "53-chat",
    "62-pub-edit",
    "81-mod-ticket",
    "86-mod-awards-series",
  ]);
  for (const n of wanted) {
    const listed = [...guestPages, ...userPages].some(([name]) => name === n);
    if (!listed) report.skipped.push(n + ": идентификатор не разрешился");
  }

  for (const [name, url] of userPages) await shoot(u, name, url);
  await userCtx.close();

  await browser.close();
  await guestApi.dispose();
  await authApi.dispose();

  fs.writeFileSync(
    path.join(OUT, "_report.json"),
    JSON.stringify(report, null, 1),
    "utf8",
  );
  console.log(
    `снято: ${report.ok.length}, пропущено: ${report.skipped.length}, ошибок: ${report.failed.length}`,
  );
  for (const s of report.skipped) console.log("SKIP " + s);
  for (const f of report.failed) console.log("FAIL " + f);
})();
