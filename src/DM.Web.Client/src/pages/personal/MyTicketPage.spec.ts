/**
 * @vitest-environment jsdom
 */

// One of the member's tickets: block, conversation, form. His answer hands the
// turn to moderation and the page shows the ticket the server answers with.
// Somebody else's ticket is "not found".
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
import MyTicketPage from "./MyTicketPage.vue";

enableAutoUnmount(afterEach);

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { ticketId: "t-1" }, query: {}, hash: "" }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

// A heavy tiptap component; the page owes the editor the text and the draft key.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeAnswer(),
);

const PENDING =
  "Когда модерация ответит, здесь появится ответ, а вам придет уведомление";

const person = (username: string, role: string) =>
  ({ id: `u-${username}`, username, role, lastActivityUtc: null }) as never;

const asked: TicketComment = {
  id: "c-1",
  author: person("TestModerator", "Moderator"),
  isGuest: false,
  createdUtc: "2026-10-06T08:00:00Z",
  text: "Пришлите, пожалуйста, ссылку на страницу",
};

const reply: TicketComment = {
  id: "c-2",
  author: person("Experienced", "RegularUser"),
  isGuest: false,
  createdUtc: "2026-10-06T09:00:00Z",
  text: "Вот она: /games/example",
};

const mine = (over: Partial<TicketDetails> = {}): TicketDetails => ({
  id: "t-1",
  reporter: person("Experienced", "RegularUser"),
  isGuest: false,
  status: "WaitingForUser",
  subtype: "Bug",
  createdUtc: "2026-10-05T18:20:00Z",
  description: "Не грузится аватар",
  comment: "",
  commentsCount: 1,
  comments: [asked],
  canComment: true,
  canResolve: false,
  resolvedUtc: null,
  resolvedByUsername: null,
  ...over,
});

const serve = (ticket: TicketDetails) =>
  vi
    .spyOn(ticketApi, "getTicket")
    .mockResolvedValue({ data: { resource: ticket }, error: null } as never);

const render = async () => {
  const wrapper = mount(MyTicketPage, {
    global: {
      plugins: [createPinia()],
      stubs: { RouterLink: RouterLinkStub, PageTitle: true },
    },
  });
  await flushPromises();
  return wrapper;
};

/** The caption of the block, its spaces as a reader reads them. */
const caption = (wrapper: VueWrapper) =>
  wrapper.get(".record-block__caption").text().replace(/\s+/g, " ");

/** Writes into the form and presses "Отправить". */
async function answer(wrapper: VueWrapper, text: string) {
  await wrapper.get(".ticket-answer-form .editor-stub").setValue(text);
  const send = wrapper
    .findAll(".ticket-answer-form button")
    .find((button) => button.text() === "Отправить");
  await send!.trigger("click");
  await flushPromises();
}

describe("the page of one of my tickets", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("reads the ticket of the address and shows it whole, its author in the caption", async () => {
    const get = serve(mine());
    const wrapper = await render();

    expect(get).toHaveBeenCalledWith("t-1");
    const block = wrapper.getComponent(TicketThread);
    expect(block.props("ticket")).toMatchObject({ id: "t-1" });
    expect(block.props("to")).toBeFalsy();
    expect(caption(wrapper)).toContain("Автор: Experienced");
    expect(caption(wrapper)).toContain("Ответы (1)");
    expect(block.props("answersTo")).toEqual({
      name: "my-ticket",
      params: { ticketId: "t-1" },
    });
  });

  // The ticket waits for its author: the star is his, only while it waits.
  it.each([
    ["WaitingForUser", true],
    ["WaitingForModeration", false],
  ] as const)(
    "marks a ticket %s with the star: %s",
    async (status, starred) => {
      localStorage.setItem(
        "user",
        JSON.stringify({
          id: "u-Experienced",
          username: "Experienced",
          role: "RegularUser",
        }),
      );
      serve(mine({ status }));
      const wrapper = await render();

      expect(wrapper.find(".record-block__caption .star").exists()).toBe(
        starred,
      );
    },
  );

  it("draws the conversation under the block and the form at its foot", async () => {
    serve(mine());
    const wrapper = await render();

    const conversation = wrapper.getComponent(TicketConversation);
    expect(conversation.props("ticket")).toMatchObject({ id: "t-1" });
    expect(wrapper.findAll(".comment")).toHaveLength(1);
    expect(wrapper.get(".comment-text").text()).toBe(asked.text);
    expect(wrapper.find(".ticket-answer-form").exists()).toBe(true);
    // The author chooses no status: his answer hands the ticket to moderation.
    expect(wrapper.find(".ticket-answer-form select").exists()).toBe(false);
    const block = wrapper.get(".ticket-thread").element;
    expect(
      block.compareDocumentPosition(conversation.element) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("leads back to the list of them", async () => {
    serve(mine());
    const wrapper = await render();

    const back = wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.text() === "Мои обращения");
    expect(back?.props("to")).toEqual({ name: "my-tickets" });
  });

  it("says what will come while nobody has answered", async () => {
    serve(
      mine({ status: "WaitingForModeration", comments: [], commentsCount: 0 }),
    );
    const wrapper = await render();

    expect(wrapper.text()).toContain(PENDING);
    // Said once, in the place of the answer.
    expect(wrapper.find(".pending").exists()).toBe(false);
  });

  it("says nothing of the kind once an answer stands there", async () => {
    serve(mine());
    const wrapper = await render();

    expect(wrapper.text()).not.toContain(PENDING);
  });

  it("writes the author's answer and shows the ticket the server answers with", async () => {
    serve(mine());
    const answered = mine({
      status: "WaitingForModeration",
      comments: [asked, reply],
      commentsCount: 2,
    });
    const create = vi.spyOn(ticketApi, "createComment").mockResolvedValue({
      data: { resource: answered },
      error: null,
    } as never);
    const wrapper = await render();

    await answer(wrapper, "Вот она: /games/example");

    expect(create).toHaveBeenCalledWith("t-1", {
      text: "Вот она: /games/example",
    });
    expect(wrapper.findAll(".comment-text").map((t) => t.text())).toEqual([
      asked.text,
      reply.text,
    ]);
    expect(wrapper.findAll(".comment-number").map((n) => n.text())).toEqual([
      "1",
      "2",
    ]);
    expect(caption(wrapper)).toContain("Ответы (2)");
    expect(caption(wrapper)).toContain("Ожидает ответа модерации");
    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe("");
  });

  it("keeps the text and the conversation as they were when the answer is refused", async () => {
    serve(mine());
    vi.spyOn(ticketApi, "createComment").mockResolvedValue({
      data: null,
      error: { status: 400, title: "Обращение закрыто" },
    } as never);
    const wrapper = await render();

    await answer(wrapper, "Еще одно");

    expect(wrapper.findAll(".comment")).toHaveLength(1);
    expect(caption(wrapper)).toContain("Ожидает ответа пользователя");
    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe("Еще одно");
  });

  it("keeps the unsent text of the form under the ticket", async () => {
    serve(mine());
    const wrapper = await render();

    expect(wrapper.getComponent(TicketConversation).props("draftKey")).toBe(
      "ticket:t-1:comment",
    );
  });

  it("offers no form on a closed ticket and says who closed it", async () => {
    serve(
      mine({
        status: "Closed",
        canComment: false,
        resolvedUtc: "2026-10-06T10:00:00Z",
        resolvedByUsername: "TestModerator",
      }),
    );
    const wrapper = await render();

    expect(wrapper.find(".ticket-answer-form").exists()).toBe(false);
    expect(wrapper.get(".refusal-note").text()).toContain(
      "закрыл(а) TestModerator.",
    );
  });

  it("finds no ticket that is not the reader's", async () => {
    vi.spyOn(ticketApi, "getTicket").mockResolvedValue({
      data: null,
      error: { status: 404 },
    } as never);
    const wrapper = await render();

    expect(wrapper.text()).toContain("Обращение не найдено");
    expect(wrapper.findComponent(TicketThread).exists()).toBe(false);
    expect(wrapper.findComponent(TicketConversation).exists()).toBe(false);
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
