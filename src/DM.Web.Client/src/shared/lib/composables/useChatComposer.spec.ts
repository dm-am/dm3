import { describe, it, expect, vi } from "vitest";
import { ref } from "vue";
import { useChatComposer, type ChatActionResult } from "./useChatComposer";
import type { GeneralError } from "@/shared/api/models/common";

/**
 * A 404: the interceptor stays quiet about those, so notifyFailure reaches the
 * toast layer and nothing here has to stub it out.
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
    // still there; the toast is what says the delete did not happen.
    expect(options.confirmingDeleteId.value).toBeNull();
  });
});
