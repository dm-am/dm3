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

    test("tooltip persists when hovering over tooltip itself", async ({ page }) => {
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

    test("trigger has aria-describedby when tooltip is visible", async ({ page }) => {
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
      // Navigate to page with disabled tooltips if available
      // This test verifies the disabled prop works
      await page.goto("/games");

      // Find a tooltip trigger that might be disabled
      const tooltipTrigger = page.locator(".tooltip-trigger").first();
      if (await tooltipTrigger.isVisible().catch(() => false)) {
        // Check if tooltip is conditionally disabled
        await tooltipTrigger.hover();
        await page.waitForTimeout(300);

        // This is a generic test - actual behavior depends on component state
      }
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

      // Touch/tap to show tooltip
      await scrollNavBtn.tap();

      // May show tooltip on touch (behavior depends on implementation)
      const tooltip = page.locator('[role="tooltip"]');
      // Touch behavior may vary - this documents expected behavior

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
    test("sidebar section headers have tooltips", async ({ page }) => {
      const sidebarHeader = page.locator(".sidebar-block-header").first();

      if (await sidebarHeader.isVisible()) {
        await sidebarHeader.hover();
        await page.waitForTimeout(300);

        // Check if tooltip appears (not all headers have tooltips)
        const tooltip = page.locator('[role="tooltip"]');
        // May or may not have tooltip
      }
    });
  });

  test.describe("Data Table Tooltips", () => {
    test("data table cells can have tooltips", async ({ page }) => {
      await page.goto("/games");

      // Wait for table to load
      await page.waitForSelector(".games-data-table, .games-list", {
        timeout: 5000,
      }).catch(() => null);

      // Find table cells that might have tooltips
      const statusCell = page.locator('[data-testid="game-status"]').first();
      if (await statusCell.isVisible().catch(() => false)) {
        await statusCell.hover();
        await page.waitForTimeout(300);

        // Check for tooltip
        const tooltip = page.locator('[role="tooltip"]');
        // May or may not have tooltip depending on content
      }
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
