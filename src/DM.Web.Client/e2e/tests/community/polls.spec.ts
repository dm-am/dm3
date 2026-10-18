import {
  test,
  expect,
  API_BASE_URL,
  authenticatedContext,
} from "../../fixtures/auth";

/**
 * ".polls-list" exists; ".poll-item", ".the-poll", ".sidebar-poll",
 * ".poll-option", ".poll-answer", ".poll-results" and ".voted" do not. A poll
 * is a `.poll` card — `.poll-card` on /polls, straight inside
 * `#sidebar-list-ActivePolls` in the sidebar — whose rows are
 * `.poll-option-row` and whose vote control is `.poll-option-vote`. Both
 * bodies sat behind `isVisible()` on a locator that could never match, so
 * neither ever ran.
 */
test.describe("Polls", () => {
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

  test("should show poll on sidebar", async ({ page }) => {
    await page.goto("/");
    const poll = page.locator("#sidebar-list-ActivePolls .poll").first();

    await expect(poll).toBeVisible({ timeout: 10000 });
    await expect(poll.locator(".poll-option-row").first()).toBeVisible();
  });

  // Runs, and gives the vote back. A vote is a write against the shared seed,
  // and the card carries its own undo ("Отменить голос"), so the test casts the
  // vote, checks it landed, retracts it and checks the retraction. A tier that
  // cannot press its one write control is a tier that does not cover voting.
  //
  // It was off with the note that the sidebar draws no vote control for an
  // open poll the API reports unvoted. On the stand the block does draw it, for
  // both open polls of the seed; what failed was the test's handle on the card,
  // see below.
  test("votes in a poll and takes the vote back", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/");

    // A card that offers the vote, not simply the first one: the block lists
    // closed and pending polls too, and a poll the viewer has already voted in
    // offers the retraction instead. Addressed by the accessible name and not
    // by the class, because .poll-option-vote is on both buttons - filtering
    // by it picked the already-voted poll and the first click cancelled a vote
    // rather than casting one.
    // The name carries the option after it - "Проголосовать за: ..." - so the
    // match is by prefix.
    const voteControl = authenticatedPage.getByRole("button", {
      name: /^Проголосовать за:/,
    });
    const offering = authenticatedPage
      .locator("#sidebar-list-ActivePolls .poll")
      .filter({ has: voteControl })
      .first();
    await expect(offering).toBeVisible({ timeout: 10000 });

    // The card is held by its title from here on. A locator is read again on
    // every use, and "the first card that offers the vote" stops being this
    // card the moment the vote lands: it no longer offers one, so the same
    // locator moved to the next open poll and looked there for the vote just
    // cast.
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
});
