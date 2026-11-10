import {
  ADMIN_GAME,
  BIG_POST_GAME,
  button,
  chooseComposerFile,
  DRAFT_GAME,
  HIDDEN_GAME,
  hoverUntilTooltip,
  openFilter,
  OWNED_GAME,
  refuse,
  settingsSection,
  type State,
} from "./shared";

export const GAMES_STATES: State[] = [
  // Games
  {
    name: "guest-games-filter-status",
    role: "guest",
    url: "/games",
    whole: false,
    act: async (page) => {
      await openFilter(page);
      await button(page, /^Статус игры/).click();
    },
  },
  {
    name: "guest-games-filter-tags",
    role: "guest",
    url: "/games",
    whole: false,
    act: async (page) => {
      await openFilter(page);
      await button(page, /^Тег/).click();
      await page.getByRole("textbox").last().fill("яяяя");
    },
  },
  {
    name: "guest-games-bad-tag",
    role: "guest",
    url: "/games?requiredTags=abc",
  },
  {
    // The setting of this game starts with a Cyrillic letter among Latin ones.
    name: "guest-game-crazy-adventures",
    role: "guest",
    url: "/games",
    act: async (page) => {
      await page
        .getByPlaceholder("Поиск по названию")
        .fill("Безумные Приключения");
      await page
        .getByRole("link", { name: "Безумные Приключения" })
        .first()
        .click();
      await page.waitForURL(/\/game\/[^/]+$/);
    },
  },
  {
    // The two counters whose tooltip counts what an account still sets up.
    name: "guest-community-tooltip-games",
    role: "guest",
    url: "/community",
    whole: false,
    act: (page) =>
      hoverUntilTooltip(
        page,
        'a[aria-label^="Игр в роли ведущего:"]',
        "Оформляется:",
      ),
  },
  {
    name: "guest-community-tooltip-blogs",
    role: "guest",
    url: "/community",
    whole: false,
    act: (page) =>
      hoverUntilTooltip(page, 'a[aria-label^="Блогов:"]', "Оформляется:"),
  },
  // Games and blogs of the administrator in the statuses his first ones are not in.
  {
    name: "admin-game-frozen",
    role: "admin",
    url: "/game/aaacg",
  },
  {
    name: "admin-game-finished",
    role: "admin",
    url: "/game/aaabt",
  },
  {
    name: "admin-blog-active",
    role: "admin",
    url: "/blogs/aaaaa",
  },
  {
    name: "admin-blog-closed",
    role: "admin",
    url: "/blogs/aaaaq",
  },
  {
    // The parameters of the address under the names they had before.
    name: "member-pulse-address-params",
    role: "user",
    url: "/pulse?q=Диопсид&sort=rating&order=asc&minRating=1&createdFrom=2026-01-01",
  },
  {
    name: "member-room-address-params",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1&q=Диопсид&sort=rating&order=desc&minRating=1`,
  },
  {
    name: "member-pulse-sort-menu",
    role: "user",
    url: "/pulse",
    whole: false,
    act: async (page) => {
      await button(page, /^Сортировка/).click();
    },
  },
  {
    name: "member-pulse-two-menus",
    role: "user",
    url: "/pulse",
    whole: false,
    act: async (page) => {
      await button(page, /^Сортировка/).click();
      await openFilter(page);
    },
  },
  {
    name: "member-room-sort-menu",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1`,
    whole: false,
    act: async (page) => {
      await button(page, /^Сортировка/).click();
    },
  },
  {
    name: "member-create-game",
    role: "user",
    url: "/games/create",
  },
  {
    // A game of TestSeniorMod with one review: the member may write his own.
    name: "member-game-reviews",
    role: "user",
    url: "/game/aaaab/reviews",
  },
  {
    name: "guest-game-reviews",
    role: "guest",
    url: `/game/${ADMIN_GAME}/reviews`,
  },
  {
    name: "guest-game-reviews-garbage",
    role: "guest",
    url: `/game/${ADMIN_GAME}/reviews?number=abc`,
  },
  {
    name: "admin-game-schema-conflict",
    role: "admin",
    url: `/game/${DRAFT_GAME}/settings`,
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/games\/[^/]+\/attribute-schema/,
        "PUT",
        409,
        "Конфликт",
      );
      const section = settingsSection(page, "Схема атрибутов");
      await section.getByRole("button", { name: "+ Новый атрибут" }).click();
      await page.locator("#attr-title").fill("Тест");
      await section.getByRole("button", { name: "Готово" }).click();
      await section
        .getByRole("button", { name: "Сохранить", exact: true })
        .click();
    },
  },
  {
    // An attribute without a title goes to the server, which refuses it
    // before it writes (AttributeSpecificationRules): the refusal is its own.
    name: "admin-game-schema-empty-title",
    role: "admin",
    url: `/game/${DRAFT_GAME}/settings`,
    act: async (page) => {
      const section = settingsSection(page, "Схема атрибутов");
      await section.getByRole("button", { name: "+ Новый атрибут" }).click();
      await section.getByRole("button", { name: "Готово" }).click();
      await section
        .getByRole("button", { name: "Сохранить", exact: true })
        .click();
    },
  },
  {
    // 150 letters in the name of a new room, which the server caps at 100;
    // "Создать" is not pressed.
    name: "member-game-settings-room-title-long",
    role: "user",
    url: `/game/${OWNED_GAME}/settings`,
    act: async (page) => {
      await page.locator("#new-room-title").fill("а".repeat(150));
    },
  },
  {
    name: "admin-room-pendency-hover",
    role: "admin",
    url: `/game/${ADMIN_GAME}/rooms/1`,
    act: async (page) => {
      await button(page, "Снять ожидание хода").hover();
    },
  },
  {
    // A hidden roll is only prepared in the form: it goes out with the post,
    // which is never sent.
    name: "member-room-composer-roll",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: async (page) => {
      await page.getByLabel("Показывать всем результаты бросков").uncheck();
      await button(page, "Бросить D20").click();
    },
  },
  {
    name: "member-room-composer-attachment",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: chooseComposerFile,
  },
  {
    // "видят все" of the file of the form opened: the panel "Кто видит
    // карта.png", the first character checked and not yet taken by "Готово";
    // the post is never sent, so the file goes nowhere.
    name: "member-room-composer-attachment-selected",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: async (page) => {
      await chooseComposerFile(page);
      await button(page, "видят все").click();
      await page
        .getByRole("dialog", { name: "Кто видит карта.png" })
        .getByRole("checkbox")
        .first()
        .check();
    },
  },
  {
    // The same file after "Готово": its name green, "видят: <имя>" in its
    // line, the readers in the tooltip over the name. Nothing is sent.
    name: "member-room-composer-attachment-chosen",
    role: "user",
    url: (f) => `/game/${f.playedGame}/rooms/1`,
    act: async (page) => {
      await chooseComposerFile(page);
      await button(page, "видят все").click();
      const panel = page.getByRole("dialog", { name: "Кто видит карта.png" });
      await panel.getByRole("checkbox").first().check();
      await panel.getByRole("button", { name: "Готово" }).click();
      await page.locator(".attachments .attachment-name").hover();
      await page
        .getByRole("tooltip")
        .filter({ hasText: "Получатели" })
        .waitFor();
    },
  },
  {
    // The figure's note to a reader the map is not for: the letter alone under
    // the clip, no count beside it, no trace of the map.
    name: "guest-room-hidden-file",
    role: "guest",
    url: `/game/${HIDDEN_GAME}/rooms/1`,
    act: async (page) => {
      await page
        .getByRole("button", { name: "Вложение", exact: true })
        .last()
        .click();
    },
  },
  {
    // The file of the big post as a reader it is kept for gets it: green, its
    // readers in the tooltip. No role reads a marked file in the seed, so the
    // GET of the posts is answered with the file marked; nothing is written.
    name: "member-room-marked-file",
    role: "user",
    url: `/game/${BIG_POST_GAME}/rooms/1?number=1`,
    act: async (page) => {
      await page.route(/\/v1\/rooms\/[^/]+\/posts/, async (route) => {
        if (route.request().method() !== "GET") return route.fallback();
        const response = await route.fetch();
        const json = await response.json();
        const marked = json.resources?.find(
          (post: { attachments?: unknown[] }) => post.attachments?.length,
        );
        if (marked)
          Object.assign(marked.attachments[0], {
            recipients: "DungeonMaster, Assistant, Вася",
            addressees: [],
          });
        await route.fulfill({ response, json });
      });
      await page.reload({ waitUntil: "load" });
      const toggle = button(page, /^Вложени/);
      await toggle.click();
      // The strip opens by a transition: hovered while it moves, the name
      // slides from under the pointer and the tooltip comes late or never.
      // So the hover waits for the strip to be open and still.
      const stripId = await toggle.getAttribute("aria-controls");
      await page
        .locator(`[aria-controls="${stripId}"][aria-expanded="true"]`)
        .waitFor();
      const strip = page.locator(`#${stripId}`);
      await strip.evaluate((list) =>
        Promise.all(
          (list.closest(".expand-fold") ?? list)
            .getAnimations({ subtree: true })
            .map((animation) => animation.finished),
        ),
      );
      await strip.getByRole("link").first().hover();
      await page
        .getByRole("tooltip")
        .filter({ hasText: "Получатели" })
        .waitFor();
    },
  },
  {
    // The room's own error block, with its "Повторить".
    name: "member-room-chat-load-failed",
    role: "user",
    url: (f) => `/game/${f.playedGame}/chat-rooms/${f.chatRoomNumber}`,
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/chat-rooms\/[^/]+\/messages/,
        "GET",
        500,
        "Ошибка",
      );
      await page.reload({ waitUntil: "load" });
    },
  },
];
