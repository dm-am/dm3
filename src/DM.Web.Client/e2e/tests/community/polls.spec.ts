import {
  test,
  expect,
  API_BASE_URL,
  authenticatedContext,
} from "../../fixtures/auth";

/**
 * A poll is a `.poll` card (`.poll-card` on /polls, straight inside
 * `#sidebar-list-ActivePolls` in the sidebar) whose rows are `.poll-option-row`.
 */
test.describe("Polls", () => {
  // In order: while it lives, the poll the form creates is one the sidebar
  // offers to vote in, and its cleanup would take it from under the vote test.
  test.describe.configure({ mode: "default" });

  test("should display polls page", async ({ page }) => {
    await page.goto("/polls");
    // exact, because the sidebar block "Активные опросы" is a heading too and
    // a substring match resolves to both at once.
    await expect(
      page.getByRole("heading", { name: "Опросы", exact: true }),
    ).toBeVisible();
    await expect(page.locator(".polls-list")).toBeVisible({ timeout: 10000 });
    await expect(page.locator(".poll-card").first()).toBeVisible();
  });

  // A vote is a write against the shared seed, and the card carries its own
  // undo ("Отменить голос"), so the test casts the vote, checks it landed,
  // retracts it and checks the retraction.
  test("votes in a poll and takes the vote back", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/");

    // A card that offers the vote: the block lists closed and pending polls
    // too, and a voted poll offers the retraction instead. Addressed by the
    // accessible name ("Проголосовать за: ..."), because .poll-option-vote is
    // on both buttons.
    const voteControl = authenticatedPage.getByRole("button", {
      name: /^Проголосовать за:/,
    });
    const offering = authenticatedPage
      .locator("#sidebar-list-ActivePolls .poll")
      .filter({ has: voteControl })
      .first();
    await expect(offering).toBeVisible({ timeout: 10000 });

    // The card is held by its title from here on: once the vote lands it no
    // longer offers one, and the locator above would move to the next poll.
    const title = (await offering.locator(".poll-title").innerText()).trim();
    const poll = authenticatedPage
      .locator("#sidebar-list-ActivePolls .poll")
      .filter({
        has: authenticatedPage.locator(".poll-title", { hasText: title }),
      });

    try {
      await poll
        .getByRole("button", { name: /^Проголосовать за:/ })
        .first()
        .click();
      await expect(poll.locator(".poll-option-voted")).toBeVisible({
        timeout: 5000,
      });

      await poll.getByRole("button", { name: "Отменить голос" }).click();
      await expect(poll.locator(".poll-option-voted")).toHaveCount(0, {
        timeout: 5000,
      });
    } finally {
      // A vote left behind by a failure above is taken back through the API,
      // so the next run finds the poll open for the primary account again.
      const cleanup = await authenticatedContext();
      const active = await cleanup.get(
        `${API_BASE_URL}/v1/polls?status=Active&take=50`,
      );
      const polls: {
        id: string;
        title: string;
        options: { voted?: boolean }[];
      }[] = (await active.json()).resources ?? [];
      const cast = polls.find(
        (one) => one.title === title && one.options.some((o) => o.voted),
      );
      if (cast)
        await cleanup.delete(`${API_BASE_URL}/v1/polls/${cast.id}/vote`);
      await cleanup.dispose();
    }
  });

  // Creating takes a senior moderator or above; the primary account is the
  // seed's administrator. The start is left as the form offers it, now.
  test("creates a poll through the form, and the list shows it", async ({
    authenticatedPage,
  }, testInfo) => {
    const title = `Опрос из формы ${testInfo.project.name} ${Date.now()}`;
    await authenticatedPage.goto("/polls");
    // The list is loaded before the form sends: the new poll is put on top of it.
    await expect(
      authenticatedPage.locator(".polls-list .poll-title").first(),
    ).toBeVisible({ timeout: 10000 });

    await authenticatedPage
      .getByRole("button", { name: "+ Создать опрос" })
      .click();
    await authenticatedPage.locator("#poll-title").fill(title);
    await authenticatedPage
      .getByRole("textbox", { name: "Вариант 1", exact: true })
      .fill("Да");
    await authenticatedPage
      .getByRole("textbox", { name: "Вариант 2", exact: true })
      .fill("Нет");

    try {
      await authenticatedPage
        .getByRole("button", { name: "Создать опрос", exact: true })
        .click();
      await expect(
        authenticatedPage.locator(".polls-list .poll-title", {
          hasText: title,
        }),
      ).toBeVisible({ timeout: 5000 });
    } finally {
      // A poll is a write against the shared seed: the one made here is
      // removed through the API, also after a failure above.
      const cleanup = await authenticatedContext();
      const found = await cleanup.get(
        `${API_BASE_URL}/v1/polls?search=${encodeURIComponent(title)}`,
      );
      const made: { id: string; title: string }[] =
        (await found.json()).resources ?? [];
      for (const poll of made.filter((one) => one.title === title))
        await cleanup.delete(`${API_BASE_URL}/v1/polls/${poll.id}`);
      await cleanup.dispose();
    }
  });
});
