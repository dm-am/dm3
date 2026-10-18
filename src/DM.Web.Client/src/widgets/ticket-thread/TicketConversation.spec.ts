/**
 * @vitest-environment jsdom
 */

/**
 * The conversation of a ticket, under its block: the answers of the author
 * and of moderation, and the form to answer in under them.
 *
 * An answer here is the comment of a topic in its compact look, the one look
 * a comment has on DM2, whatever the reader chose for the discussions; read
 * only: a ticket is the record of how it was settled, so nothing in it is
 * edited, deleted, liked or warned about, by anybody. The guest who filed a
 * ticket signs his answers "Гость". The form counts to the length the server
 * takes an answer of a ticket at, 10000, not to the 50000 of a topic's
 * comment. Moderation chooses in it, over the field, the status its answer
 * moves the ticket to, "Ожидает ответа пользователя" unless it closes the
 * ticket or marks it as spam, and closing needs no word. A closed ticket has
 * no form and says in one plate, the plate of every place the reader may not
 * write in, when it was closed and by whom. The block knows nothing of who
 * reads it: the page hands it the ticket, the sending and what to say while
 * nobody has answered.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import { useUiStore } from "@/shared/stores/ui";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import type { TicketComment } from "@/entities/ticket";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import TicketConversation from "./TicketConversation.vue";
import type { ConversationTicket } from "./types";

const HERE = dirname(fileURLToPath(import.meta.url));

const route = vi.hoisted(() => ({
  query: {} as Record<string, string>,
  hash: "",
}));

vi.mock("vue-router", () => ({
  useRoute: () => route,
  RouterLink: { template: "<a><slot /></a>" },
}));

/** Where every link of the block leads, kept on the anchor. */
const RouterLinkStub = {
  name: "RouterLink",
  props: ["to"],
  template: "<a><slot /></a>",
};

const editor = vi.hoisted(() => ({ cleared: 0 }));

// A heavy tiptap component. What the form owes the reader is read off what it
// hands the editor: the text, and the length its counter counts to.
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

beforeEach(() => {
  route.query = {};
  route.hash = "";
  editor.cleared = 0;
  localStorage.clear();
  setActivePinia(createPinia());
  send = vi.fn().mockResolvedValue({ error: null });
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
});

const signInAs = (username: string, role?: string) =>
  localStorage.setItem("user", JSON.stringify({ username, role }));

function render(
  ticket: ConversationTicket,
  extra: Record<string, unknown> = {},
): VueWrapper {
  return mount(TicketConversation, {
    global: { stubs: { RouterLink: RouterLinkStub } },
    props: {
      ticket,
      send,
      draftKey: "ticket:t-1:comment",
      ...extra,
    } as never,
  });
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
  it("stand oldest first, numbered from one, each a comment of the site", () => {
    const wrapper = render(open());

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

  // The number alone says nothing to a screen reader, so it is read out with
  // the name of what it counts, and a ticket holds answers.
  it("read their numbers out as answers", () => {
    const wrapper = render(open());

    expect(
      wrapper.findAll(".comment-number").map((n) => n.attributes("aria-label")),
    ).toEqual(["Ответ 1", "Ответ 2"]);
  });

  // A senior moderator is offered everything on a comment of a topic: the
  // edit and the removal of anybody's, the warning about a user's, the heart.
  it("offer nothing to press, even to a senior moderator", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = render(open());

    expect(wrapper.findAll(".comment")).toHaveLength(2);
    expect(wrapper.find(".caption-action").exists()).toBe(false);
    expect(wrapper.find(".like-badge").exists()).toBe(false);
    expect(wrapper.text()).not.toMatch(/Редактировать|Удалить|Предупреждение/);
  });

  it("sign the guest's with a word that leads nowhere", () => {
    const wrapper = render(open({ comments: [fromGuest] }));
    const caption = wrapper.get(".comment-footer .author-info");

    expect(caption.text().replace(/\s+/g, " ")).toBe("Автор: Гость");
    expect(caption.find("a").exists()).toBe(false);
    expect(caption.text()).not.toMatch(/online|offline/);
  });

  it("sign a removed account's as every such name is signed", () => {
    const wrapper = render(open({ comments: [fromRemoved] }));

    expect(
      wrapper.get(".comment-footer .author-info").text().replace(/\s+/g, " "),
    ).toBe("Автор: Аккаунт удален");
  });

  // A comment of DM2 has one look, the compact one, and so has an answer of a
  // ticket, whatever the reader chose for the discussions of the site.
  it.each(["full", "compact"] as const)(
    "are compact comments with the reader's layout %s",
    (layout) => {
      useUiStore().setMessageLayout(layout);
      const wrapper = render(open({ comments: [fromGuest] }));

      expect(wrapper.get(".comment").classes()).toContain("compact");
    },
  );
});

describe("the form of a ticket", () => {
  it("counts to the length the server takes an answer of a ticket at", () => {
    const counter = render(open())
      .getComponent({ name: "BBCodeEditor" })
      .props("maxLength");

    expect(counter).toBe(10000);
  });

  it("asks for an answer, not a comment", () => {
    const field = render(open()).getComponent({ name: "BBCodeEditor" });

    expect(field.props("placeholder")).toBe("Написать ответ...");
  });

  it("sends what the author wrote through the page, with no status", async () => {
    const wrapper = render(open());

    await wrapper.get(".editor-stub").setValue("Вот ссылка");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(send).toHaveBeenCalledWith("Вот ссылка", undefined);
    expect(editor.cleared).toBe(1);
  });

  it("sends nothing empty for the author", async () => {
    const wrapper = render(open());

    expect(wrapper.find("select").exists()).toBe(false);
    expect(sendButton(wrapper)!.attributes("disabled")).toBeDefined();
  });

  it("gives the text back with the reason when the answer is refused", async () => {
    send.mockResolvedValueOnce({
      error: { type: "", title: "Обращение закрыто", status: 400, traceId: "" },
    });
    const wrapper = render(open());

    await wrapper.get(".editor-stub").setValue("Вот ссылка");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe("Вот ссылка");
    expect(editor.cleared).toBe(0);
    expect(wrapper.text()).toContain("Обращение закрыто");
  });

  it("is there only while the reader may write", () => {
    const wrapper = render(open({ canComment: false }));

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(sendButton(wrapper)).toBeUndefined();
  });

  // An open ticket takes no answer past the hundredth (TicketTextLimits).
  it("says where the next word goes once the conversation is full", () => {
    const wrapper = render(open({ canComment: false }));

    expect(plates(wrapper)).toEqual([
      "В обращении 100 ответов, для продолжения подайте новое обращение",
    ]);
    expect(lines(wrapper)).toEqual([]);
  });
});

/**
 * Moderation chooses the status with its answer, over the field: the list of
 * a form of the site, "Ожидает ответа пользователя" first and chosen, then
 * "Закрыто" and "Спам". The text and the status go together, and a closing
 * needs no text.
 */
describe("the answer of moderation", () => {
  const moderated = () => render(open({ canResolve: true }));

  it("chooses the status over the field, waiting for the user at first", () => {
    const wrapper = moderated();
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
    const wrapper = moderated();

    await wrapper.get(".editor-stub").setValue("Пришлите ссылку");
    await sendButton(wrapper)!.trigger("click");
    await flushPromises();

    expect(send).toHaveBeenCalledWith("Пришлите ссылку", "WaitingForUser");
  });

  it("asks a word of an answer that waits for the user", () => {
    expect(sendButton(moderated())!.attributes("disabled")).toBeDefined();
  });

  it.each(["Closed", "Spam"] as const)(
    "closes as %s without a word",
    async (status) => {
      const wrapper = moderated();

      await wrapper.get("select").setValue(status);
      expect(sendButton(wrapper)!.attributes("disabled")).toBeUndefined();
      await sendButton(wrapper)!.trigger("click");
      await flushPromises();

      expect(send).toHaveBeenCalledWith("", status);
    },
  );

  it("closes with the last word when there is one", async () => {
    const wrapper = moderated();

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

  it("says what the page gives it to say, over the form", () => {
    const wrapper = render(open({ comments: [] }), { emptyText: WAIT });

    expect(lines(wrapper)).toEqual([WAIT]);
    expect(wrapper.find(".editor-stub").exists()).toBe(true);
  });

  it("says nothing where the page gives it nothing to say", () => {
    expect(lines(render(open({ comments: [] })))).toEqual([]);
  });

  it("says nothing of an answer to come once there are answers", () => {
    expect(lines(render(open(), { emptyText: WAIT }))).toEqual([]);
  });

  it("says nothing of an answer to come once it is closed", () => {
    const wrapper = render(closed({ comments: [] }), { emptyText: WAIT });

    expect(lines(wrapper)).toEqual([]);
    expect(plates(wrapper)).toEqual([
      `Обращение закрыто ${at(CLOSED)}, закрыл(а) TestModerator.`,
    ]);
  });
});

/**
 * "(а)" with no gender, as the records of a ban say "выдал(а)" and "снял(а)":
 * the next screen of moderation, and the same signature of what a moderator
 * did. The line stands in the plate of a place the reader may not write in,
 * DM2's "Тема закрыта. Добавление сообщений невозможно".
 */
describe("a closed ticket", () => {
  it("has no form and one plate under the answers: when, and by whom", () => {
    const wrapper = render(closed());

    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(plates(wrapper)).toEqual([
      `Обращение закрыто ${at(CLOSED)}, закрыл(а) TestModerator.`,
    ]);
  });

  it("says it was closed as spam", () => {
    expect(plates(render(closed({ status: "Spam" })))).toEqual([
      `Обращение закрыто как спам ${at(CLOSED)}, закрыл(а) TestModerator.`,
    ]);
  });

  it("names a closer whose account was removed as every such name", () => {
    expect(plates(render(closed({ resolvedByUsername: null })))).toEqual([
      `Обращение закрыто ${at(CLOSED)}, закрыл(а) Аккаунт удален.`,
    ]);
  });
});

/**
 * The answers stand a $medium step under the block of the ticket and a $small
 * one apart, as in the discussion of a topic, and the lines take no inset of
 * their own (the rule of the discussion's closed line, line 31).
 */
describe("the spacing of the conversation", () => {
  const rules = () =>
    styleRules(componentCss(resolve(HERE, "TicketConversation.vue")));
  const ruleOf = (selector: string) =>
    Object.assign(
      {},
      ...rules()
        .filter((rule) => rule.selector === selector)
        .map((rule) => rule.declarations),
    ) as Record<string, string>;

  it("keeps the topic's steps", () => {
    expect(ruleOf(".ticket-conversation")).toMatchObject({
      "margin-top": "16px",
      gap: "16px",
    });
    expect(ruleOf(".ticket-conversation__list")).toMatchObject({ gap: "8px" });
  });

  // Nothing in the block is inset but the block itself, a step under the
  // ticket: the lines stand where the form would, at the left of the column.
  it("gives its lines no inset", () => {
    const insets = rules().flatMap((rule) =>
      Object.keys(rule.declarations)
        .filter((property) => /^(padding|margin)/.test(property))
        .map((property) => `${rule.selector} ${property}`),
    );

    expect(insets).toEqual([".ticket-conversation margin-top"]);
  });
});
