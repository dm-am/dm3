/**
 * @vitest-environment jsdom
 */

// The store is real and only the transport faked: what the control offers depends on its cache.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { setActivePinia, createPinia } from "pinia";
import type { GlobalChatEvent } from "@/entities/global-chat";
import type { User } from "@/shared/api/models/common";
import { createMockUser } from "@/entities/forum/model/store.fixtures";

const { api } = vi.hoisted(() => ({
  api: {
    getEvents: vi.fn(),
    getEvent: vi.fn(),
    joinEvent: vi.fn(),
    leaveEvent: vi.fn(),
    startEvent: vi.fn(),
    endEvent: vi.fn(),
  },
}));

vi.mock("@/entities/global-chat/api/globalChatApi", () => ({ default: api }));

import { useGlobalChatEventsStore } from "@/entities/global-chat";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import ChatEventActions from "./ChatEventActions.vue";

const EVENT_ID = "e1";

const person = (username: string): User =>
  createMockUser(`u-${username}`, username);

const event = (over: Partial<GlobalChatEvent> = {}): GlobalChatEvent => ({
  id: EVENT_ID,
  title: "Вечер быстрых зарисовок",
  description: "",
  startsUtc: "2026-08-04T19:00:00Z",
  duration: "03:00:00",
  isOpen: true,
  status: "Scheduled",
  createdBy: person("SolohinLex"),
  participants: [],
  ...over,
});

const member = (username: string, isOrganizer = false) => ({
  id: `p-${username}`,
  user: person(username),
  isOrganizer,
  joinedUtc: "2026-08-01T10:00:00Z",
});

const answered = (details: GlobalChatEvent) => ({
  data: { resource: details },
  error: null,
});

function stand(username: string | null, details: GlobalChatEvent | null) {
  useAuthStore().user = username ? person(username) : null;
  if (details) useGlobalChatEventsStore().eventDetails[EVENT_ID] = details;
}

const render = () => mount(ChatEventActions, { props: { eventId: EVENT_ID } });

const control = (wrapper: ReturnType<typeof render>) =>
  wrapper.find("button.event-action");

const toast = useToast();

const said = (): string[] => toast.toasts.value.map((t) => t.message);

beforeEach(() => {
  setActivePinia(createPinia());
  vi.clearAllMocks();
  localStorage.clear();
  // The toast list is module state shared by every test in the run.
  for (const item of [...toast.toasts.value]) toast.dismiss(item.id);
  api.getEvents.mockResolvedValue({ data: { resources: [] }, error: null });
});

describe("what the control offers", () => {
  it("offers a guest nothing at all", () => {
    stand(null, event());

    expect(render().find(".event-actions").exists()).toBe(false);
  });

  it("offers nothing until the details are read", () => {
    stand("Astrellan", null);

    expect(render().find(".event-actions").exists()).toBe(false);
  });

  it("offers a signed-in reader a place in an open event", () => {
    stand("Astrellan", event());

    expect(control(render()).text()).toBe("Участвовать");
  });

  it("offers a participant the way out", () => {
    stand("Astrellan", event({ participants: [member("Astrellan")] }));

    expect(control(render()).text()).toBe("Отменить участие");
  });

  it("offers the organizer the start of a scheduled event", () => {
    stand("SolohinLex", event({ participants: [member("SolohinLex", true)] }));

    expect(control(render()).text()).toBe("Начать");
  });

  it("offers the organizer the end of a live one", () => {
    stand(
      "SolohinLex",
      event({ status: "Live", participants: [member("SolohinLex", true)] }),
    );

    expect(control(render()).text()).toBe("Закончить");
  });

  it("does not offer a closed event, and says why", () => {
    stand("Astrellan", event({ isOpen: false }));

    const wrapper = render();
    expect(control(wrapper).exists()).toBe(false);
    expect(wrapper.find(".event-note").text()).toBe(
      "Участников закрытого эвента приглашает организатор",
    );
  });

  it("leaves the organizer of a closed event their own action", () => {
    stand(
      "SolohinLex",
      event({ isOpen: false, participants: [member("SolohinLex", true)] }),
    );

    expect(control(render()).text()).toBe("Начать");
  });
});

// Success is silent: each pair checks that the plate is gone and the control reads the other word.
describe("what the control does", () => {
  it("signs up, and the offer flips instead of a plate", async () => {
    stand("Astrellan", event());
    const joined = event({ participants: [member("Astrellan")] });
    api.joinEvent.mockResolvedValue(answered(joined));

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(api.joinEvent).toHaveBeenCalledWith(EVENT_ID);
    expect(control(wrapper).text()).toBe("Отменить участие");
    expect(said()).toEqual([]);
  });

  it("leaves, and the offer flips back instead of a plate", async () => {
    stand("Astrellan", event({ participants: [member("Astrellan")] }));
    api.leaveEvent.mockResolvedValue(answered(event({ participants: [] })));

    const wrapper = render();
    expect(control(wrapper).text()).toBe("Отменить участие");
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(api.leaveEvent).toHaveBeenCalledWith(EVENT_ID);
    expect(control(wrapper).text()).toBe("Участвовать");
    expect(said()).toEqual([]);
  });

  it("replaces the control with its reason on a closed event", async () => {
    const closed = event({
      isOpen: false,
      participants: [member("Astrellan")],
    });
    stand("Astrellan", closed);
    api.leaveEvent.mockResolvedValue(
      answered(event({ isOpen: false, participants: [] })),
    );

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(control(wrapper).exists()).toBe(false);
    expect(wrapper.find(".event-note").text()).toBe(
      "Участников закрытого эвента приглашает организатор",
    );
    expect(said()).toEqual([]);
  });

  it("starts it, and the offer becomes its end, not a plate", async () => {
    stand("SolohinLex", event({ participants: [member("SolohinLex", true)] }));
    api.startEvent.mockResolvedValue(
      answered(
        event({ status: "Live", participants: [member("SolohinLex", true)] }),
      ),
    );

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(api.startEvent).toHaveBeenCalledWith(EVENT_ID);
    expect(control(wrapper).text()).toBe("Закончить");
    expect(said()).toEqual([]);
    // The strip prints a status that lives only in the summary list: "Скоро" becomes "Идет".
    expect(api.getEvents).toHaveBeenCalledTimes(1);
  });

  it("ends it, and the control goes away, not a plate", async () => {
    stand(
      "SolohinLex",
      event({ status: "Live", participants: [member("SolohinLex", true)] }),
    );
    api.endEvent.mockResolvedValue(
      answered(
        event({ status: "Ended", participants: [member("SolohinLex", true)] }),
      ),
    );

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(api.endEvent).toHaveBeenCalledWith(EVENT_ID);
    expect(wrapper.find(".event-actions").exists()).toBe(false);
    expect(said()).toEqual([]);
    expect(api.getEvents).toHaveBeenCalledTimes(1);
  });

  it("blocks the control while the request is out", async () => {
    stand("Astrellan", event());
    let settle: (value: unknown) => void = () => {};
    api.joinEvent.mockReturnValue(
      new Promise((resolve) => {
        settle = resolve;
      }),
    );

    const wrapper = render();
    await control(wrapper).trigger("click");

    expect(control(wrapper).attributes("disabled")).toBeDefined();
    await control(wrapper).trigger("click");
    expect(api.joinEvent).toHaveBeenCalledTimes(1);

    settle(answered(event()));
    await flushPromises();
    expect(control(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("reads the refusal out instead of falling silent", async () => {
    stand("SolohinLex", event({ participants: [member("SolohinLex", true)] }));
    api.startEvent.mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Уже идет другой эвент",
        status: 409,
        traceId: "t",
      },
    });

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.get(".event-refusal").text()).toBe("Уже идет другой эвент");
    expect(said()).toEqual([]);
    expect(control(wrapper).text()).toBe("Начать");
    expect(control(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("falls back to its own sentence when the server sent none", async () => {
    stand("Astrellan", event());
    api.joinEvent.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.get(".event-refusal").text()).toBe(
      "Не удалось записаться на эвент",
    );
    expect(said()).toEqual([]);
  });
});
