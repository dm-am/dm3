import { test, expect, PRIMARY_STORAGE_STATE } from "../../fixtures/auth";
import { phonePage } from "../../fixtures/phone";
import type { Browser, Page } from "@playwright/test";

/**
 * "Последние баны" written as DM2's bot wrote its ban log: every issue and
 * every lift an entry of its own, "<модератор> выдал(а) <вид> бан пользователю
 * <логин> до <дата>.", a blank line, "Примечание от модератора:" and the
 * reason under it. Under the text the caption says what its moment is the
 * moment of, "Выдан:" or "Снят:", and in its corner stands the number of the
 * entry in the whole log, a link to the entry, as DM2 numbered the comments
 * of its log. Nothing in an entry is red or green. Red is for points and
 * punishment and green for what is good, and DM2 coloured nothing in its log
 * but the login: an entry is the record of a decision.
 *
 * The colours are read as the browser computed them and compared with the two
 * accents as the theme of the page resolves them, so the check holds in
 * either theme and for a colour set by any rule, not only by a class. The
 * list is the administrator's, who sees the lift where the server offers it.
 * Nothing is written.
 */

/** A phone, as a phone lays the page out (fixtures/phone.ts), and the full version. */
const SCREENS = ["phone", 1366] as const;

/** The end of the sentence: the end date, or none for a permanent ban. */
const END = String.raw`.+(\.| до \d{2}\.\d{2}\.\d{4}\.)`;

/** The note, the moment of the caption, an action, the number in the corner. */
const tail = (moment: string, action: string) =>
  String.raw` Примечание от модератора:( .+)? ${moment}: \d{2}\.\d{2}\.\d{4} в \d{2}:\d{2}${action} \d+$`;

/** An entry of an issue: the lift is offered in its caption where it may be. */
const ISSUE = new RegExp(
  String.raw`^\S.* выдал\(а\) (бессрочный )?(демократический|полный) бан пользователю ${END}` +
    tail("Выдан", String.raw`( \| Снять бан)?`),
);

/** An entry of a lift. */
const LIFT = new RegExp(
  String.raw`^\S.* снял\(а\) (бессрочный )?(демократический|полный) бан с пользователя ${END}` +
    tail("Снят", ""),
);

type Entry = {
  text: string;
  /** The number in the corner, and where it leads. */
  number: string;
  to: string | null;
  /** Elements of the entry whose text, ground or frame is an accent. */
  coloured: string[];
};

async function readEntries(page: Page): Promise<Entry[]> {
  return page.evaluate(() => {
    const resolved = (name: string) => {
      const probe = document.createElement("span");
      probe.style.color = `var(${name})`;
      document.body.append(probe);
      const colour = getComputedStyle(probe).color;
      probe.remove();
      return colour;
    };
    const accents = new Set([
      resolved("--accent-red"),
      resolved("--accent-green"),
    ]);
    const entries = document.querySelectorAll<HTMLElement>(
      "main#main .moderation-bans article",
    );
    return [...entries].map((entry) => {
      const number = entry.querySelector(".ban-log-card__number");
      return {
        text: entry.innerText.replace(/\s+/g, " ").trim(),
        number: (number?.textContent ?? "").trim(),
        to: number?.getAttribute("href") ?? null,
        coloured: [entry, ...entry.querySelectorAll<HTMLElement>("*")]
          .filter((element) => {
            const style = getComputedStyle(element);
            return [
              style.color,
              style.backgroundColor,
              style.borderTopColor,
              style.textDecorationColor,
            ].some((colour) => accents.has(colour));
          })
          .map((element) => (element.textContent ?? "").trim()),
      };
    });
  });
}

/** The administrator's page on a phone or in a window of a width. */
async function openAs(
  browser: Browser,
  screen: (typeof SCREENS)[number],
): Promise<Page> {
  if (screen === "phone")
    return phonePage(browser, { storageState: PRIMARY_STORAGE_STATE });
  const context = await browser.newContext({
    storageState: PRIMARY_STORAGE_STATE,
    viewport: { width: screen, height: 900 },
  });
  return context.newPage();
}

test.describe("The ban list as DM2's ban log", () => {
  for (const screen of SCREENS) {
    const where = screen === "phone" ? "on a phone" : `at ${screen}`;
    test(`writes every entry in DM2's words, neither red nor green, ${where}`, async ({
      browser,
    }) => {
      const page = await openAs(browser, screen);
      await page.goto("/moderation/bans");
      await expect(
        page.locator("main#main .moderation-bans article").first(),
      ).toBeVisible({ timeout: 15000 });
      await page.evaluate(() => document.fonts.ready);

      const entries = await readEntries(page);

      expect(entries.length).toBeGreaterThan(0);
      expect(
        entries
          .filter((entry) => !ISSUE.test(entry.text) && !LIFT.test(entry.text))
          .map((entry) => entry.text),
      ).toEqual([]);
      // The number is the entry's own and leads to the entry, on whatever
      // page of the log it stands.
      expect(
        entries
          .filter(
            (entry) =>
              !/^\d+$/.test(entry.number) ||
              !entry.to?.includes(`entry=${entry.number}`) ||
              !entry.to?.endsWith(`#ban-${entry.number}`),
          )
          .map((entry) => `${entry.number} -> ${entry.to}`),
      ).toEqual([]);
      expect(entries.flatMap((entry) => entry.coloured)).toEqual([]);
      expect(
        entries
          .filter((entry) => /Еще|…/.test(entry.text))
          .map((entry) => entry.text),
      ).toEqual([]);
      await page.context().close();
    });
  }
});
