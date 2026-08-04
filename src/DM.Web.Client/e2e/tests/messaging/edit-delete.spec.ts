import { test, expect } from "../../fixtures/auth";

/**
 * ".message-item", ".message-editor" and the data-testids this file clicked
 * are in no template. A message is `.pm-message`; the edit and delete
 * controls live in the hover toolbar `.msg-toolbar-fixed` as `.toolbar-btn`
 * buttons labelled "Редактировать" and "Удалить". Two nested `if`s meant
 * none of it ever ran.
 */
test.describe("Message Edit/Delete", () => {
  // This one runs: opening the editor is not a write, so nothing here has to be
  // put back afterwards, and a file whose only test is skipped runs nothing at
  // all. The save is what the shared seed cannot take, and that is the test
  // below.
  test("should open the editor on a message of one's own", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/messenger");

    const firstChat = authenticatedPage.locator(".chat-preview").first();
    await expect(firstChat).toBeVisible({ timeout: 10000 });
    await firstChat.click();

    // The toolbar is rendered once, next to whichever message is hovered, and
    // it only carries the edit control for a message the viewer may edit — so
    // the button being there is the whole precondition.
    const messages = authenticatedPage.locator(".pm-message");
    await expect(messages.first()).toBeVisible({ timeout: 10000 });

    const toolbar = authenticatedPage.locator(".msg-toolbar-fixed");
    const edit = toolbar.getByRole("button", { name: "Редактировать" });

    const count = await messages.count();
    for (let index = 0; index < count; index++) {
      await messages.nth(index).hover();
      if (await edit.isVisible()) break;
    }

    await expect(edit).toBeVisible();
    await edit.click();

    await expect(authenticatedPage.locator(".msg-edit")).toBeVisible();
  });

  test.skip("should save an edited message", async ({ authenticatedPage }) => {
    // Switched off, and no longer for want of a selector: saving rewrites a
    // seeded message and nothing in the corpus puts it back.
    await authenticatedPage.goto("/messenger");

    const editor = authenticatedPage.locator(".msg-edit");
    await expect(editor).toBeVisible();
    await editor.getByRole("button", { name: "Сохранить" }).click();

    await expect(editor).toBeHidden();
  });
});
