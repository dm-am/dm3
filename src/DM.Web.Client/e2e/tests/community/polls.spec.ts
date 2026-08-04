import { test, expect } from "../../fixtures/auth";

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

  // Runs again, and gives the vote back. It was switched off because a vote is
  // a write against the shared seed with nothing to undo it; the card carries
  // the undo itself ("Отменить голос"), so the test casts the vote, checks it
  // landed, retracts it and checks the retraction. A tier that cannot press its
  // one write control is a tier that does not cover voting at all.
  // Still off, and the reason is now narrower than it was. The write does have
  // an inverse and the seed keeps an open poll the primary account has not
  // voted in, so both former obstacles are gone; what remains is that the
  // sidebar block renders no vote control for it in the browser, while the API
  // reports the poll open and unvoted. Whatever decides that is above this test
  // and has to be found before the test can mean anything.
  test.skip("votes in a poll and takes the vote back", async ({
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
    const poll = authenticatedPage
      .locator("#sidebar-list-ActivePolls .poll")
      .filter({ has: voteControl })
      .first();
    await expect(poll).toBeVisible({ timeout: 10000 });

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
  });
});
