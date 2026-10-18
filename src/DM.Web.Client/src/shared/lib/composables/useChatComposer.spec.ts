import { describe, it, expect, vi } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ref } from "vue";
import { useChatComposer, type ChatActionResult } from "./useChatComposer";
import type { GeneralError } from "@/shared/api/models/common";

/**
 * A 404 the server worded: the line at the place says it as it came.
 */
const refusal: GeneralError = {
  type: "about:blank",
  title: "Сообщение не найдено",
  status: 404,
  traceId: "t",
};

function setup(overrides: Partial<Parameters<typeof useChatComposer>[0]> = {}) {
  const editor = { clear: vi.fn() };
  const options = {
    sending: ref(false),
    send: vi.fn(async (): Promise<ChatActionResult> => ({ error: null })),
    remove: vi.fn(async (): Promise<ChatActionResult> => ({ error: null })),
    editor: ref(editor),
    confirmingDeleteId: ref<string | null>(null),
    scrollToBottom: vi.fn(),
    ...overrides,
  };
  return { options, editor, composer: useChatComposer(options) };
}

describe("useChatComposer", () => {
  it("empties the field before the request and drops the draft only after it lands", async () => {
    const { options, editor, composer } = setup();
    composer.newMessage.value = "текст";

    const sent = composer.handleSend();
    // Emptying before the answer is what makes sending feel instant.
    expect(composer.newMessage.value).toBe("");
    expect(editor.clear).not.toHaveBeenCalled();

    await sent;
    expect(options.send).toHaveBeenCalledWith("текст");
    // clear() also deletes the saved draft — the copy that outlives the tab.
    expect(editor.clear).toHaveBeenCalledTimes(1);
    expect(options.scrollToBottom).toHaveBeenCalledTimes(1);
  });

  it("gives the text back when the send is refused, draft included", async () => {
    const { options, editor, composer } = setup({
      send: vi.fn(async (): Promise<ChatActionResult> => ({ error: refusal })),
    });
    composer.newMessage.value = "текст";

    await composer.handleSend();

    expect(composer.newMessage.value).toBe("текст");
    expect(editor.clear).not.toHaveBeenCalled();
    expect(options.scrollToBottom).not.toHaveBeenCalled();
    // The reason stands under "Отправить", not in a plate.
    expect(composer.sendResult.refusal.value).toBe("Сообщение не найдено");
  });

  it("sends nothing while a send is in flight, or with blank text", async () => {
    const { options, composer } = setup({ sending: ref(true) });
    composer.newMessage.value = "текст";
    await composer.handleSend();

    const idle = setup();
    idle.composer.newMessage.value = "   ";
    await idle.composer.handleSend();

    expect(options.send).not.toHaveBeenCalled();
    expect(idle.options.send).not.toHaveBeenCalled();
  });

  it("honours the caller's own precondition", async () => {
    const { options, composer } = setup({ canSend: () => false });
    composer.newMessage.value = "текст";

    await composer.handleSend();

    expect(options.send).not.toHaveBeenCalled();
    // The text stays put: nothing was attempted.
    expect(composer.newMessage.value).toBe("текст");
  });

  it("takes the delete confirmation up and back down", async () => {
    const { options, composer } = setup();

    composer.requestDelete("m1");
    expect(options.confirmingDeleteId.value).toBe("m1");

    composer.cancelDelete();
    expect(options.confirmingDeleteId.value).toBeNull();

    // Nothing to confirm — the store is not called.
    await composer.confirmDelete();
    expect(options.remove).not.toHaveBeenCalled();

    composer.requestDelete("m2");
    await composer.confirmDelete();
    expect(options.remove).toHaveBeenCalledWith("m2");
    expect(options.confirmingDeleteId.value).toBeNull();
  });

  it("closes the confirmation even when the delete is refused", async () => {
    const { options, composer } = setup({
      remove: vi.fn(
        async (): Promise<ChatActionResult> => ({
          error: refusal,
        }),
      ),
    });

    composer.requestDelete("m1");
    await composer.confirmDelete();

    // The row must not keep showing "точно удалить?" over a message that is
    // still there; the line under the message says the delete did not happen.
    expect(options.confirmingDeleteId.value).toBeNull();
    expect(composer.messageRefusal.value).toEqual({
      id: "m1",
      text: "Сообщение не найдено",
    });
  });

  it("says a refused press on a message under that message, and forgets it on the next delete", async () => {
    const { composer } = setup({
      remove: vi.fn(async (): Promise<ChatActionResult> => ({ error: null })),
    });

    composer.refuseMessage("m3", refusal, "Не удалось поставить лайк");
    expect(composer.messageRefusal.value).toEqual({
      id: "m3",
      text: "Сообщение не найдено",
    });

    composer.requestDelete("m4");
    await composer.confirmDelete();

    expect(composer.messageRefusal.value).toBeNull();
  });

  // An expired session is about the reader: the plate says it, the line not.
  it("leaves an expired session to its plate", () => {
    const { composer } = setup();

    composer.refuseMessage(
      "m5",
      { ...refusal, status: 401, title: "" },
      "Не удалось сохранить сообщение",
    );

    expect(composer.messageRefusal.value).toBeNull();
  });
});

/**
 * A line under a message says the last press on it was refused. A retry that
 * goes through fills the heart or saves the edit, and a line still standing
 * under it would say the opposite of the message above it. So each press on a
 * message takes its line down before it asks the server, in both chats: the
 * presses live in the one feed both chats are drawn with (chatParity.spec.ts
 * holds that they are).
 */
describe("the line under a message on the next press", () => {
  it("is taken down for that message and no other", () => {
    const { composer } = setup();
    composer.refuseMessage("m3", refusal, "Не удалось поставить лайк");

    composer.clearMessageRefusal("m4");
    expect(composer.messageRefusal.value?.id).toBe("m3");

    composer.clearMessageRefusal("m3");
    expect(composer.messageRefusal.value).toBeNull();
  });

  const SRC = join(dirname(fileURLToPath(import.meta.url)), "../../..");

  /** The body of one function of a component, up to the next top-level one. */
  const body = (file: string, name: string): string => {
    const source = readFileSync(join(SRC, file), "utf8");
    const start = source.indexOf(`async function ${name}(`);
    expect(start, `${file} has ${name}`).toBeGreaterThan(-1);
    const end = source.indexOf("\n}\n", start);
    return source.slice(start, end);
  };

  it.each([
    ["widgets/chat-feed/ChatFeed.vue", "toggleLike"],
    ["widgets/chat-feed/ChatFeed.vue", "saveEdit"],
  ])("%s %s clears it before the request", (file, name) => {
    const text = body(file, name);
    const cleared = text.indexOf("clearMessageRefusal(");
    expect(cleared, "clears the line").toBeGreaterThan(-1);
    expect(cleared).toBeLessThan(text.indexOf("await "));
  });
});
