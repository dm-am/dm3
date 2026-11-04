import { expect } from "../../fixtures/auth";
import { test } from "../../fixtures/roles";
import type { Page } from "@playwright/test";
import { settle } from "../../fixtures/window";

/**
 * "Последние баны" as DM2's bot wrote its ban log: every issue and every lift
 * an entry of its own in DM2's words, the caption "Выдан:" or "Снят:", and the
 * number of the entry in the corner, a link to it. Nothing in an entry is red
 * or green: an entry is the record of a decision. The colours are compared as
 * the browser computed them, so the check holds in either theme.
 */

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

test.describe("The ban list as DM2's ban log", () => {
  test("writes every entry in DM2's words, neither red nor green, at 1366", async ({
    openAs,
  }) => {
    const page = await openAs("admin", 1366, {
      path: "/moderation/bans",
      roles: ["admin"],
      ready: "",
    });
    await expect(
      page.locator("main#main .moderation-bans article").first(),
    ).toBeVisible({ timeout: 15000 });
    await settle(page);

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
  });
});
