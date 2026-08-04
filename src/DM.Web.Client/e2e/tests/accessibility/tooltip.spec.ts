import { test, expect } from "@playwright/test";

test.describe("Tooltip Accessibility", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/");
  });

  test.describe("WCAG 1.4.13 - Content on Hover or Focus", () => {
    test("tooltip appears on mouse hover", async ({ page }) => {
      // Find a button with tooltip (scroll nav buttons have tooltips)
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      // Hover over button
      await scrollNavBtn.hover();

      // Wait for tooltip to appear (with delay)
      await page.waitForTimeout(300);

      // Tooltip should be visible
      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();
    });

    test("tooltip appears on keyboard focus", async ({ page }) => {
      // Find a focusable element with tooltip
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      // Focus via keyboard (Tab)
      await scrollNavBtn.focus();

      // Tooltip should appear immediately on focus (no delay)
      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();
    });

    test("tooltip is dismissible via ESC key", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      // Focus to show tooltip
      await scrollNavBtn.focus();

      // Tooltip should be visible
      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();

      // Press ESC
      await page.keyboard.press("Escape");

      // Tooltip should be hidden
      await expect(tooltip).not.toBeVisible();
    });

    test("tooltip persists when hovering over tooltip itself", async ({
      page,
    }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      // Hover to show tooltip
      await scrollNavBtn.hover();
      await page.waitForTimeout(300);

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();

      // Move to tooltip
      await tooltip.hover();

      // Tooltip should still be visible
      await expect(tooltip).toBeVisible();
    });
  });

  test.describe("ARIA Attributes", () => {
    test("tooltip has role=tooltip", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();
      await scrollNavBtn.hover();
      await page.waitForTimeout(300);

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toHaveAttribute("role", "tooltip");
    });

    test("trigger has aria-describedby when tooltip is visible", async ({
      page,
    }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();
      const trigger = scrollNavBtn.locator("xpath=..");

      // Before hover - no aria-describedby
      await expect(trigger).not.toHaveAttribute("aria-describedby");

      // Hover to show tooltip
      await scrollNavBtn.hover();
      await page.waitForTimeout(300);

      // After hover - has aria-describedby matching tooltip id
      const tooltip = page.locator('[role="tooltip"]');
      const tooltipId = await tooltip.getAttribute("id");
      await expect(trigger).toHaveAttribute("aria-describedby", tooltipId!);
    });

    test("tooltip has unique id", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();
      await scrollNavBtn.hover();
      await page.waitForTimeout(300);

      const tooltip = page.locator('[role="tooltip"]');
      const id = await tooltip.getAttribute("id");

      expect(id).toBeTruthy();
      expect(id).toMatch(/^tooltip-/);
    });
  });

  test.describe("Tooltip Content", () => {
    test("tooltip displays text content", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();
      await scrollNavBtn.hover();
      await page.waitForTimeout(300);

      const tooltip = page.locator('[role="tooltip"]');
      const text = await tooltip.textContent();

      expect(text?.trim()).toBeTruthy();
    });

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
      await page.waitForTimeout(300);

      const tooltip = page.locator('[role="tooltip"]');
      await expect(tooltip).toBeVisible();

      // Get bounding box
      const box = await tooltip.boundingBox();
      expect(box).toBeTruthy();

      // Should be within viewport
      const viewportSize = page.viewportSize();
      if (box && viewportSize) {
        expect(box.x).toBeGreaterThanOrEqual(0);
        expect(box.y).toBeGreaterThanOrEqual(0);
        expect(box.x + box.width).toBeLessThanOrEqual(viewportSize.width);
        expect(box.y + box.height).toBeLessThanOrEqual(viewportSize.height);
      }
    });

    test("tooltip has correct placement class", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();
      await scrollNavBtn.hover();
      await page.waitForTimeout(300);

      const tooltip = page.locator('[role="tooltip"]');

      // Should have one of the placement classes
      const classAttr = await tooltip.getAttribute("class");
      expect(classAttr).toMatch(/tooltip--(top|bottom|left|right)/);
    });
  });

  test.describe("Touch Device Support", () => {
    test("tooltip works on touch devices", async ({ browser }) => {
      // Create context with touch support
      const context = await browser.newContext({
        hasTouch: true,
        viewport: { width: 375, height: 667 },
      });
      const page = await context.newPage();
      await page.goto("/");

      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      // Touch is the third way in (WCAG 1.4.13) and it shows without the hover
      // delay. The body used to end on the tap and assert nothing.
      await scrollNavBtn.tap();
      await expect(page.locator('[role="tooltip"]')).toBeVisible();

      await context.close();
    });
  });

  test.describe("Multiple Tooltips", () => {
    test("only one tooltip is visible at a time", async ({ page }) => {
      const buttons = page.locator(".scroll-nav-btn");
      const count = await buttons.count();

      if (count >= 2) {
        // Hover first button
        await buttons.first().hover();
        await page.waitForTimeout(300);

        // Should have one tooltip
        let tooltips = page.locator('[role="tooltip"]');
        expect(await tooltips.count()).toBe(1);

        // Move to second button
        await buttons.nth(1).hover();
        await page.waitForTimeout(300);

        // Should still have only one tooltip visible
        tooltips = page.locator('[role="tooltip"]');
        const visibleCount = await tooltips.count();
        expect(visibleCount).toBeLessThanOrEqual(1);
      }
    });
  });

  test.describe("Sidebar Tooltips", () => {
    test("sidebar game entries carry a tooltip", async ({ page }) => {
      // Section headers do not have one: the block title is a heading with a
      // collapse toggle. The entries do, and that is what the old body hovered
      // without asserting.
      const entry = page
        .locator("#sidebar-list-ActiveGames .tooltip-trigger")
        .first();
      await expect(entry).toBeVisible();
      await entry.hover();
      await expect(page.locator('[role="tooltip"]')).toBeVisible();
    });
  });

  test.describe("Data Table Tooltips", () => {
    test("data table cells can have tooltips", async ({ page }) => {
      await page.goto("/games");

      // The waitForSelector named a table class that does not exist and its
      // failure was caught, so the guard below never opened.
      const cell = page
        .locator("table.data-table tbody .tooltip-trigger")
        .first();
      await expect(cell).toBeVisible();
      await cell.hover();
      await expect(page.locator('[role="tooltip"]')).toBeVisible();
    });
  });

  test.describe("Transition Animation", () => {
    test("tooltip has fade transition", async ({ page }) => {
      const scrollNavBtn = page.locator(".scroll-nav-btn").first();

      // Hover to trigger tooltip
      await scrollNavBtn.hover();

      // Wait for transition to start
      await page.waitForTimeout(250);

      const tooltip = page.locator('[role="tooltip"]');

      // Check that tooltip is visible (animation completed)
      await expect(tooltip).toBeVisible();
    });
  });
});
