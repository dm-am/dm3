import type { Page } from "@playwright/test";
import { test, expect, API_BASE_URL } from "../../fixtures/auth";
import { block } from "../../fixtures/sidebar";

/**
 * The right column of the sidebar and its tail: the popular games and blogs,
 * the tag cloud, the polls, and the contact, support and partner blocks.
 */
test.describe("Sidebars", () => {
  test.describe("Right Sidebar", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    for (const [name, token] of [
      ["active polls", "ActivePolls"],
      ["popular games", "PopularGames"],
      ["popular blogs", "PopularBlogs"],
      ["tag cloud", "GameTags"],
      ["contact forms", "ContactForms"],
      ["support", "SupportUs"],
      ["partners", "Partners"],
    ]) {
      test(`should display ${name} section`, async ({ page }) => {
        await expect(block(page, token)).toBeVisible();
      });
    }
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
    /**
     * The fundraising the panel reads, answered in the shape of GET
     * /v1/fundraising, so a goal the stand does not hold shows without a
     * write to it.
     */
    const answerFundraising = (
      page: Page,
      goal: { title: string; goalAmount: number; collectedAmount: number },
    ) =>
      page.route(`${API_BASE_URL}/v1/fundraising`, (route) =>
        route.fulfill({
          json: {
            resource: { ...goal, modifiedUtc: "2020-01-01T00:00:00+00:00" },
          },
        }),
      );

    // The bar is always there: the stand's seed announces an example collection.
    test("should display the bar of the collection", async ({ page }) => {
      await page.goto("/");

      const support = block(page, "SupportUs");
      await expect(support.locator(".progress")).toBeVisible();
      await expect(support.locator(".goal-title")).not.toBeEmpty();
    });

    test("should keep the bar while nothing is announced", async ({ page }) => {
      await answerFundraising(page, {
        title: "",
        goalAmount: 0,
        collectedAmount: 0,
      });
      await page.goto("/");

      const support = block(page, "SupportUs");
      await expect(support.locator(".progress")).toBeVisible();
      await expect(support.locator(".progress")).toHaveText(/0\s\/\s0\sр\./);
    });

    test("should have donation link", async ({ page }) => {
      await page.goto("/");

      await expect(
        block(page, "SupportUs").getByRole("link", { name: "Помогите нам" }),
      ).toHaveAttribute("href", "/donate");
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

  test.describe("Blog Link Component", () => {
    test.beforeEach(async ({ page }) => {
      await page.goto("/");
    });

    test("should display blog title", async ({ page }) => {
      const rows = block(page, "PopularBlogs").locator("li.link a");
      await expect(rows.first()).toBeVisible();
    });

    test("should navigate to blog on click", async ({ page }) => {
      const blogLink = block(page, "PopularBlogs").locator("li.link a").first();

      await blogLink.click();
      await expect(page).toHaveURL(/\/blogs\//);
    });
  });
});
