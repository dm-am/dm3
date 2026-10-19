import { expect } from "../../fixtures/auth";
import { test, type Role } from "../../fixtures/roles";
import { block } from "../../fixtures/sidebar";
import { settle } from "../../fixtures/window";

/**
 * The left column of the sidebar: its blocks for a guest and for a signed-in
 * reader, the rows of the games and the boards of the forum, and its menus.
 */
test.describe("Sidebars", () => {
  test.describe("Left Sidebar - Guest", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display left sidebar", async ({ page }) => {
      await expect(page.locator(".sidebar-left")).toBeVisible();
    });

    for (const [name, token] of [
      ["recruiting games", "RecruitingGames"],
      ["active games", "ActiveGames"],
      ["finished games", "FinishedGames"],
      ["active blogs", "ActiveBlogs"],
      ["forum boards", "ForumBoards"],
    ]) {
      test(`should display ${name} section`, async ({ page }) => {
        await expect(block(page, token)).toBeVisible();
      });
    }

    test("should not display owned games for guests", async ({ page }) => {
      await expect(block(page, "OwnedGames")).toBeHidden();
    });

    test("should not display owned blogs for guests", async ({ page }) => {
      await expect(block(page, "OwnedBlogs")).toBeHidden();
    });
  });

  test.describe("Left Sidebar - Authenticated", () => {
    test.beforeEach(async ({ authenticatedPage }) => {
      await authenticatedPage.goto("/");
    });

    test("should display owned games section when authenticated", async ({
      authenticatedPage,
    }) => {
      await expect(block(authenticatedPage, "OwnedGames")).toBeVisible();
    });

    test("should display owned blogs section when authenticated", async ({
      authenticatedPage,
    }) => {
      await expect(block(authenticatedPage, "OwnedBlogs")).toBeVisible();
    });

    test("lists the games the signed-in user takes part in", async ({
      authenticatedPage,
    }) => {
      // The seeded account hosts and plays games, so the block has entries.
      const rows = block(authenticatedPage, "OwnedGames").locator("li.link a");
      await expect(rows.first()).toBeVisible();
    });
  });

  test.describe("Recruiting Games", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should show recruiting games as rows", async ({ page }) => {
      const rows = block(page, "RecruitingGames").locator("li.link a");
      await expect(rows.first()).toBeVisible();
    });
  });

  test.describe("Active Games", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("pins the unread counters open on every active game row", async ({
      page,
    }) => {
      // The block passes always-show-counters, so the pair is not a hover
      // affordance here.
      const rows = page.locator("#sidebar-list-ActiveGames li.link");
      await expect(rows.first()).toBeVisible();
      await expect(rows.first().locator(".counters")).toBeVisible();
    });
  });

  test.describe("Forum Boards", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display board names", async ({ page }) => {
      const boards = block(page, "ForumBoards").locator("li.board-link a");
      await expect(boards.first()).toBeVisible();
    });

    test("should navigate to forum board on click", async ({ page }) => {
      const boardLink = block(page, "ForumBoards")
        .locator("li.board-link a")
        .first();

      await boardLink.click();
      await expect(page).toHaveURL(/\/forum\//);
    });
  });

  test.describe("Game Link Component", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display game title", async ({ page }) => {
      const gameLink = page.locator("#sidebar-list-ActiveGames li.link a");

      await expect(gameLink.first()).toHaveText(/\S/);
    });

    test("should display master name", async ({ page }) => {
      // The master is in the tooltip that wraps the title. A bare "a" also
      // matches the block's own header link, which carries no tooltip.
      const gameLink = page
        .locator("#sidebar-list-ActiveGames li.link a")
        .first();
      await expect(gameLink).toBeVisible();
      await gameLink.hover();
      await expect(page.locator('[role="tooltip"]')).toContainText("Мастер:");
    });

    test("should show unread indicator when applicable", async ({
      authenticatedPage,
    }) => {
      await authenticatedPage.goto("/");

      // "Мои игры" pins the counters open too, so the unread pair is on the
      // row whatever its numbers are.
      const rows = authenticatedPage.locator(
        "#sidebar-list-OwnedGames li.link",
      );
      await expect(rows.first()).toBeVisible();
      await expect(rows.first().locator(".counters")).toBeVisible();
    });
  });
});

/**
 * The menus of the left column. A row of a menu is a row too: one line, its
 * count beside its name. The
 * labels of moderation are written in the client, so a longer one is caught
 * here before a moderator sees it break; the boards of the forum are read as
 * the seed names them. The administrator sees every row of moderation.
 */
const MENUS: { path: string; role: Role; list: string }[] = [
  {
    path: "/moderation/bans",
    role: "admin",
    list: "#sidebar-list-ModerationPanel",
  },
  { path: "/forum/general", role: "user", list: "#sidebar-list-ForumBoards" },
];

test.describe("Menus of the left column on one line", () => {
  for (const { path, role, list } of MENUS) {
    test(`${list} on ${path} as ${role} keeps every row on one line`, async ({
      openAs,
    }) => {
      const page = await openAs(role, 1366, {
        path,
        roles: [role],
        ready: "",
      });
      const rows = page.locator(`aside.sidebar-left ${list} > li`);
      await expect(rows.locator("a").first()).toBeVisible({ timeout: 15000 });
      await settle(page);
      // A row taller than its line stands on two.
      const broken = await rows.evaluateAll((items) =>
        items
          .filter(
            (li) =>
              li.getBoundingClientRect().height >
              parseFloat(getComputedStyle(li).lineHeight) + 1,
          )
          .map((li) => (li.textContent ?? "").trim()),
      );
      expect(await rows.count()).toBeGreaterThan(9);
      expect(broken).toEqual([]);
    });
  }
});
