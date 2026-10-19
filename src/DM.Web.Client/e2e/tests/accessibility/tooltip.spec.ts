import { test, expect } from "@playwright/test";

test.describe("Tooltip Accessibility", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/");
  });

  test.describe("WCAG 1.4.13 - Content on Hover or Focus", () => {
    test("tooltip appears on mouse hover", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      await scrollNavBtn.hover();

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();
    });

    test("tooltip appears on keyboard focus", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      await scrollNavBtn.focus();

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();
    });

    test("tooltip is dismissible via ESC key", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      await scrollNavBtn.focus();

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();

      await page.keyboard.press("Escape");

      await expect(tooltip).not.toBeVisible();
    });

    test("tooltip persists when hovering over tooltip itself", async ({
      page,
    }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      await scrollNavBtn.hover();

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();

      await tooltip.hover();

      await expect(tooltip).toBeVisible();
    });
  });

  test.describe("ARIA Attributes", () => {
    test("trigger has aria-describedby when tooltip is visible", async ({
      page,
    }) => {
      // Named, not taken first: the rail grows an "expand all" button above
      // the others once the page has something collapsible.
      const scrollNavBtn = page.getByRole("button", {
        name: "Прокрутить наверх",
      });
      const trigger = scrollNavBtn.locator("xpath=..");

      await expect(trigger).not.toHaveAttribute("aria-describedby");

      await scrollNavBtn.hover();

      const tooltip = page.locator('[role="tooltip"]', { hasText: "Наверх" });
      await expect(tooltip).toBeVisible();

      const tooltipId = await tooltip.getAttribute("id");
      await expect(trigger).toHaveAttribute("aria-describedby", tooltipId!);
    });
  });

  test.describe("Tooltip Content", () => {
    test("tooltip is not shown for disabled state", async ({ page }) => {
      // The settings button is the one trigger in the tree that disables its
      // own tooltip, and it does so while the panel it opens is on screen.
      const settings = page.locator(".settings-btn");
      const tooltip = page.locator('[role="tooltip"]', {
        hasText: "Настройки сайта",
      });

      await settings.hover();
      await expect(tooltip).toBeVisible();

      await settings.click();
      await expect(page.locator(".settings-bubble")).toBeVisible();
      await expect(tooltip).toHaveCount(0);
    });
  });

  test.describe("Tooltip Positioning", () => {
    test("tooltip is positioned on screen", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();
      await scrollNavBtn.hover();

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();

      const box = await tooltip.boundingBox();
      const viewport = page.viewportSize();
      expect(box).not.toBeNull();
      expect(viewport).not.toBeNull();
      expect(box!.x).toBeGreaterThanOrEqual(0);
      expect(box!.y).toBeGreaterThanOrEqual(0);
      expect(box!.x + box!.width).toBeLessThanOrEqual(viewport!.width);
      expect(box!.y + box!.height).toBeLessThanOrEqual(viewport!.height);
    });
  });

  test.describe("Multiple Tooltips", () => {
    test("only one tooltip is visible at a time", async ({ page }) => {
      const buttons = page.locator(".scroll-nav-btn");
      await expect(buttons.nth(1)).toBeVisible();

      await buttons.first().hover();
      await expect(page.locator('[role="tooltip"]')).toHaveCount(1);

      await buttons.nth(1).hover();
      await expect(page.locator('[role="tooltip"]')).toHaveCount(1);
    });
  });

  test.describe("Sidebar Tooltips", () => {
    test("sidebar game entries carry a tooltip", async ({ page }) => {
      // The block renders before its list arrives, so the entry appears only
      // once the request has answered.
      const entry = page
        .locator("#sidebar-list-ActiveGames .tooltip-trigger")
        .first();
      await expect(entry).toBeVisible({ timeout: 10000 });
      await entry.hover();
      await expect(page.locator('[role="tooltip"]')).toBeVisible();
    });
  });

  test.describe("Data Table Tooltips", () => {
    test("data table cells can have tooltips", async ({ page }) => {
      await page.goto("/games");

      // The table renders empty and fills when the listing answers.
      const cell = page
        .locator("table.data-table tbody .tooltip-trigger:visible")
        .first();
      await expect(cell).toBeVisible({ timeout: 10000 });
      await cell.hover();
      await expect(page.locator('[role="tooltip"]')).toBeVisible();
    });
  });
});
