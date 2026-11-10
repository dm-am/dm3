/**
 * @vitest-environment jsdom
 */

// The guest's ticket page: the answer reaches him only here. The address
// carries the token and nothing else, and a ticket number in its place opens
// nothing. His answer is appended without reading the ticket again, and no
// draft is kept under the token.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import {
  ticketApi,
  type TicketComment,
  type TrackedTicket,
} from "@/entities/ticket";
import { TicketConversation, TicketThread } from "@/widgets/ticket-thread";
import TicketTrackPage from "./TicketTrackPage.vue";

enableAutoUnmount(afterEach);

const TOKEN = "k7Qm2xV9pLr4Tz8N";
const NOT_FOUND = "Обращение по этой ссылке не найдено";

// A heavy tiptap component; the page owes the editor the text and the draft key.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeAnswer(),
);

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
  createdUtc: "2026-10-05T18:20:00Z",
  resolvedUtc: null,
  resolvedByUsername: null,
  comments: [asked],
  canComment: true,
};

/** The page, and every address it and the conversation link to. */
const routes: Address[] = [
  ["support-track", "/support/track"],
  ["profile", "/users/:username"],
];

/** The address of the letter the guest follows; the token rides in the fragment. */
let hash = "";

const render = async () =>
  (
    await mountPage(TicketTrackPage, `/support/track${hash}`, {
      routes,
      stubs: { PageTitle: true },
    })
  ).wrapper;

const serve = (ticket: TrackedTicket = tracked) =>
  vi
    .spyOn(ticketApi, "trackTicket")
    .mockResolvedValue({ data: { resource: ticket }, error: null } as never);

/** The caption of the block, its spaces as a reader reads them. */
const caption = (wrapper: Awaited<ReturnType<typeof render>>) =>
  wrapper.get(".record-block__caption").text().replace(/\s+/g, " ");

describe("the page of a guest's ticket", () => {
  beforeEach(() => {
    hash = `#token=${TOKEN}`;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("shows the ticket of the token, and the place of its answer", async () => {
    const track = vi
      .spyOn(ticketApi, "trackTicket")
      .mockResolvedValue({ data: { resource: tracked }, error: null } as never);
    const wrapper = await render();
    await flushPromises();

    expect(track).toHaveBeenCalledWith(TOKEN);
    expect(wrapper.getComponent(TicketThread).props("ticket")).toEqual(tracked);
    expect(wrapper.getComponent(TicketConversation).props("ticket")).toEqual(
      tracked,
    );
    const lead = wrapper.get(".lead-text");
    expect(lead.text().replace(/\s+/g, " ")).toBe(
      `Ответ появится на этой странице: ${window.location.origin}/support/track#token=${TOKEN}. Сохраните ссылку, чтобы вернуться к обращению.`,
    );
    const link = lead.get("a");
    expect(link.attributes("href")).toBe(`/support/track#token=${TOKEN}`);
    expect(link.text()).toBe(
      `${window.location.origin}/support/track#token=${TOKEN}`,
    );
  });

  it("signs the guest Гость in the caption and on his answers", async () => {
    serve({ ...tracked, comments: [asked, guestReply] });
    const wrapper = await render();
    await flushPromises();

    expect(caption(wrapper)).toContain("Автор: Гость,");
    const guest = wrapper.findAll(".comment")[1].get(".author-info");
    expect(guest.text().replace(/\s+/g, " ")).toBe("Автор: Гость");
    expect(guest.find("a").exists()).toBe(false);
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
    const wrapper = await render();
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
    const wrapper = await render();
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
    const wrapper = await render();
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
    const wrapper = await render();
    await flushPromises();

    expect(wrapper.text()).toContain(NOT_FOUND);
    expect(wrapper.findComponent(TicketThread).exists()).toBe(false);
    expect(wrapper.findComponent(TicketConversation).exists()).toBe(false);
    // Nothing to keep on a page that found nothing.
    expect(wrapper.find(".lead-text").exists()).toBe(false);
  });

  it("opens no ticket by its number in place of the token", async () => {
    const number = "6de5652f-1749-a4b8-1a72-e2e16f69dd89";
    hash = `#token=${number}`;
    const track = vi.spyOn(ticketApi, "trackTicket").mockResolvedValue({
      data: null,
      error: { status: 404 },
    } as never);
    const byNumber = vi.spyOn(ticketApi, "getTicket");
    const wrapper = await render();
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
    const wrapper = await render();
    await flushPromises();

    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить обращение",
    );
    expect(wrapper.text()).not.toContain(NOT_FOUND);
  });
});
