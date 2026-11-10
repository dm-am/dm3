import { button, minutesAfter, openFilter, refuse, type State } from "./shared";

export const PROFILE_STATES: State[] = [
  // Profile
  {
    name: "member-profile-rating",
    role: "user",
    url: "/users/RatingPositive",
  },
  {
    name: "guest-profile-offline",
    role: "guest",
    url: "/users/RatingDisabled",
  },
  {
    // Tab until the focus is in the table of games, and left there.
    name: "member-profile-assistants-focus",
    role: "user",
    url: "/users/SolohinLex?tab=games",
    act: async (page) => {
      for (let i = 0; i < 60; i++) {
        await page.keyboard.press("Tab");
        const inTable = await page.evaluate(
          () => !!document.activeElement?.closest("table"),
        );
        if (inTable) break;
      }
    },
  },
  {
    // The own name in other letters: the own profile is not recognised.
    name: "member-profile-own-lowercase",
    role: "user",
    url: "/users/testuser",
  },
  {
    name: "member-profile-games-player",
    role: "user",
    url: "/users/SolohinLex?tab=games",
    act: async (page) => {
      await button(page, "Игрок").click();
    },
  },
  {
    // An account that only plays: the table opens on the player's games.
    name: "member-profile-games-autoswitch",
    role: "user",
    url: (f) => `/users/${f.playerOnlyUsername}?tab=games`,
  },
  {
    name: "member-profile-other-topics",
    role: "user",
    url: "/users/SolohinLex",
    act: async (page) => {
      await page.getByRole("tab", { name: "Топики", exact: true }).click();
    },
  },
  {
    name: "member-profile-achievement-popover",
    role: "user",
    url: "/users/SolohinLex/achievements",
    whole: false,
    act: async (page) => {
      await page.locator("main button, main [tabindex='0']").first().hover();
    },
  },
  {
    name: "member-profile-achievements-blocks",
    role: "user",
    url: "/users/SolohinLex/achievements",
  },
  {
    // The mentor follows the administrator: "Подписан" opens the events of the
    // subscription, and nothing in it is changed.
    name: "mentor-profile-subscribe-menu",
    role: "mentor",
    url: "/users/SolohinLex",
    whole: false,
    act: async (page) => {
      await button(page, "Подписан").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "member-profile-subscribe-failed",
    role: "user",
    url: "/users/SolohinLex",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/users\/me\/subscriptions$/,
        "POST",
        500,
        "Ошибка",
      );
      await button(page, "Подписаться").click();
    },
  },
  {
    name: "member-write-endorsement",
    role: "user",
    url: "/users/TestSeniorMod/write-endorsement",
    act: async (page) => {
      await page.getByRole("textbox").first().fill("коротко");
    },
  },
  {
    name: "guest-profile-received-endorsements",
    role: "guest",
    url: "/users/TestSeniorMod/received-endorsements",
  },
  {
    // The administrator wrote the endorsement: the clock an hour after it,
    // inside the day he may edit it, and then past that day.
    name: "admin-endorsements-edit-fresh",
    role: "admin",
    url: "/users/TestSeniorMod/received-endorsements",
    clock: (f) => minutesAfter(f.endorsementCreatedUtc, 60),
  },
  {
    name: "admin-endorsements-edit-late",
    role: "admin",
    url: "/users/TestSeniorMod/received-endorsements",
    clock: (f) => minutesAfter(f.endorsementCreatedUtc, 25 * 60),
  },
  {
    // A character added to the status, never saved.
    name: "admin-profile-own-edit",
    role: "admin",
    url: "/users/SolohinLex",
    act: async (page) => {
      await button(page, "Редактировать").click();
      const status = page.getByRole("textbox", { name: "Статус" });
      await status.press("End");
      await status.pressSequentially(".");
      await page.getByText("Несохраненные изменения").waitFor();
    },
  },
  {
    name: "admin-profile-own-uploads",
    role: "admin",
    url: "/users/SolohinLex/uploads",
  },
  {
    name: "member-community",
    role: "user",
    url: "/community",
  },
  {
    name: "guest-community-filter",
    role: "guest",
    url: "/community",
    whole: false,
    act: openFilter,
  },
  {
    // A query typed, then the search refused: what the table keeps.
    name: "guest-community-search-failed",
    role: "guest",
    url: "/community",
    act: async (page) => {
      const search = page.getByPlaceholder(/Поиск/).first();
      await search.fill("Pla");
      await page.waitForLoadState("networkidle");
      await refuse(page, /\/v1\/users\?/, "GET", 500, "Ошибка");
      await search.pressSequentially("y");
      await page.waitForLoadState("networkidle");
    },
  },
];
