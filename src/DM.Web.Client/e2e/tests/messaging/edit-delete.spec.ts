import {
  test,
  expect,
  API_BASE_URL,
  secondaryUser,
  authenticatedContext,
} from "../../fixtures/auth";

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

  // Off, with the isolation problem solved and one thing left. The body no
  // longer rewrites a seeded message: it posts its own, edits that, and deletes
  // it in a finally with a context of its own, so the seed is untouched whether
  // the test passes or fails. What is unfinished is the typing - the editor
  // behind .msg-edit is not a plain textarea, and filling it needs whatever the
  // BBCode editor exposes rather than locator("textarea"). Left switched off
  // rather than left red, and the reason is now the last real step and not the
  // shared database.
  test.skip("saves an edit to a message of its own", async ({
    authenticatedPage,
    authContext,
  }) => {
    const marker = `e2e-edit-${Date.now()}`;

    const chatResponse = await authContext.post(
      `${API_BASE_URL}/v1/chats/direct/${secondaryUser.username}`,
    );
    expect(chatResponse.ok(), "the direct chat must open").toBeTruthy();
    const chat = await chatResponse.json();
    const chatId = chat.resource?.id ?? chat.id;

    const created = await authContext.post(
      `${API_BASE_URL}/v1/chats/${chatId}/messages`,
      {
        headers: { "Content-Type": "application/json" },
        data: { text: marker },
      },
    );
    expect(created.status(), "the message must be created").toBe(201);
    const messageId = (await created.json()).resource.id;

    try {
      // Straight to the conversation this test wrote in. Opening the first
      // preview in the list opens whichever chat sorted to the top, which is
      // not the one holding the message below.
      await authenticatedPage.goto(`/messenger/user/${secondaryUser.username}`);

      const own = authenticatedPage.locator(".pm-message", { hasText: marker });
      await expect(own).toBeVisible({ timeout: 10000 });
      await own.hover();

      const toolbar = authenticatedPage.locator(".msg-toolbar-fixed");
      await toolbar.getByRole("button", { name: "Редактировать" }).click();

      const editor = authenticatedPage.locator(".msg-edit");
      await expect(editor).toBeVisible();
      await editor.locator("textarea").fill(`${marker} правка`);
      await editor.getByRole("button", { name: "Сохранить" }).click();

      await expect(editor).toBeHidden();
      await expect(
        authenticatedPage.locator(".pm-message", {
          hasText: `${marker} правка`,
        }),
      ).toBeVisible();
    } finally {
      // A context of its own: the injected one is torn down with the fixture,
      // and a cleanup that runs against a closed context leaves the message
      // behind while reporting the failure as the test's.
      const cleanup = await authenticatedContext();
      await cleanup.delete(`${API_BASE_URL}/v1/messages/${messageId}`);
      await cleanup.dispose();
    }
  });
});
