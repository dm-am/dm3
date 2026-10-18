/**
 * @vitest-environment jsdom
 */

/**
 * The page of a guest's ticket, at /support/track/<token>.
 *
 * A guest gets no letters about his ticket (LEGAL-18), so this page is the
 * only place the answer reaches him. It says so in one sentence with its own
 * address in it as an ordinary link, to keep: no button that copies it and no
 * grey line with the token. It shows the ticket as the author's page does:
 * the block every screen draws a ticket with, the conversation under it and
 * the form at its foot. The guest is signed "Гость", in the caption of the
 * block and on his answers.
 *
 * His answer goes by the token, as the reading does, and is his whoever may
 * be signed in on the device: it hands the turn to moderation, and the page
 * puts it at the end of the conversation at once, without reading the ticket
 * again (every reading by the token costs him a place under the limit of five
 * a minute). Its unsent text is kept nowhere: the only thing the page holds
 * that tells one guest ticket from another is the token, and a token is not
 * written into the names of the browser's storage.
 *
 * The address carries the tracking token and nothing else, and that is a
 * guard, not a detail: the token is the only credential of the call, and the
 * number of the ticket in its place would open somebody else's ticket with the
 * address he left and the answer he got. So the page hands whatever stands in
 * the address to the tracking call as the token, never asks for a ticket by
 * its number, and reads a token that matches nothing as "не найдено".
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import {
  ticketApi,
  type TicketComment,
  type TrackedTicket,
} from "@/entities/ticket";
import { TicketConversation, TicketThread } from "@/widgets/ticket-thread";
import TicketTrackPage from "./TicketTrackPage.vue";

enableAutoUnmount(afterEach);

const { route } = vi.hoisted(() => ({
  route: {
    params: { token: "" } as Record<string, string>,
    query: {},
    hash: "",
  },
}));
vi.mock("vue-router", () => ({
  useRoute: () => route,
  useRouter: () => ({
    push: vi.fn(),
    resolve: (to: { params: { token: string } }) => ({
      href: `/support/track/${to.params.token}`,
    }),
  }),
}));

const TOKEN = "k7Qm2xV9pLr4Tz8N";
const NOT_FOUND = "Обращение по этой ссылке не найдено";

// A heavy tiptap component. What the page owes the form is read off what it
// hands the editor: the text and the key of its draft.
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

const asked: TicketComment = {
  id: "c-1",
  author: {
    id: "u-1",
    username: "SolohinLex",
    role: "Admin",
    lastActivityUtc: null,
  } as never,
  isGuest: false,
  createdUtc: "2026-10-05T19:00:00Z",
  text: "На какую почту был зарегистрирован аккаунт?",
};

const guestReply: TicketComment = {
  id: "c-2",
  author: null,
  isGuest: true,
  createdUtc: "2026-10-05T19:30:00Z",
  text: "На старую, она больше не работает",
};

const tracked: TrackedTicket = {
  status: "WaitingForUser",
  subtype: "AccessRecovery",
  subject: "",
  description: "Не могу войти",
  descriptionHtml: "Не могу войти",
  createdUtc: "2026-10-05T18:20:00Z",
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [asked],
  canComment: true,
};

const render = () =>
  mount(TicketTrackPage, {
    global: {
      plugins: [createPinia()],
      stubs: { RouterLink: RouterLinkStub, PageTitle: true },
    },
  });

const serve = (ticket: TrackedTicket = tracked) =>
  vi
    .spyOn(ticketApi, "trackTicket")
    .mockResolvedValue({ data: { resource: ticket }, error: null } as never);

/** The caption of the block, its spaces as a reader reads them. */
const caption = (wrapper: ReturnType<typeof render>) =>
  wrapper.get(".record-block__caption").text().replace(/\s+/g, " ");

describe("the page of a guest's ticket", () => {
  beforeEach(() => {
    route.params = { token: TOKEN };
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("shows the ticket of the token, and the place of its answer", async () => {
    const track = vi
      .spyOn(ticketApi, "trackTicket")
      .mockResolvedValue({ data: { resource: tracked }, error: null } as never);
    const wrapper = render();
    await flushPromises();

    expect(track).toHaveBeenCalledWith(TOKEN);
    expect(wrapper.getComponent(TicketThread).props("ticket")).toEqual(tracked);
    expect(wrapper.getComponent(TicketConversation).props("ticket")).toEqual(
      tracked,
    );
    const lead = wrapper.get(".lead-text");
    expect(lead.text().replace(/\s+/g, " ")).toBe(
      `Ответ появится на этой странице: ${window.location.origin}/support/track/${TOKEN}. Сохраните ссылку, чтобы вернуться к обращению.`,
    );
    const link = lead.getComponent(RouterLinkStub);
    expect(link.props("to")).toEqual({
      name: "support-track",
      params: { token: TOKEN },
    });
    expect(link.text()).toBe(
      `${window.location.origin}/support/track/${TOKEN}`,
    );
  });

  // An ordinary link is all the guest needs to keep: the token is not said a
  // second time in grey, and nothing on the page copies.
  it("offers no button that copies and no grey line with the token", async () => {
    serve();
    const wrapper = render();
    await flushPromises();

    expect(wrapper.text()).not.toMatch(/Скопировать|номер обращения/i);
    expect(wrapper.findAll("button").map((button) => button.text())).toEqual([
      "Отправить",
    ]);
    expect(wrapper.find(".secondary-text").exists()).toBe(false);
  });

  it("signs the guest Гость in the caption and on his answers", async () => {
    serve({ ...tracked, comments: [asked, guestReply] });
    const wrapper = render();
    await flushPromises();

    expect(caption(wrapper)).toContain("Автор: Гость,");
    const guest = wrapper.findAll(".comment")[1].get(".author-info");
    expect(guest.text().replace(/\s+/g, " ")).toBe("Автор: Гость");
    expect(guest.find("a").exists()).toBe(false);
  });

  // The guest is not signed in: nothing marks the ticket as his to answer.
  it("marks nothing with the star", async () => {
    serve();
    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".star").exists()).toBe(false);
  });

  it("writes the guest's answer by the token and hands the turn to moderation", async () => {
    const track = serve();
    const create = vi
      .spyOn(ticketApi, "createTrackedComment")
      .mockResolvedValue({
        data: { resource: guestReply },
        error: null,
      } as never);
    const asMember = vi.spyOn(ticketApi, "createComment");
    const wrapper = render();
    await flushPromises();

    await wrapper.get(".editor-stub").setValue(guestReply.text);
    await wrapper
      .findAll(".ticket-answer-form button")
      .find((button) => button.text() === "Отправить")!
      .trigger("click");
    await flushPromises();

    expect(create).toHaveBeenCalledWith(TOKEN, guestReply.text);
    expect(asMember).not.toHaveBeenCalled();
    expect(wrapper.findAll(".comment-text").map((t) => t.text())).toEqual([
      asked.text,
      guestReply.text,
    ]);
    expect(caption(wrapper)).toContain("Ответы (2)");
    expect(caption(wrapper)).toContain("Ожидает ответа модерации");
    // One reading: the answer the server answered with is put in place.
    expect(track).toHaveBeenCalledTimes(1);
  });

  it("keeps no draft under the token", async () => {
    serve();
    const wrapper = render();
    await flushPromises();

    const key = wrapper.getComponent(TicketConversation).props("draftKey");
    expect(key).toBe("");
    expect(JSON.stringify(localStorage)).not.toContain(TOKEN);
  });

  it("offers no form on a closed ticket and says who closed it", async () => {
    serve({
      ...tracked,
      status: "Closed",
      canComment: false,
      resolvedUtc: "2026-10-06T10:00:00Z",
      resolvedByUsername: "SolohinLex",
    });
    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".ticket-answer-form").exists()).toBe(false);
    expect(wrapper.get(".refusal-note").text()).toContain(
      "закрыл(а) SolohinLex.",
    );
  });

  it("finds nothing by a token that matches nothing", async () => {
    vi.spyOn(ticketApi, "trackTicket").mockResolvedValue({
      data: null,
      error: { status: 404 },
    } as never);
    const wrapper = render();
    await flushPromises();

    expect(wrapper.text()).toContain(NOT_FOUND);
    expect(wrapper.findComponent(TicketThread).exists()).toBe(false);
    expect(wrapper.findComponent(TicketConversation).exists()).toBe(false);
    // Nothing to keep on a page that found nothing.
    expect(wrapper.find(".lead-text").exists()).toBe(false);
  });

  it("opens no ticket by its number in place of the token", async () => {
    const number = "6de5652f-1749-a4b8-1a72-e2e16f69dd89";
    route.params = { token: number };
    const track = vi.spyOn(ticketApi, "trackTicket").mockResolvedValue({
      data: null,
      error: { status: 404 },
    } as never);
    const byNumber = vi.spyOn(ticketApi, "getTicket");
    const wrapper = render();
    await flushPromises();

    expect(track).toHaveBeenCalledWith(number);
    expect(byNumber).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain(NOT_FOUND);
  });

  it("offers to try again when the ticket did not load", async () => {
    vi.spyOn(ticketApi, "trackTicket").mockResolvedValue({
      data: null,
      error: { status: 500 },
    } as never);
    const wrapper = render();
    await flushPromises();

    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить обращение",
    );
    expect(wrapper.text()).not.toContain(NOT_FOUND);
  });
});
