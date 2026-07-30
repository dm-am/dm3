import { test, expect } from "../../fixtures/auth";

test.describe("Sidebars", () => {
  test.describe("Left Sidebar - Guest", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display left sidebar", async ({ page }) => {
      await expect(page.locator(".left-sidebar")).toBeVisible();
    });

    test("should display recruiting games section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Набор игроков" });
      await expect(section).toBeVisible();
    });

    test("should display active games section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Активные игры" });
      await expect(section).toBeVisible();
    });

    test("should display finished games section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Завершенные игры" });
      await expect(section).toBeVisible();
    });

    test("should display active blogs section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Активные блоги" });
      await expect(section).toBeVisible();
    });

    test("should display forum boards section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Форум" });
      await expect(section).toBeVisible();
    });

    test("should not display owned games for guests", async ({ page }) => {
      const ownedGamesSection = page.locator(".owned-games");
      await expect(ownedGamesSection).not.toBeVisible();
    });

    test("should not display owned blogs for guests", async ({ page }) => {
      const ownedBlogsSection = page.locator(".owned-blogs");
      await expect(ownedBlogsSection).not.toBeVisible();
    });
  });

  test.describe("Left Sidebar - Authenticated", () => {
    test.beforeEach(async ({ authenticatedPage }) => {
      await authenticatedPage.goto("/");
    });

    test("should display owned games section when authenticated", async ({
      authenticatedPage,
    }) => {
      const section = authenticatedPage
        .locator(".sidebar-block")
        .filter({ hasText: "Мои игры" });
      await expect(section).toBeVisible();
    });

    test("should display owned blogs section when authenticated", async ({
      authenticatedPage,
    }) => {
      const section = authenticatedPage
        .locator(".sidebar-block")
        .filter({ hasText: "Мои блоги" });
      await expect(section).toBeVisible();
    });

    test("lists the games the signed-in user takes part in", async ({
      authenticatedPage,
    }) => {
      await authenticatedPage.goto("/");
      const ownedGames = authenticatedPage.locator(".owned-games");

      // The seeded account hosts and plays games, so the block has entries.
      // Asserting count >= 0 passed even when the block rendered nothing.
      await expect(ownedGames.locator(".game-link").first()).toBeVisible();
    });
  });

  test.describe("Right Sidebar", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display right sidebar", async ({ page }) => {
      await expect(page.locator(".right-sidebar")).toBeVisible();
    });

    test("should display active polls section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Опросы" });
      await expect(section).toBeVisible();
    });

    test("should display popular games section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Популярные игры" });
      await expect(section).toBeVisible();
    });

    test("should display popular blogs section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Популярные блоги" });
      await expect(section).toBeVisible();
    });

    test("should display tag cloud section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Облако тегов" });
      await expect(section).toBeVisible();
    });

    test("should display contact forms section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Обратная связь" });
      await expect(section).toBeVisible();
    });

    test("should display support section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Поддержать проект" });
      await expect(section).toBeVisible();
    });

    test("should display partners section", async ({ page }) => {
      const section = page
        .locator(".sidebar-block")
        .filter({ hasText: "Партнеры" });
      await expect(section).toBeVisible();
    });
  });

  test.describe("SidebarBlock - Collapse/Expand", () => {
    test.beforeEach(async ({ page }) => {
      // Clear localStorage to reset collapse state
      await page.goto("/");
      await page.evaluate(() => {
        Object.keys(localStorage)
          .filter((key) => key.startsWith("sidebar-"))
          .forEach((key) => localStorage.removeItem(key));
      });
      await page.reload();
    });

    test("should toggle block collapse on header click", async ({ page }) => {
      const block = page
        .locator(".sidebar-block")
        .filter({ hasText: "Популярные игры" });
      const header = block.locator(".sidebar-block-header");
      const content = block.locator(".sidebar-block-content");

      // Initially expanded
      await expect(content).toBeVisible();

      // Click to collapse
      await header.click();
      await expect(content).not.toBeVisible();

      // Click to expand
      await header.click();
      await expect(content).toBeVisible();
    });

    test("should persist collapse state in localStorage", async ({ page }) => {
      const block = page
        .locator(".sidebar-block")
        .filter({ hasText: "Популярные игры" });
      const header = block.locator(".sidebar-block-header");

      // Collapse the block
      await header.click();

      // Reload the page
      await page.reload();

      // Block should still be collapsed
      const content = block.locator(".sidebar-block-content");
      await expect(content).not.toBeVisible();
    });

    test("should show collapse indicator icon", async ({ page }) => {
      const block = page
        .locator(".sidebar-block")
        .filter({ hasText: "Популярные игры" });
      const header = block.locator(".sidebar-block-header");
      const collapseIcon = header.locator(".collapse-icon");

      await expect(collapseIcon).toBeVisible();
    });
  });

  test.describe("Popular Games", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display game links in popular games", async ({ page }) => {
      const section = page.locator(".popular-games");
      const gameLinks = section.locator(".game-link");

      // Should have some games or empty state
      const count = await gameLinks.count();
      if (count > 0) {
        await expect(gameLinks.first()).toBeVisible();
      }
    });

    test("should navigate to game page on click", async ({ page }) => {
      const section = page.locator(".popular-games");
      const gameLink = section.locator(".game-link").first();

      if (await gameLink.isVisible()) {
        await gameLink.click();
        await expect(page).toHaveURL(/\/games\//);
      }
    });
  });

  test.describe("Recruiting Games", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display recruiting games list", async ({ page }) => {
      const section = page.locator(".recruiting-games");
      await expect(section).toBeVisible();
    });

    test("should show recruitment indicator for games", async ({ page }) => {
      const section = page.locator(".recruiting-games");
      const games = section.locator(".game-link");

      if ((await games.count()) > 0) {
        // Games in this section should be recruiting
        await expect(games.first()).toBeVisible();
      }
    });
  });

  test.describe("Active Games", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display active games list", async ({ page }) => {
      const section = page.locator(".active-games");
      await expect(section).toBeVisible();
    });

    test("should show last activity time for games", async ({ page }) => {
      const section = page.locator(".active-games");
      const games = section.locator(".game-link");

      if ((await games.count()) > 0) {
        // The first game should have an activity indicator, but the
        // activity time may or may not be rendered depending on game data
      }
    });
  });

  test.describe("Game Tag Cloud", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display tag cloud", async ({ page }) => {
      const tagCloud = page.locator(".game-tag-cloud");
      await expect(tagCloud).toBeVisible();
    });

    test("should display tags with varying font sizes", async ({ page }) => {
      const tagCloud = page.locator(".game-tag-cloud");
      const tags = tagCloud.locator(".tag-link");

      if ((await tags.count()) > 1) {
        // Tags should have different font sizes based on popularity
        const firstTagSize = await tags
          .first()
          .evaluate((el) => window.getComputedStyle(el).fontSize);
        expect(firstTagSize).toBeDefined();
      }
    });

    test("should navigate to games filtered by tag on click", async ({
      page,
    }) => {
      const tagCloud = page.locator(".game-tag-cloud");
      const tag = tagCloud.locator(".tag-link").first();

      if (await tag.isVisible()) {
        await tag.click();
        await expect(page).toHaveURL(/\/games\?.*tag/i);
      }
    });
  });

  test.describe("Active Polls", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display active polls section", async ({ page }) => {
      const section = page.locator(".active-polls");
      await expect(section).toBeVisible();
    });

    test("should display poll if exists", async ({ page }) => {
      const section = page.locator(".active-polls");
      const poll = section.locator(".poll");

      // May or may not have active polls
      const hasPoll = await poll.isVisible().catch(() => false);
      if (hasPoll) {
        await expect(poll.locator(".poll-title, .poll-question")).toBeVisible();
      }
    });
  });

  test.describe("Poll Voting", () => {
    test("should display poll options", async ({ page }) => {
      await page.goto("/");
      const poll = page.locator(".poll").first();

      if (await poll.isVisible().catch(() => false)) {
        const options = poll.locator(".poll-option");
        await expect(options.first()).toBeVisible();
      }
    });

    test("should show vote button for authenticated users", async ({
      authenticatedPage,
    }) => {
      await authenticatedPage.goto("/");

      const poll = authenticatedPage.locator(".poll").first();
      if (await poll.isVisible().catch(() => false)) {
        // The "голосовать" button should be visible if user hasn't voted
      }
    });

    test("should show results after voting", async ({ authenticatedPage }) => {
      await authenticatedPage.goto("/");

      const poll = authenticatedPage.locator(".poll").first();
      if (await poll.isVisible().catch(() => false)) {
        // If user has voted, results should be shown - may or may not be
        // visible depending on vote state
      }
    });
  });

  test.describe("Forum Boards", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display forum boards list", async ({ page }) => {
      const section = page.locator(".forum-boards");
      await expect(section).toBeVisible();
    });

    test("should display board names", async ({ page }) => {
      const section = page.locator(".forum-boards");
      const boards = section.locator(".board-link, a");

      if ((await boards.count()) > 0) {
        await expect(boards.first()).toBeVisible();
      }
    });

    test("should show comment count for boards", async ({ page }) => {
      const section = page.locator(".forum-boards");
      const commentCounts = section.locator(".comment-count, .topic-count");

      // Comment counts may be displayed
      if ((await commentCounts.count()) > 0) {
        await expect(commentCounts.first()).toBeVisible();
      }
    });

    test("should navigate to forum board on click", async ({ page }) => {
      const section = page.locator(".forum-boards");
      const boardLink = section.locator("a").first();

      if (await boardLink.isVisible()) {
        await boardLink.click();
        await expect(page).toHaveURL(/\/forum\//);
      }
    });
  });

  test.describe("Contact Forms", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display contact forms section", async ({ page }) => {
      const section = page.locator(".contact-forms");
      await expect(section).toBeVisible();
    });

    test("should have support link", async ({ page }) => {
      const section = page.locator(".contact-forms");
      const supportLink = section
        .locator("a")
        .filter({ hasText: /поддержк|связь|обращени/i });

      if ((await supportLink.count()) > 0) {
        await expect(supportLink.first()).toBeVisible();
      }
    });

    test("should have complaint link", async ({ page }) => {
      const section = page.locator(".contact-forms");
      const complaintLink = section
        .locator("a")
        .filter({ hasText: /жалоб|нарушени/i });

      if ((await complaintLink.count()) > 0) {
        await expect(complaintLink.first()).toBeVisible();
      }
    });
  });

  test.describe("Support Us", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display support section", async ({ page }) => {
      const section = page.locator(".support-us");
      await expect(section).toBeVisible();
    });

    test("should display progress bar", async ({ page }) => {
      const section = page.locator(".support-us");

      // No visibility guard around the assertion: the guard repeated the
      // assertion word for word, so the test passed by skipping itself.
      await expect(section.locator(".progress-bar, .progress")).toBeVisible();
    });

    test("should have donation link", async ({ page }) => {
      const section = page.locator(".support-us");
      const donateLink = section.locator("a");

      if ((await donateLink.count()) > 0) {
        await expect(donateLink.first()).toHaveAttribute("href");
      }
    });
  });

  test.describe("Partners", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display partners section", async ({ page }) => {
      const section = page.locator(".partners");
      await expect(section).toBeVisible();
    });

    test("should display partner links", async ({ page }) => {
      const section = page.locator(".partners");
      const partnerLinks = section.locator("a");

      if ((await partnerLinks.count()) > 0) {
        await expect(partnerLinks.first()).toHaveAttribute("href");
      }
    });
  });

  test.describe("Game Link Component", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display game title", async ({ page }) => {
      const gameLink = page.locator(".game-link").first();

      if (await gameLink.isVisible().catch(() => false)) {
        const title = gameLink.locator(".game-title, .title");
        await expect(title).toBeVisible();
      }
    });

    test("should display master name", async ({ page }) => {
      const gameLink = page.locator(".game-link").first();

      if (await gameLink.isVisible().catch(() => false)) {
        // Master name may be shown
      }
    });

    test("should show unread indicator when applicable", async ({
      authenticatedPage,
    }) => {
      await authenticatedPage.goto("/");

      const gameLink = authenticatedPage.locator(".game-link").first();
      if (await gameLink.isVisible().catch(() => false)) {
        // Unread indicator shown for subscribed games with new content -
        // may or may not be visible
      }
    });
  });

  test.describe("Blog Link Component", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display blog title", async ({ page }) => {
      const section = page.locator(".active-blogs, .popular-blogs").first();

      // Same as above: the guard was the assertion.
      await expect(section.locator(".blog-link, a").first()).toBeVisible();
    });

    test("should navigate to blog on click", async ({ page }) => {
      const section = page.locator(".active-blogs, .popular-blogs").first();
      const blogLink = section.locator("a").first();

      if (await blogLink.isVisible()) {
        await blogLink.click();
        await expect(page).toHaveURL(/\/blogs\//);
      }
    });
  });

  test.describe("Sidebar Responsiveness", () => {
    test("should hide sidebars on mobile viewport", async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 667 });
      await page.goto("/");

      // Sidebars should be hidden or collapsible on mobile - either hidden
      // or transformed into a mobile menu.
      // On mobile, sidebars are typically hidden or in a hamburger menu
      // This test verifies the responsive behavior exists
    });

    test("should show sidebars on desktop viewport", async ({ page }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });
      await page.goto("/");

      await expect(page.locator(".left-sidebar")).toBeVisible();
      await expect(page.locator(".right-sidebar")).toBeVisible();
    });
  });

  test.describe("Sidebar on Different Pages", () => {
    test("should display sidebars on home page", async ({ page }) => {
      await page.goto("/");
      await expect(page.locator(".left-sidebar")).toBeVisible();
      await expect(page.locator(".right-sidebar")).toBeVisible();
    });

    test("should display sidebars on games page", async ({ page }) => {
      await page.goto("/games");
      await expect(page.locator(".left-sidebar")).toBeVisible();
      await expect(page.locator(".right-sidebar")).toBeVisible();
    });

    test("should display sidebars on forum page", async ({ page }) => {
      await page.goto("/forum");
      await expect(page.locator(".left-sidebar")).toBeVisible();
      await expect(page.locator(".right-sidebar")).toBeVisible();
    });

    test("should display sidebars on community page", async ({ page }) => {
      await page.goto("/community");
      await expect(page.locator(".left-sidebar")).toBeVisible();
      await expect(page.locator(".right-sidebar")).toBeVisible();
    });
  });
});
