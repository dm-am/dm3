import { test, expect } from "../../fixtures/auth";
import type { Locator, Page } from "@playwright/test";

/**
 * The two sidebar columns and the blocks in them.
 *
 * What this file was: `.sidebar-block`, `.sidebar-block-header`,
 * `.sidebar-block-content`, `.left-sidebar`, `.right-sidebar`,
 * `.owned-games`, `.popular-games`, `.recruiting-games`, `.active-games`,
 * `.active-polls`, `.game-tag-cloud`, `.forum-boards`, `.contact-forms`,
 * `.support-us`, `.collapse-icon` — none of them is written anywhere in the
 * client. `SidebarBlock` renders an `<li>` with an `h4.sidebar-title` and a
 * `ul#sidebar-list-<token>`; the shell columns are `.sidebar-left` and
 * `.sidebar-right`. Every one of those tests therefore looked at nothing,
 * and the ones with a `count() > 0` guard around the assertion said so out
 * loud.
 *
 * So the blocks are addressed the way the component identifies them — by
 * token — and no assertion sits inside a condition.
 */

/** The list a sidebar block renders its rows into. */
const block = (page: Page, token: string): Locator =>
  page.locator(`#sidebar-list-${token}`);

/**
 * The clip the fold collapses, which is what "collapsed" means on screen.
 *
 * Not the list itself: collapsing is the CSS fold (Reset.sass — `.expand-fold`
 * goes to `grid-template-rows: 0fr` and the clip hides its overflow), so the
 * list keeps its own height and its own bounding box the whole time. An
 * assertion that the list is hidden is red with the block collapsed and red
 * with it open, which is the same "looked at nothing" this file was rewritten
 * to stop doing.
 */
const blockFold = (page: Page, token: string): Locator =>
  page.locator(`.expand-fold-clip:has(#sidebar-list-${token})`);

/** The +/- control in the block heading; it carries the expanded state. */
const blockToggle = (page: Page, token: string): Locator =>
  page.locator(`#sidebar-toggle-${token}`);

test.describe("Sidebars", () => {
  test.describe("Left Sidebar - Guest", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display left sidebar", async ({ page }) => {
      await expect(page.locator(".sidebar-left")).toBeVisible();
    });

    test("should display recruiting games section", async ({ page }) => {
      await expect(block(page, "RecruitingGames")).toBeVisible();
    });

    test("should display active games section", async ({ page }) => {
      await expect(block(page, "ActiveGames")).toBeVisible();
    });

    test("should display finished games section", async ({ page }) => {
      await expect(block(page, "FinishedGames")).toBeVisible();
    });

    test("should display active blogs section", async ({ page }) => {
      await expect(block(page, "ActiveBlogs")).toBeVisible();
    });

    test("should display forum boards section", async ({ page }) => {
      await expect(block(page, "ForumBoards")).toBeVisible();
    });

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
      // Asserting count >= 0 passed even when the block rendered nothing.
      const rows = block(authenticatedPage, "OwnedGames").locator("li.link a");
      await expect(rows.first()).toBeVisible();
    });
  });

  test.describe("Right Sidebar", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display right sidebar", async ({ page }) => {
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });

    test("should display active polls section", async ({ page }) => {
      await expect(block(page, "ActivePolls")).toBeVisible();
    });

    test("should display popular games section", async ({ page }) => {
      await expect(block(page, "PopularGames")).toBeVisible();
    });

    test("should display popular blogs section", async ({ page }) => {
      await expect(block(page, "PopularBlogs")).toBeVisible();
    });

    test("should display tag cloud section", async ({ page }) => {
      await expect(block(page, "GameTags")).toBeVisible();
    });

    test("should display contact forms section", async ({ page }) => {
      await expect(block(page, "ContactForms")).toBeVisible();
    });

    test("should display support section", async ({ page }) => {
      await expect(block(page, "SupportUs")).toBeVisible();
    });

    test("should display partners section", async ({ page }) => {
      await expect(block(page, "Partners")).toBeVisible();
    });
  });

  test.describe("SidebarBlock - Collapse/Expand", () => {
    test.beforeEach(async ({ page }) => {
      // Reset the collapse state. The key is the component's own
      // (`__HideMenuModule_<token>__`); the "sidebar-" prefix this used to
      // clear matches nothing the client stores.
      await page.goto("/");
      await page.evaluate(() => {
        Object.keys(localStorage)
          .filter((key) => key.startsWith("__HideMenuModule_"))
          .forEach((key) => localStorage.removeItem(key));
      });
      await page.reload();
    });

    test("should toggle block collapse on toggle click", async ({ page }) => {
      const fold = blockFold(page, "PopularGames");
      const toggle = blockToggle(page, "PopularGames");

      // Initially expanded. Only the +/- control toggles: the heading text
      // is deliberately not clickable, which is why clicking the header
      // asserted nothing.
      await expect(fold).toBeVisible();

      await toggle.click();
      await expect(fold).toBeHidden();

      await toggle.click();
      await expect(fold).toBeVisible();
    });

    test("should persist collapse state in localStorage", async ({ page }) => {
      await blockToggle(page, "PopularGames").click();
      await expect(blockFold(page, "PopularGames")).toBeHidden();

      await page.reload();

      await expect(blockFold(page, "PopularGames")).toBeHidden();
    });

    test("should announce the collapse state on the toggle", async ({
      page,
    }) => {
      const toggle = blockToggle(page, "PopularGames");

      await expect(toggle).toHaveAttribute("aria-expanded", "true");
      await toggle.click();
      await expect(toggle).toHaveAttribute("aria-expanded", "false");
    });
  });

  test.describe("Popular Games", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display game links in popular games", async ({ page }) => {
      const rows = block(page, "PopularGames").locator("li.link a");
      await expect(rows.first()).toBeVisible();
    });

    test("should navigate to game page on click", async ({ page }) => {
      const gameLink = block(page, "PopularGames").locator("li.link a").first();

      await gameLink.click();
      await expect(page).toHaveURL(/\/game\//);
    });
  });

  test.describe("Recruiting Games", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display recruiting games list", async ({ page }) => {
      await expect(block(page, "RecruitingGames")).toBeVisible();
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

    test("should display active games list", async ({ page }) => {
      await expect(block(page, "ActiveGames")).toBeVisible();
    });

    test("pins the unread counters open on every active game row", async ({
      page,
    }) => {
      // The block passes always-show-counters, so the pair is not a hover
      // affordance here. There is no activity time in this row and never was:
      // the old title described one and the body asserted nothing at all.
      const rows = page.locator("#sidebar-list-ActiveGames li.link");
      await expect(rows.first()).toBeVisible();
      await expect(rows.first().locator(".counters")).toBeVisible();
    });
  });

  test.describe("Game Tag Cloud", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display tag cloud", async ({ page }) => {
      await expect(block(page, "GameTags").locator(".tag-cloud")).toBeVisible();
    });

    test("should size the tags by popularity", async ({ page }) => {
      const tags = block(page, "GameTags").locator(".tag-cloud .tag");
      await expect(tags.first()).toBeVisible();

      const size = await tags
        .first()
        .evaluate((el) => window.getComputedStyle(el).fontSize);
      expect(size).toMatch(/^\d+(\.\d+)?px$/);
    });

    test("should navigate to games filtered by tag on click", async ({
      page,
    }) => {
      const tag = block(page, "GameTags").locator(".tag-cloud .tag").first();

      await tag.click();
      await expect(page).toHaveURL(/\/games\?.*requiredTags/i);
    });
  });

  test.describe("Active Polls", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display active polls section", async ({ page }) => {
      await expect(block(page, "ActivePolls")).toBeVisible();
    });

    test("should display poll if exists", async ({ page }) => {
      const poll = block(page, "ActivePolls").locator(".poll").first();

      await expect(poll).toBeVisible();
      await expect(poll.locator(".poll-title")).toBeVisible();
    });
  });

  test.describe("Poll Voting", () => {
    test("should display poll options", async ({ page }) => {
      await page.goto("/");
      const poll = page.locator("#sidebar-list-ActivePolls .poll").first();

      await expect(poll).toBeVisible();
      await expect(poll.locator(".poll-option-row").first()).toBeVisible();
    });

    test("should show vote button for authenticated users", async ({
      authenticatedPage,
    }) => {
      await authenticatedPage.goto("/");

      const poll = authenticatedPage
        .locator("#sidebar-list-ActivePolls .poll")
        .first();
      await expect(poll).toBeVisible();
      // Either "Проголосовать" on every option or "Отменить голос" on the one
      // already chosen: an active poll offers a signed-in reader one of the
      // two, and offers a guest neither.
      await expect(poll.locator(".poll-option-vote").first()).toBeVisible();
    });

    test("shows the tally next to every option", async ({
      authenticatedPage,
    }) => {
      await authenticatedPage.goto("/");

      const poll = authenticatedPage
        .locator("#sidebar-list-ActivePolls .poll")
        .first();
      await expect(poll).toBeVisible();
      await expect(poll.locator(".poll-option-count").first()).toHaveText(
        /\(\d+\)/,
      );
    });
  });

  test.describe("Forum Boards", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display forum boards list", async ({ page }) => {
      await expect(block(page, "ForumBoards")).toBeVisible();
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

  test.describe("Contact Forms", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display contact forms section", async ({ page }) => {
      await expect(
        block(page, "ContactForms").locator(".contact-links"),
      ).toBeVisible();
    });

    test("should have support link", async ({ page }) => {
      const supportLink = block(page, "ContactForms")
        .locator(".contact-item a")
        .filter({ hasText: /поддержк/i });

      await expect(supportLink).toHaveAttribute("href", "/support");
    });

    test("should have complaint link", async ({ page }) => {
      const complaintLink = block(page, "ContactForms")
        .locator(".contact-item a")
        .filter({ hasText: /жалоб/i });

      await expect(complaintLink).toHaveAttribute("href", "/complaint");
    });
  });

  test.describe("Support Us", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display support section", async ({ page }) => {
      await expect(block(page, "SupportUs")).toBeVisible();
    });

    test("should display progress bar", async ({ page }) => {
      // No visibility guard around the assertion: the guard repeated the
      // assertion word for word, so the test passed by skipping itself.
      await expect(block(page, "SupportUs").locator(".progress")).toBeVisible();
    });

    test("should have donation link", async ({ page }) => {
      const donateLink = block(page, "SupportUs").locator("a").first();

      await expect(donateLink).toHaveAttribute("href", /\S/);
    });
  });

  test.describe("Partners", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display partners section", async ({ page }) => {
      await expect(block(page, "Partners").locator(".partners")).toBeVisible();
    });

    test("should display partner links", async ({ page }) => {
      const partnerLinks = block(page, "Partners").locator(".partner-link");

      await expect(partnerLinks.first()).toHaveAttribute("href", /\S/);
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
      // The row is a title; the master is in the tooltip that wraps it, which
      // is where the old guard stopped without asserting anything.
      const gameLink = page.locator("#sidebar-list-ActiveGames a").first();
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

  test.describe("Blog Link Component", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display blog title", async ({ page }) => {
      // Same as above: the guard was the assertion.
      const rows = block(page, "PopularBlogs").locator("li.link a");
      await expect(rows.first()).toBeVisible();
    });

    test("should navigate to blog on click", async ({ page }) => {
      const blogLink = block(page, "PopularBlogs").locator("li.link a").first();

      await blogLink.click();
      await expect(page).toHaveURL(/\/blogs\//);
    });
  });

  test.describe("Sidebar Responsiveness", () => {
    test("should hide sidebars on mobile viewport", async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 667 });
      await page.goto("/");

      // Below the shell breakpoint the left column is gone and its content is
      // reachable through the burger drawer; the right column reflows under
      // the page instead of disappearing.
      await expect(page.locator(".sidebar-left")).toBeHidden();
      await expect(page.locator(".burger-btn")).toBeVisible();
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });

    test("should show sidebars on desktop viewport", async ({ page }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });
      await page.goto("/");

      await expect(page.locator(".sidebar-left")).toBeVisible();
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });
  });

  test.describe("Sidebar on Different Pages", () => {
    test("should display sidebars on home page", async ({ page }) => {
      await page.goto("/");
      await expect(page.locator(".sidebar-left")).toBeVisible();
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });

    test("should display sidebars on games page", async ({ page }) => {
      await page.goto("/games");
      await expect(page.locator(".sidebar-left")).toBeVisible();
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });

    test("should display sidebars on forum page", async ({ page }) => {
      await page.goto("/forum");
      await expect(page.locator(".sidebar-left")).toBeVisible();
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });

    test("should display sidebars on community page", async ({ page }) => {
      await page.goto("/community");
      await expect(page.locator(".sidebar-left")).toBeVisible();
      await expect(page.locator(".sidebar-right")).toBeVisible();
    });
  });
});
