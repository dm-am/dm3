import {
  test,
  expect,
  API_BASE_URL,
  secondaryUser,
  authenticatedContext,
} from "../../fixtures/auth";

/**
 * A message is `.chat-feed__message` (the feed both chats are drawn with); the
 * edit and delete controls live in the hover toolbar `.msg-toolbar` as buttons
 * labelled "Редактировать" and "Удалить".
 */
test.describe("Message Edit/Delete", () => {
  // A moderator edits only where moderation reaches, the global chat: in a
  // private chat a message of the other side carries no edit control for him.
  // The signed-in account is the administrator of the seed, so this holds the
  // rule and not the plain "not the author". The chat is the seed's own.
  test("offers no edit on a message of the other side of a private chat", async ({
    authenticatedPage,
    authContext,
  }) => {
    const chatResponse = await authContext.post(
      `${API_BASE_URL}/v1/chats/direct/${secondaryUser.username}`,
    );
    expect(chatResponse.ok(), "the direct chat must open").toBeTruthy();
    const chat = await chatResponse.json();
    const chatId = chat.resource?.id ?? chat.id;

    const messagesResponse = await authContext.get(
      `${API_BASE_URL}/v1/chats/${chatId}/messages`,
    );
    expect(messagesResponse.ok()).toBeTruthy();
    const { resources } = await messagesResponse.json();
    const theirs = (resources ?? []).find(
      (message: { isRemoved?: boolean; author?: { username?: string } }) =>
        !message.isRemoved &&
        message.author?.username === secondaryUser.username,
    );
    expect(
      theirs,
      `the seeded chat with ${secondaryUser.username} holds a message of his`,
    ).toBeTruthy();

    await authenticatedPage.goto(`/messenger/user/${secondaryUser.username}`);
    const message = authenticatedPage.locator(
      `.chat-feed__message[data-id="${theirs.id}"]`,
    );
    await expect(message).toBeVisible({ timeout: 10000 });

    // The toolbar answering at all is the precondition: the link to the
    // message stands on every one, the edit and delete controls are missing.
    const toolbar = authenticatedPage.locator(".msg-toolbar");
    await expect(async () => {
      await authenticatedPage.mouse.move(0, 0);
      await message.hover();
      await expect(
        toolbar.getByRole("link", { name: "Ссылка на сообщение" }),
      ).toBeVisible({ timeout: 1000 });
    }).toPass({ timeout: 10000 });
    await expect(
      toolbar.getByRole("button", { name: "Редактировать" }),
    ).toHaveCount(0);
    await expect(toolbar.getByRole("button", { name: "Удалить" })).toHaveCount(
      0,
    );
  });

  // The body posts its own message, edits that, and deletes it in a finally
  // with a context of its own, so the seed is untouched either way.
  //
  // The editor behind .msg-edit is the BBCode editor, which opens in its
  // visual mode, a contenteditable surface and not a textarea. The text is
  // typed in its BBCode mode, the one whose text is what the server receives.
  test("saves an edit to a message of its own", async ({
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

      const own = authenticatedPage.locator(".chat-feed__message", {
        hasText: marker,
      });
      await expect(own).toBeVisible({ timeout: 10000 });

      // The page scrolls to the newest message as it opens, and a pointer that
      // arrives while the list is still moving is not taken for a hover. The
      // pointer is taken off the message and brought back until the toolbar
      // answers, the way a reader would move it again.
      const toolbar = authenticatedPage.locator(".msg-toolbar");
      const edit = toolbar.getByRole("button", { name: "Редактировать" });
      await expect(async () => {
        await authenticatedPage.mouse.move(0, 0);
        await own.hover();
        await expect(edit).toBeVisible({ timeout: 1000 });
      }).toPass({ timeout: 10000 });
      await edit.click();

      const editor = authenticatedPage.locator(".msg-edit");
      await expect(editor).toBeVisible();
      await editor.getByRole("button", { name: "Режим BBCode" }).click();
      // The box opens holding the text of the message, not empty with the text on its way.
      const field = editor.locator("textarea.bbcode-textarea");
      await expect(field).toHaveValue(marker);
      await field.fill(`${marker} правка`);
      await editor.getByRole("button", { name: "Сохранить" }).click();

      await expect(editor).toBeHidden();
      await expect(
        authenticatedPage.locator(".chat-feed__message", {
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
