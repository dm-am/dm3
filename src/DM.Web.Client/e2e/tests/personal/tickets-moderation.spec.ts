import { test, expect, PRIMARY_STORAGE_STATE } from "../../fixtures/auth";
import {
  WIDTHS,
  OPEN,
  ADMIN,
  asked,
  replied,
  openTicket,
  open,
  close,
  commentTexts,
  hiddenParts,
  colouredParts,
  write,
  press,
} from "../../fixtures/tickets";

/**
 * A moderator's page of a ticket: the status over the field of his answer,
 * "Ожидает ответа пользователя" unless he closes the ticket or marks it as
 * spam, the text and the status in one request, and nobody taking the
 * ticket in hand. What the page of a ticket is, tickets.spec.ts says.
 */

// A page of the site makes dozens of reads.
test.describe.configure({ timeout: 120_000 });

test.describe("A moderator's page of a ticket", () => {
  for (const width of WIDTHS) {
    test(`offers the status over the field of the answer, nobody taking it in hand, at ${width}`, async ({
      browser,
    }) => {
      const { page } = await open(
        browser,
        width,
        `/moderation/tickets/${OPEN}`,
        {
          storage: PRIMARY_STORAGE_STATE,
          tickets: [openTicket()],
          canResolve: true,
          writer: ADMIN,
        },
      );
      const status = page.locator("main#main .ticket-answer-form select");
      await expect(status).toBeVisible({ timeout: 20000 });

      await expect(page.locator("main#main")).not.toContainText(
        /Взять в работу|Разбирает|В работе|Решение|Закрыть обращение|Выдать предупреждение|Выдать бан/,
      );
      // One editor on the page, the form's, and the status over it.
      await expect(
        page.locator("main#main .bbcode-editor-wrapper"),
      ).toHaveCount(1);
      await expect(status).toHaveValue("WaitingForUser");
      await expect(status.locator("option")).toHaveText([
        "Ожидает ответа пользователя",
        "Закрыто",
        "Спам",
      ]);
      const order = await page.evaluate(() => {
        const choice = document.querySelector(
          "main#main .ticket-answer-form select",
        );
        const field = document.querySelector(
          "main#main .ticket-answer-form .bbcode-editor-wrapper",
        );
        return choice && field
          ? choice.compareDocumentPosition(field) &
              Node.DOCUMENT_POSITION_FOLLOWING
          : 0;
      });
      expect(order, "the status stands over the field").toBeTruthy();
      // The ticket waits for moderation, and the reader answers as moderation.
      await expect(
        page.locator("main#main .record-block__caption .star"),
      ).toHaveText("*");
      expect(await hiddenParts(page)).toEqual([]);
      expect(await colouredParts(page)).toEqual([]);
      await close(page);
    });
  }

  test("closes with the last word in one request", async ({ browser }) => {
    const { page, writes } = await open(
      browser,
      1366,
      `/moderation/tickets/${OPEN}`,
      {
        storage: PRIMARY_STORAGE_STATE,
        tickets: [openTicket()],
        canResolve: true,
        writer: ADMIN,
      },
    );
    const status = page.locator("main#main .ticket-answer-form select");
    await expect(status).toBeVisible({ timeout: 20000 });

    await status.selectOption("Closed");
    await write(page, "Предупреждение снято, извините за ошибку.");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .refusal-note"),
    ).toHaveText(/^Обращение закрыто .+, закрыл\(а\) SolohinLex\.$/);
    expect(writes).toEqual([`POST /v1/moderation/tickets/${OPEN}/comments`]);
    expect((await commentTexts(page)).at(-1)).toBe(
      "Предупреждение снято, извините за ошибку.",
    );
    await expect(page.locator("main#main .ticket-answer-form")).toHaveCount(0);
    await close(page);
  });

  test("closes as spam without a word", async ({ browser }) => {
    const { page, writes } = await open(
      browser,
      1366,
      `/moderation/tickets/${OPEN}`,
      {
        storage: PRIMARY_STORAGE_STATE,
        tickets: [openTicket()],
        canResolve: true,
        writer: ADMIN,
      },
    );
    const status = page.locator("main#main .ticket-answer-form select");
    await expect(status).toBeVisible({ timeout: 20000 });

    await status.selectOption("Spam");
    await press(page, "Отправить");

    await expect(
      page.locator("main#main .ticket-conversation .refusal-note"),
    ).toHaveText(/^Обращение закрыто как спам .+, закрыл\(а\) SolohinLex\.$/);
    expect(writes).toEqual([`POST /v1/moderation/tickets/${OPEN}/comments`]);
    expect(await commentTexts(page)).toEqual([asked.text, replied.text]);
    await close(page);
  });
});
