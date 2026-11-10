/**
 * @vitest-environment jsdom
 */

/** The conversation of a ticket: read-only answers, the form with its status, the closed line. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import { useUiStore } from "@/shared/stores/ui";
import type { TicketComment } from "@/entities/ticket";
import TicketConversation from "./TicketConversation.vue";
import type { ConversationTicket } from "./types";

const editor = vi.hoisted(() => ({ cleared: 0 }));

// A heavy tiptap component; the spec reads what the form hands it.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    name: "BBCodeEditor",
    props: ["modelValue", "maxLength", "draftKey", "disabled", "placeholder"],
    emits: ["update:modelValue", "submit"],
    methods: {
      clear() {
        editor.cleared += 1;
      },
    },
    template:
      "<textarea class='editor-stub' :value='modelValue' @input='$emit(\"update:modelValue\", $event.target.value)' />",
  },
}));

const CREATED = "2026-10-08T08:58:00Z";
const CLOSED = "2026-10-08T09:00:00Z";

/** A moment as the lines below print it, its spaces as a reader reads them. */
const at = (utc: string) => formatDateFull(utc).replace(/\s+/g, " ");

const fromModeration: TicketComment = {
  id: "c-1",
  author: {
    username: "TestModerator",
    role: "Moderator",
    lastActivityUtc: null,
  } as never,
  isGuest: false,
  createdUtc: CREATED,
  text: "Пришлите ссылку на сообщение",
};

const fromAuthor: TicketComment = {
  id: "c-2",
  author: {
    username: "Experienced",
    role: "RegularUser",
    lastActivityUtc: null,
  } as never,
  isGuest: false,
  createdUtc: CREATED,
  text: "Вот оно",
};

const fromGuest: TicketComment = {
  id: "c-3",
  author: null,
  isGuest: true,
  createdUtc: CREATED,
  text: "Почта та же",
};

const fromRemoved: TicketComment = {
  id: "c-4",
  author: null,
  isGuest: false,
  createdUtc: CREATED,
  text: "Уже неважно",
};

const open = (over: Partial<ConversationTicket> = {}): ConversationTicket => ({
  status: "WaitingForModeration",
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [fromModeration, fromAuthor],
  canComment: true,
  canResolve: false,
  ...over,
});

const closed = (over: Partial<ConversationTicket> = {}) =>
  open({
    status: "Closed",
    resolvedUtc: CLOSED,
    resolvedByUsername: "TestModerator",
    canComment: false,
    ...over,
  });

enableAutoUnmount(afterEach);

let send: ReturnType<typeof vi.fn>;
let pinia: Pinia;

/** The page of a ticket the block stands on, and the profiles its names link to. */
const routes: Address[] = [
  ["my-ticket", "/my-tickets/:ticketId"],
  ["profile", "/users/:username"],
  ["received-reviews", "/users/:username/received-reviews"],
];

beforeEach(() => {
  editor.cleared = 0;
  localStorage.clear();
  pinia = createPinia();
  setActivePinia(pinia);
  send = vi.fn().mockResolvedValue({ error: null });
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
});

const signInAs = (username: string, role?: string) =>
  localStorage.setItem("user", JSON.stringify({ username, role }));

async function render(
  ticket: ConversationTicket,
  extra: Record<string, unknown> = {},
  at = "/my-tickets/t-1",
): Promise<VueWrapper> {
  const { wrapper } = await mountPage(TicketConversation, at, {
    routes,
    pinia,
    attachTo: document.body,
    props: {
      ticket,
      send,
      draftKey: "ticket:t-1:comment",
      ...extra,
    },
  });
  return wrapper;
}

/** The grey lines of the block, by their words. */
const lines = (wrapper: VueWrapper) =>
  wrapper
    .findAll(".secondary-text")
    .map((line) => line.text().replace(/\s+/g, " ").trim());

/** The plates of the block: the lines of a place the reader may not write in. */
const plates = (wrapper: VueWrapper) =>
  wrapper
    .findAll(".refusal-note")
    .map((line) => line.text().replace(/\s+/g, " ").trim());

/** The button that sends the form, if there is one. */
const sendButton = (wrapper: VueWrapper) =>
  wrapper.findAll("button").find((button) => button.text() === "Отправить");

describe("the answers of a ticket", () => {
  it("stand oldest first, numbered from one, each a comment of the site", async () => {
    const wrapper = await render(open());

    expect(wrapper.findAll(".comment").map((c) => c.attributes("id"))).toEqual([
      "comment-c-1",
      "comment-c-2",
    ]);
    expect(wrapper.findAll(".comment-number").map((n) => n.text())).toEqual([
      "1",
      "2",
    ]);
    expect(wrapper.findAll(".comment-text").map((t) => t.text())).toEqual([
      "Пришлите ссылку на сообщение",
      "Вот оно",
    ]);
  });

  // The number is read out with the name of what it counts: answers.
  it("read their numbers out as answers", async () => {
    const wrapper = await render(open());

    expect(
      wrapper.findAll(".comment-number").map((n) => n.attributes("aria-label")),
    ).toEqual(["Ответ 1", "Ответ 2"]);
  });

  // A senior moderator, offered everything on a comment of a topic.
  it("offer nothing to press, even to a senior moderator", async () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = await render(open());

    expect(wrapper.findAll(".comment")).toHaveLength(2);
    expect(wrapper.find(".caption-action").exists()).toBe(false);
    expect(wrapper.find(".like-badge").exists()).toBe(false);
    expect(wrapper.text()).not.toMatch(/Редактировать|Удалить|Предупреждение/);
  });

  it("sign the guest's with a word that leads nowhere", async () => {
    const wrapper = await render(open({ comments: [fromGuest] }));
    const caption = wrapper.get(".comment-footer .author-info");

    expect(caption.text().replace(/\s+/g, " ")).toBe("Автор: Гость");
    expect(caption.find("a").exists()).toBe(false);
    expect(caption.text()).not.toMatch(/online|offline/);
  });

  it("sign a removed account's as every such name is signed", async () => {
    const wrapper = await render(open({ comments: [fromRemoved] }));

    expect(
      wrapper.get(".comment-footer .author-info").text().replace(/\s+/g, " "),
    ).toBe("Автор: Аккаунт удален");
  });

  // An answer has the one look of a DM2 comment, the compact one.
  it.each(["full", "compact"] as const)(
    "are compact comments with the reader's layout %s",
    async (layout) => {
      useUiStore().setMessageLayout(layout);
      const wrapper = await render(open({ comments: [fromGuest] }));

      expect(wrapper.get(".comment").classes()).toContain("compact");
    },
  );

  it("open at the answer the address names", async () => {
    const scrolled: string[] = [];
    // jsdom has no scrollIntoView of its own.
    Element.prototype.scrollIntoView = function (this: Element) {
      scrolled.push(this.id);
    };
    try {
      await render(open(), {}, "/my-tickets/t-1#comment-c-2");
      // The block waits out its layout before it scrolls.
      await new Promise((resolve) => setTimeout(resolve, 150));

      expect(scrolled).toEqual(["comment-c-2"]);
    } finally {
      delete (Element.prototype as Partial<Element>).scrollIntoView;
    }
  });
});

describe("the form of a ticket", () => {
  it("counts to the length the server takes an answer of a ticket at", async () => {
    const counter = (await render(open()))
      .getComponent({ name: "BBCodeEditor" })
      .props("maxLength");

    expect(counter).toBe(10000);
  });

  it("asks for an answer, not a comment", async () => {
    const field = (await render(open())).getComponent({ name: "BBCodeEditor" });

    expect(field.props("placeholder")).toBe("Написать ответ...");
  });

  it("sends what the author wrote through the page, with no status", async () => {
    const wrapper = await render(open());

    await wrapper.get(".editor-stub").setValue("Вот ссылка");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(send).toHaveBeenCalledWith("Вот ссылка", undefined);
    expect(editor.cleared).toBe(1);
  });

  it("sends nothing empty for the author", async () => {
    const wrapper = await render(open());

    expect(wrapper.find("select").exists()).toBe(false);
    expect(sendButton(wrapper)!.attributes("disabled")).toBeDefined();
  });

  it("gives the text back with the reason when the answer is refused", async () => {
    send.mockResolvedValueOnce({
      error: { type: "", title: "Обращение закрыто", status: 400, traceId: "" },
    });
    const wrapper = await render(open());

    await wrapper.get(".editor-stub").setValue("Вот ссылка");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe("Вот ссылка");
    expect(editor.cleared).toBe(0);
    expect(wrapper.text()).toContain("Обращение закрыто");
  });

  it("is gone once the ticket is closed", async () => {
    const wrapper = await render(closed());

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(sendButton(wrapper)).toBeUndefined();
  });

  // An open ticket takes no answer past the limit (TicketTextLimits): neither a form nor a line.
  it("shows nothing once the conversation is full", async () => {
    const wrapper = await render(open({ canComment: false }));

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(plates(wrapper)).toEqual([]);
    expect(wrapper.text()).not.toContain("В обращении");
  });

  // The limit reached while the form was open: the text is the server's own.
  it("shows the refusal of the server when the limit came while the form was open", async () => {
    send.mockResolvedValue({
      error: {
        type: "",
        title:
          "В обращении уже 100 ответов, для продолжения подайте новое обращение",
        status: 400,
        traceId: "",
      },
    });
    const wrapper = await render(open());

    await wrapper.get(".editor-stub").setValue("Еще слово");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("В обращении уже 100 ответов");
  });
});

/** Moderation chooses the status with its answer; a closing needs no text. */
describe("the answer of moderation", () => {
  const moderated = () => render(open({ canResolve: true }));

  it("chooses the status over the field, waiting for the user at first", async () => {
    const wrapper = await moderated();
    const select = wrapper.get("select");
    const options = select
      .findAll("option")
      .map((option) => [option.attributes("value"), option.text()]);

    expect(options).toEqual([
      ["WaitingForUser", "Ожидает ответа пользователя"],
      ["Closed", "Закрыто"],
      ["Spam", "Спам"],
    ]);
    expect((select.element as HTMLSelectElement).value).toBe("WaitingForUser");
    expect(wrapper.get(".form-field-label").text()).toBe("Статус");
    const order =
      select.element.compareDocumentPosition(
        wrapper.get(".editor-stub").element,
      ) & Node.DOCUMENT_POSITION_FOLLOWING;
    expect(order, "the status stands over the field").toBeTruthy();
  });

  it("sends the status chosen with the text", async () => {
    const wrapper = await moderated();

    await wrapper.get(".editor-stub").setValue("Пришлите ссылку");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(send).toHaveBeenCalledWith("Пришлите ссылку", "WaitingForUser");
  });

  it("asks a word of an answer that waits for the user", async () => {
    expect(sendButton(await moderated())!.attributes("disabled")).toBeDefined();
  });

  it.each(["Closed", "Spam"] as const)(
    "closes as %s without a word",
    async (status) => {
      const wrapper = await moderated();

      await wrapper.get("select").setValue(status);
      expect(sendButton(wrapper)!.attributes("disabled")).toBeUndefined();
      await sendButton(wrapper)!.trigger("click");
      await flushPromises();

      expect(send).toHaveBeenCalledWith("", status);
    },
  );

  it("closes with the last word when there is one", async () => {
    const wrapper = await moderated();

    await wrapper.get("select").setValue("Closed");
    await wrapper.get(".editor-stub").setValue("Предупреждение снято");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(send).toHaveBeenCalledTimes(1);
    expect(send).toHaveBeenCalledWith("Предупреждение снято", "Closed");
  });
});

describe("a ticket nobody answered yet", () => {
  const WAIT =
    "Когда модерация ответит, здесь появится ответ, а вам придет уведомление";

  it("says what the page gives it to say, over the form", async () => {
    const wrapper = await render(open({ comments: [] }), { emptyText: WAIT });

    expect(lines(wrapper)).toEqual([WAIT]);
    expect(wrapper.find(".editor-stub").exists()).toBe(true);
  });

  it("says nothing where the page gives it nothing to say", async () => {
    expect(lines(await render(open({ comments: [] })))).toEqual([]);
  });

  it("says nothing of an answer to come once there are answers", async () => {
    expect(lines(await render(open(), { emptyText: WAIT }))).toEqual([]);
  });

  it("says nothing of an answer to come once it is closed", async () => {
    const wrapper = await render(closed({ comments: [] }), { emptyText: WAIT });

    expect(lines(wrapper)).toEqual([]);
    expect(plates(wrapper)).toEqual([
      `Обращение закрыто ${at(CLOSED)}, закрыл(а) TestModerator.`,
    ]);
  });
});

/** The closed line, "(а)" with no gender as in the records of a ban, in the plate. */
describe("a closed ticket", () => {
  it("has no form and one plate under the answers: when, and by whom", async () => {
    const wrapper = await render(closed());

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(plates(wrapper)).toEqual([
      `Обращение закрыто ${at(CLOSED)}, закрыл(а) TestModerator.`,
    ]);
  });

  it("says it was closed as spam", async () => {
    expect(plates(await render(closed({ status: "Spam" })))).toEqual([
      `Обращение закрыто как спам ${at(CLOSED)}, закрыл(а) TestModerator.`,
    ]);
  });

  it("names a closer whose account was removed as every such name", async () => {
    expect(plates(await render(closed({ resolvedByUsername: null })))).toEqual([
      `Обращение закрыто ${at(CLOSED)}, закрыл(а) Аккаунт удален.`,
    ]);
  });
});
