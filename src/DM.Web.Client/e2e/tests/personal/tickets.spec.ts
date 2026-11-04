import { test, expect, SECONDARY_STORAGE_STATE } from "../../fixtures/auth";
import {
  WIDTHS,
  OPEN,
  CLOSED,
  asked,
  replied,
  openTicket,
  closedTicket,
  open,
  close,
  caption,
  commentTexts,
  hiddenParts,
  colouredParts,
  frameOf,
  write,
  press,
} from "../../fixtures/tickets";

/**
 * A ticket works as a topic with answers and looks like one, and nobody takes
 * it in hand. The page of a ticket is the page of a topic: the block of the
 * ticket in the frame of a topic, its caption "Автор: имя, дата | Ответы (N) |
 * состояние", the answers of the author and of moderation under it as compact
 * comments, read only, and the form at their foot while the ticket is open,
 * its counter at the 10000 the server takes. A closed ticket has no form and
 * says in one plate who closed it. A moderator chooses over the field of his
 * answer the status it moves the ticket to, "Ожидает ответа пользователя"
 * unless he closes it or marks it as spam, and the text and the status go in
 * one request; there is no block "Решение", no warning and no ban. The
 * guest's page by its token is the same page, his words signed "Гость".
 *
 * Nothing of a ticket is hidden: no "Еще", no "…", no line cut short, nothing standing out of the column or scrolled in a
 * box of its own. The editor of the form is the site's own and is not read
 * here. Whether the page is wider than the window is read once for every page
 * of the site, in common/page-width.spec.ts. Nothing is red or green but the
 * letter of a role and [online], as by every name of the site, and the red
 * "*" after the state of a ticket that waits for the reader.
 *
 * The tickets are answered in the shape of the wire (fixtures/tickets.ts):
 * nothing is written to the stand.
 */

// A page of the site makes dozens of reads.
test.describe.configure({ timeout: 120_000 });

test.describe("The page of a ticket, as a topic with answers", () => {
  for (const width of WIDTHS) {
    test(`reads as a topic to its author, nothing hidden and nothing coloured, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(browser, width, `/my-tickets/${OPEN}`, {
        storage: SECONDARY_STORAGE_STATE,
        tickets: [openTicket()],
      });
      await expect(page.locator("main#main .ticket-thread")).toBeVisible({
        timeout: 20000,
      });

      expect(await caption(page)).toMatch(
        /^Автор: TestUser \[online\], .+ \| Ответы \(2\) \| Ожидает ответа модерации$/,
      );
      await expect(
        page.locator("main#main .ticket-conversation .comment.compact"),
      ).toHaveCount(2);
      expect(await commentTexts(page)).toEqual([asked.text, replied.text]);
      await expect(
        page.locator("main#main .ticket-conversation .comment-number"),
      ).toHaveText(["1", "2"]);
      await expect(
        page.locator("main#main .ticket-conversation"),
      ).not.toContainText(/Редактировать|Удалить|Предупреждение/);
      await expect(page.locator("main#main .like-badge")).toHaveCount(0);
      await expect(page.locator("main#main .ticket-answer-form")).toContainText(
        "0 / 10000",
      );
      // The author chooses no status: his answer hands the ticket on.
      await expect(
        page.locator("main#main .ticket-answer-form select"),
      ).toHaveCount(0);
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }

  test("takes the author's answer at the end and hands the turn to moderation", async ({
    browser,
  }) => {
    const { page, writes } = await open(browser, 1366, `/my-tickets/${OPEN}`, {
      storage: SECONDARY_STORAGE_STATE,
      tickets: [{ ...openTicket(), status: "WaitingForUser" }],
    });
    await expect(page.locator("main#main .ticket-answer-form")).toBeVisible({
      timeout: 20000,
    });

    await write(page, "Ссылка на сообщение: /forum/general/5");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .comment-text").last(),
    ).toHaveText("Ссылка на сообщение: /forum/general/5");
    await expect(
      page.locator("main#main .ticket-conversation .comment-number").last(),
    ).toHaveText("3");
    expect(await caption(page)).toMatch(
      /Ответы \(3\) \| Ожидает ответа модерации$/,
    );
    expect(writes).toEqual([`POST /v1/moderation/tickets/${OPEN}/comments`]);
    await close(page);
  });

  test("offers no form on a closed ticket and says who closed it", async ({
    browser,
  }) => {
    const { page } = await open(browser, 1366, `/my-tickets/${CLOSED}`, {
      storage: SECONDARY_STORAGE_STATE,
      tickets: [closedTicket()],
    });
    await expect(page.locator("main#main .ticket-thread")).toBeVisible({
      timeout: 20000,
    });

    await expect(page.locator("main#main .ticket-answer-form")).toHaveCount(0);
    await expect(
      page.locator("main#main .ticket-conversation .refusal-note"),
    ).toHaveText(/^Обращение закрыто .+, закрыл\(а\) TestModerator\.$/);
    expect(await hiddenParts(page)).toEqual([]);
    await close(page);
  });

  // The frame, the ground and the inset of the block are those of a topic.
  test("draws the block in the frame of a topic", async ({ browser }) => {
    const ticket = await open(browser, 1366, `/my-tickets/${OPEN}`, {
      storage: SECONDARY_STORAGE_STATE,
      tickets: [openTicket()],
    });
    await expect(ticket.page.locator("main#main .record-block")).toBeVisible({
      timeout: 20000,
    });
    const ofTicket = await frameOf(
      ticket.page,
      "main#main .ticket-thread .record-block",
    );
    await close(ticket.page);

    const topic = await open(browser, 1366, "/forum/general/5", {
      storage: SECONDARY_STORAGE_STATE,
    });
    await expect(topic.page.locator("main#main .topic").first()).toBeVisible({
      timeout: 20000,
    });
    const ofTopic = await frameOf(topic.page, "main#main .topic");
    await close(topic.page);

    expect(ofTicket).toEqual(ofTopic);
  });
});

test.describe("Мои обращения", () => {
  for (const width of WIDTHS) {
    test(`name the answers and the turn of each, and neither the author nor a colour, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(browser, width, "/my-tickets", {
        storage: SECONDARY_STORAGE_STATE,
        tickets: [openTicket(), closedTicket()],
      });
      await expect(page.locator("main#main .ticket-thread")).toHaveCount(2, {
        timeout: 20000,
      });

      const captions = (
        await page.locator("main#main .record-block__caption").allInnerTexts()
      ).map((text) => text.replace(/\s+/g, " ").trim());
      expect(captions).toEqual([
        expect.stringMatching(
          /^\d{2}\.\d{2}\.\d{4} .+ \| Ответы \(2\) \| Ожидает ответа модерации$/,
        ),
        expect.stringMatching(
          /^\d{2}\.\d{2}\.\d{4} .+ \| Ответы \(1\) \| Закрыто$/,
        ),
      ]);
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }
});
