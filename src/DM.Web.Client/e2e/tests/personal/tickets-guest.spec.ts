import { test, expect } from "../../fixtures/auth";
import { SEEDED_GUEST_TICKET } from "../../fixtures/seed";
import {
  WIDTHS,
  TOKEN,
  open,
  close,
  caption,
  commentTexts,
  hiddenParts,
  colouredParts,
  write,
  press,
} from "../../fixtures/tickets";

/**
 * The guest's page of a ticket, by its token: the same page as the author's,
 * his words signed "Гость", and the token in the header of a write and never
 * in the address. What the page of a ticket is, tickets.spec.ts says. One
 * test reads the guest ticket the seed writes with a known token, as it
 * stands on the stand.
 */

// A page of the site makes dozens of reads.
test.describe.configure({ timeout: 120_000 });

const TRACK_PAGE = `/support/track#token=${TOKEN}`;
/** The page of the seed's open guest ticket (DataSeeder.GuestTicketToken). */
const SEEDED_PAGE = `/support/track#token=${SEEDED_GUEST_TICKET}`;

test.describe("The guest's page of a ticket", () => {
  for (const width of WIDTHS) {
    test(`signs the guest Гость and hides nothing, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(browser, width, TRACK_PAGE);
      await expect(page.locator("main#main .ticket-thread")).toBeVisible({
        timeout: 20000,
      });

      expect(await caption(page)).toMatch(
        /^Автор: Гость, .+ \| Ответы \(2\) \| Ожидает ответа пользователя$/,
      );
      const guest = page
        .locator("main#main .ticket-conversation .comment")
        .last()
        .locator(".author-info");
      await expect(guest).toHaveText(/^Автор:\s+Гость$/);
      await expect(guest.locator("a")).toHaveCount(0);
      await expect(page.locator("main#main .ticket-answer-form")).toContainText(
        "0 / 10000",
      );
      // The address of the page is an ordinary link in one sentence, and
      // nothing copies it. Read as the reader sees it: the text of the
      // paragraph also holds the line breaks of its template at both ends.
      await expect(page.locator("main#main .lead-text")).toHaveText(
        /^Ответ появится на этой странице: http\S+\/support\/track#token=e2e0e2e0e2e0e2e0e2e0e2e0e2e0e2e0\. Сохраните ссылку, чтобы вернуться к обращению\.$/,
        { useInnerText: true },
      );
      await expect(page.locator("main#main")).not.toContainText(
        /Скопировать|номер обращения/i,
      );
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }

  test("writes by the token in its header, never in the address", async ({
    browser,
  }) => {
    const { page, writes, tokens } = await open(browser, 1366, TRACK_PAGE);
    await expect(page.locator("main#main .ticket-answer-form")).toBeVisible({
      timeout: 20000,
    });

    await write(page, "Почта была на старом адресе, он больше не работает.");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .comment-text").last(),
    ).toHaveText("Почта была на старом адресе, он больше не работает.");
    expect(writes).toEqual(["POST /v1/tickets/track/comments"]);
    expect(tokens).toEqual([TOKEN]);
    expect(await caption(page)).toMatch(/Ожидает ответа модерации$/);
    await close(page);
  });

  // The ticket as the seed writes it, read from the stand: a request for
  // access, open, with one word the guest added.
  test("reads the seeded guest ticket by its known token", async ({
    browser,
  }) => {
    const { page } = await open(browser, 1366, SEEDED_PAGE);
    await expect(page.locator("main#main .ticket-thread")).toBeVisible({
      timeout: 20000,
    });

    await expect(page.locator("main#main .record-block__title")).toHaveText(
      "Восстановление доступа",
    );
    expect(await caption(page)).toMatch(/^Автор: Гость, /);
    expect(await commentTexts(page)).toEqual([
      "Уточню: старая почта больше не работает, логин в аккаунте тот же, что в письме.",
    ]);
    await expect(page.locator("main#main .ticket-answer-form")).toContainText(
      "0 / 10000",
    );
    await close(page);
  });
});
