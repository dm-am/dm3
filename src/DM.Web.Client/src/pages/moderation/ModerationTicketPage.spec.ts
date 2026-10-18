/**
 * @vitest-environment jsdom
 */

/**
 * A moderator's page of one ticket.
 *
 * The ticket works as a topic with answers and looks like one: its block with
 * the author in the caption, under a link back to the queue "Обращения", the
 * conversation under the block and the form at its foot. Nobody takes a
 * ticket in hand: any moderator to whom its reason is shown answers and closes
 * it, so there is no "Взять в работу".
 *
 * The moderator chooses in the form, over the field of the answer, the status
 * the answer moves the ticket to: "Ожидает ответа пользователя" unless he
 * closes it or marks it as spam, and closing needs no word. The text and the
 * status go in one request, and the page shows the ticket the server answers
 * with. The choice stands only where the server says the reader answers as
 * moderation (canResolve): never on a closed ticket, never for its author,
 * whose answer hands the ticket to moderation. The ticket punishes nobody: no
 * warning, no ban, no block "Решение" under the form.
 *
 * A ticket the moderator may not read (its reason is not shown to his role,
 * or it names him) is "not found", the same as one that never existed.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import {
  ticketApi,
  type TicketComment,
  type TicketDetails,
} from "@/entities/ticket";
import { TicketConversation, TicketThread } from "@/widgets/ticket-thread";
import ModerationTicketPage from "./ModerationTicketPage.vue";

enableAutoUnmount(afterEach);

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { ticketId: "t-1" }, query: {}, hash: "" }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return {
    useRoleGate: () => ({ hasAccess: ref(true), deniedText: "" }),
  };
});

// A heavy tiptap component, as a plain field: what it is for here is its
// value, and that the page holds one editor, the form's.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    name: "BBCodeEditor",
    props: ["modelValue", "maxLength", "draftKey", "disabled"],
    emits: ["update:modelValue", "submit"],
    methods: { clear() {} },
    template:
      "<textarea class='editor-stub' :value='modelValue' @input='$emit(\"update:modelValue\", $event.target.value)' />",
  },
}));

const person = (id: string, username: string, role: string) =>
  ({ id, username, role, lastActivityUtc: null }) as never;

const asked: TicketComment = {
  id: "c-1",
  author: person("u-mod", "TestModerator", "Moderator"),
  isGuest: false,
  createdUtc: "2026-10-03T18:00:00Z",
  text: "В каком комментарии было оскорбление?",
};

const answered = (text: string): TicketComment => ({
  id: "c-2",
  author: person("u-reader", "Moderator_Me", "Moderator"),
  isGuest: false,
  createdUtc: "2026-10-04T07:10:00Z",
  text,
});

const complaint = (over: Partial<TicketDetails> = {}): TicketDetails => ({
  id: "t-1",
  reporter: person("u-author", "Experienced", "RegularUser"),
  isGuest: false,
  status: "WaitingForModeration",
  subtype: "UserComplaint",
  createdUtc: "2026-10-03T17:38:00Z",
  description: "Оскорбляет участников\n\nНарушитель: LongestLoginPossible",
  descriptionHtml:
    "Оскорбляет участников<br /><br />Нарушитель: LongestLoginPossible",
  comment: "",
  commentsCount: 1,
  comments: [asked],
  canComment: true,
  canResolve: true,
  resolvedUtc: null,
  resolvedByUsername: null,
  ...over,
});

const closedAs = (over: Partial<TicketDetails> = {}) =>
  complaint({
    status: "Closed",
    canComment: false,
    canResolve: false,
    resolvedUtc: "2026-10-04T07:15:00Z",
    resolvedByUsername: "Moderator_Me",
    ...over,
  });

const serve = (ticket: TicketDetails) =>
  vi
    .spyOn(ticketApi, "getTicket")
    .mockResolvedValue({ data: { resource: ticket }, error: null } as never);

/** The server's answer to an answer: the ticket as it stands after it. */
const answerWith = (ticket: TicketDetails) =>
  vi
    .spyOn(ticketApi, "createComment")
    .mockResolvedValue({ data: { resource: ticket }, error: null } as never);

/** The moderator reading the page, as the auth store reads him back. */
const signIn = (id = "u-reader", username = "Moderator_Me") =>
  localStorage.setItem(
    "user",
    JSON.stringify({ id, username, role: "Moderator", settings: {} }),
  );

const render = async () => {
  const wrapper = mount(ModerationTicketPage, {
    global: {
      plugins: [createPinia()],
      stubs: { RouterLink: RouterLinkStub, PageTitle: true },
    },
  });
  await flushPromises();
  return wrapper;
};

const buttonOf = (wrapper: VueWrapper, words: string) =>
  wrapper.findAll("button").find((one) => one.text() === words);

/** The caption of the block, its spaces as a reader reads them. */
const caption = (wrapper: VueWrapper) =>
  wrapper.get(".record-block__caption").text().replace(/\s+/g, " ");

async function writeInForm(wrapper: VueWrapper, text: string) {
  await wrapper.get(".ticket-answer-form .editor-stub").setValue(text);
}

async function send(wrapper: VueWrapper) {
  await buttonOf(wrapper, "Отправить")!.trigger("click");
  await flushPromises();
}

describe("a moderator's page of one ticket", () => {
  beforeEach(() => {
    localStorage.clear();
    signIn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the ticket with its author, under a link back to the queue", async () => {
    serve(complaint());
    const wrapper = await render();

    const block = wrapper.getComponent(TicketThread);
    expect(block.props("withAuthor")).toBe(true);
    expect(block.props("answersTo")).toEqual({
      name: "moderation-ticket",
      params: { ticketId: "t-1" },
    });
    expect(caption(wrapper)).toContain("Автор: Experienced");
    const back = wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.text() === "Обращения");
    expect(back?.props("to")).toEqual({ name: "moderation-tickets" });
  });

  it("leads every ticket back to the one queue, the support desk's too", async () => {
    serve(complaint({ subtype: "Bug", guestEmail: "a@b.c" }));
    const wrapper = await render();

    const back = wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.text() === "Обращения");
    expect(back?.props("to")).toEqual({ name: "moderation-tickets" });
  });

  it("offers nobody to take the ticket in hand", async () => {
    serve(complaint());
    const wrapper = await render();

    expect(wrapper.text()).not.toContain("Взять в работу");
    expect(wrapper.text()).not.toContain("Разбирает");
    expect(wrapper.text()).not.toContain("В работе");
  });

  it("draws the conversation under the block and the form at its foot", async () => {
    serve(complaint());
    const wrapper = await render();

    const conversation = wrapper.getComponent(TicketConversation);
    expect(conversation.props("ticket")).toMatchObject({ id: "t-1" });
    expect(conversation.props("draftKey")).toBe("ticket:t-1:comment");
    expect(wrapper.findAll(".comment-text").map((t) => t.text())).toEqual([
      asked.text,
    ]);
    expect(wrapper.find(".ticket-answer-form").exists()).toBe(true);
  });

  it("chooses the status in the form, over the field, and punishes nobody", async () => {
    serve(complaint());
    const wrapper = await render();

    const select = wrapper.get(".ticket-answer-form select");
    expect(
      select.element.compareDocumentPosition(
        wrapper.get(".editor-stub").element,
      ) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect((select.element as HTMLSelectElement).value).toBe("WaitingForUser");
    // One editor and one button on the page, the form's.
    expect(wrapper.findAll(".editor-stub")).toHaveLength(1);
    expect(wrapper.findAll("button").map((b) => b.text())).toEqual([
      "Отправить",
    ]);
    expect(wrapper.text()).not.toMatch(
      /Решение|Закрыть обращение|Выдать предупреждение|Выдать бан/,
    );
    expect(wrapper.find("input[type='checkbox']").exists()).toBe(false);
  });

  it("offers no choice of status where the reader does not answer as moderation", async () => {
    serve(complaint({ canResolve: false }));
    const wrapper = await render();

    // The conversation stays: the author of a ticket who is a moderator
    // writes in it as its author.
    expect(wrapper.find(".ticket-answer-form").exists()).toBe(true);
    expect(wrapper.find(".ticket-answer-form select").exists()).toBe(false);
  });

  it("hands the turn to the author with an answer, and shows what the server answers", async () => {
    serve(complaint());
    const create = answerWith(
      complaint({
        status: "WaitingForUser",
        comments: [asked, answered("Нашли, разбираемся")],
        commentsCount: 2,
      }),
    );
    const wrapper = await render();

    await writeInForm(wrapper, "Нашли, разбираемся");
    await send(wrapper);

    expect(create).toHaveBeenCalledWith("t-1", {
      text: "Нашли, разбираемся",
      status: "WaitingForUser",
    });
    expect(wrapper.findAll(".comment")).toHaveLength(2);
    expect(caption(wrapper)).toContain("Ответы (2)");
    expect(caption(wrapper)).toContain("Ожидает ответа пользователя");
  });

  it("closes without a word and shows the ticket closed", async () => {
    serve(complaint());
    const create = answerWith(closedAs());
    const wrapper = await render();

    await wrapper.get(".ticket-answer-form select").setValue("Closed");
    await send(wrapper);

    expect(create).toHaveBeenCalledWith("t-1", { text: "", status: "Closed" });
    expect(wrapper.find(".ticket-answer-form").exists()).toBe(false);
    expect(wrapper.get(".refusal-note").text()).toContain(
      "закрыл(а) Moderator_Me.",
    );
    expect(caption(wrapper)).toContain("Закрыто");
  });

  it("closes with the last word in one request", async () => {
    serve(complaint());
    const create = answerWith(
      closedAs({
        status: "Spam",
        comments: [asked, answered("Реклама")],
        commentsCount: 2,
      }),
    );
    const wrapper = await render();

    await wrapper.get(".ticket-answer-form select").setValue("Spam");
    await writeInForm(wrapper, "Реклама");
    await send(wrapper);

    expect(create).toHaveBeenCalledTimes(1);
    expect(create).toHaveBeenCalledWith("t-1", {
      text: "Реклама",
      status: "Spam",
    });
    expect(wrapper.findAll(".comment-text").map((t) => t.text())).toEqual([
      asked.text,
      "Реклама",
    ]);
    expect(wrapper.get(".refusal-note").text()).toContain(
      "Обращение закрыто как спам",
    );
  });

  it("keeps the ticket and the text when the answer is refused", async () => {
    serve(complaint());
    vi.spyOn(ticketApi, "createComment").mockResolvedValue({
      data: null,
      error: { status: 400, title: "Обращение уже закрыто" },
    } as never);
    const wrapper = await render();

    await wrapper.get(".ticket-answer-form select").setValue("Closed");
    await writeInForm(wrapper, "Предупреждение выдано");
    await send(wrapper);

    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe("Предупреждение выдано");
    expect(wrapper.get(".ticket-answer-form").text()).toContain(
      "Обращение уже закрыто",
    );
    expect(caption(wrapper)).toContain("Ожидает ответа модерации");
  });

  it("hands the turn to moderation when the moderator who writes filed the ticket", async () => {
    signIn("u-author", "Experienced");
    serve(complaint({ canResolve: false, status: "WaitingForUser" }));
    const create = answerWith(
      complaint({
        canResolve: false,
        status: "WaitingForModeration",
        comments: [asked, answered("Дополню")],
      }),
    );
    const wrapper = await render();

    await writeInForm(wrapper, "Дополню");
    await send(wrapper);

    expect(create).toHaveBeenCalledWith("t-1", { text: "Дополню" });
    expect(caption(wrapper)).toContain("Ожидает ответа модерации");
  });

  // The ticket waits for moderation, and the reader answers as moderation: the
  // star is his. Once it waits for the author, it is not.
  it.each([
    ["WaitingForModeration", true, true],
    ["WaitingForUser", true, false],
    ["WaitingForModeration", false, false],
  ] as const)(
    "marks a ticket %s with the star where the reader moderates it (%s): %s",
    async (status, canResolve, starred) => {
      serve(complaint({ status, canResolve }));
      const wrapper = await render();

      expect(wrapper.find(".record-block__caption .star").exists()).toBe(
        starred,
      );
    },
  );

  it("finds no ticket the moderator may not read", async () => {
    vi.spyOn(ticketApi, "getTicket").mockResolvedValue({
      data: null,
      error: { status: 404 },
    } as never);
    const wrapper = await render();

    expect(wrapper.text()).toContain("Обращение не найдено");
    expect(wrapper.findComponent(TicketThread).exists()).toBe(false);
    expect(wrapper.find(".error-state").exists()).toBe(false);
  });

  it("offers to try again when the ticket did not load", async () => {
    vi.spyOn(ticketApi, "getTicket").mockResolvedValue({
      data: null,
      error: { status: 500 },
    } as never);
    const wrapper = await render();

    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить обращение",
    );
  });
});
