import type { Locator, Page } from "@playwright/test";

/**
 * How the specs of the sidebar address a block of it: by the token
 * SidebarBlock identifies it with (`ul#sidebar-list-<token>`).
 */

/** The list a sidebar block renders its rows into. */
export const block = (page: Page, token: string): Locator =>
  page.locator(`#sidebar-list-${token}`);

/**
 * The clip the fold collapses, which is what "collapsed" means on screen: the
 * fold (Reset.sass, `.expand-fold`) hides the clip's overflow, and the list
 * keeps its own height the whole time.
 */
export const blockFold = (page: Page, token: string): Locator =>
  page.locator(`.expand-fold-clip:has(#sidebar-list-${token})`);

/** The fold sign in the block heading; it carries the expanded state. */
export const blockToggle = (page: Page, token: string): Locator =>
  page.locator(`#sidebar-toggle-${token}`);
