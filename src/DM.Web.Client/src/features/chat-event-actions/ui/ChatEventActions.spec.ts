/**
 * @vitest-environment jsdom
 */

/**
 * The whole point of this control is that it offers exactly what the API would
 * accept and nothing else. The client had all nine event endpoints and not one
 * caller, so the first version of this file is also the first time any of the
 * four is executed anywhere but on the server.
 *
 * Every state the strip can put a reader in is here, because the states are the
 * feature: an offer to join shown to somebody the API would refuse is worse
 * than no offer at all, and a refusal answered with silence leaves a reader
 * believing they are signed up.
 *
 * The store is real and only the transport is faked: what the control offers
 * depends on the participants list, which the store caches, and a mocked store
 * would assert the mock rather than the wiring.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { setActivePinia, createPinia } from "pinia";
import type { GlobalChatEvent } from "@/entities/global-chat";
import type { User } from "@/shared/api/models/common";

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

import { useGlobalChatStore } from "@/entities/global-chat";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import ChatEventActions from "./ChatEventActions.vue";

const EVENT_ID = "e1";

const person = (username: string) =>
  ({ id: `u-${username}`, username, role: "RegularUser" }) as unknown as User;

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

/** Sign the viewer in (or, with null, leave them a guest) and cache details. */
function stand(username: string | null, details: GlobalChatEvent | null) {
  useAuthStore().user = username ? person(username) : null;
  if (details) useGlobalChatStore().eventDetails[EVENT_ID] = details;
}

const render = () => mount(ChatEventActions, { props: { eventId: EVENT_ID } });

const control = (wrapper: ReturnType<typeof render>) =>
  wrapper.find("button.event-action");

const toast = useToast();

/** What is on screen in toasts right now, in the order it arrived. */
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

  /**
   * The label is decided by the participants list, and the list arrives with
   * the card's own request. Guessing "Участвовать" while it is in the air
   * shows the wrong word to everyone already signed up.
   */
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

  /**
   * A closed event takes its participants from the organizer, and the API
   * answers a self-join with a 403. So the control is not drawn, and the line
   * that replaces it says why rather than leaving a blank.
   */
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

describe("what the control does", () => {
  it("joins through the endpoint and says it went through", async () => {
    stand("Astrellan", event());
    const joined = event({ participants: [member("Astrellan")] });
    api.joinEvent.mockResolvedValue(answered(joined));

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(api.joinEvent).toHaveBeenCalledWith(EVENT_ID);
    expect(said()).toContain("Вы записаны на эвент");
    // The answer is what the cache now holds, so the same card immediately
    // offers the opposite action.
    expect(control(wrapper).text()).toBe("Отменить участие");
  });

  it("re-reads the events list, because the strip is drawn from it", async () => {
    stand("SolohinLex", event({ participants: [member("SolohinLex", true)] }));
    api.startEvent.mockResolvedValue(
      answered(
        event({ status: "Live", participants: [member("SolohinLex", true)] }),
      ),
    );

    await control(render()).trigger("click");
    await flushPromises();

    expect(api.startEvent).toHaveBeenCalledWith(EVENT_ID);
    expect(api.getEvents).toHaveBeenCalledTimes(1);
  });

  it("ends a live event through the endpoint", async () => {
    stand(
      "SolohinLex",
      event({ status: "Live", participants: [member("SolohinLex", true)] }),
    );
    api.endEvent.mockResolvedValue(answered(event({ status: "Ended" })));

    await control(render()).trigger("click");
    await flushPromises();

    expect(api.endEvent).toHaveBeenCalledWith(EVENT_ID);
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
    // A second click while the first is in the air must not reach the wire.
    await control(wrapper).trigger("click");
    expect(api.joinEvent).toHaveBeenCalledTimes(1);

    settle(answered(event()));
    await flushPromises();
    expect(control(wrapper).attributes("disabled")).toBeUndefined();
  });

  /**
   * A refusal used to be the one outcome nobody saw: `Api` resolves on a 409
   * exactly as it resolves on a 200, so a swallowed error reads as success.
   * The sentence is the server's own — the conflict it names is the reason.
   */
  it("reads the refusal out instead of falling silent", async () => {
    stand("SolohinLex", event({ participants: [member("SolohinLex", true)] }));
    api.startEvent.mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Сейчас уже идет другой эвент",
        status: 409,
        traceId: "t",
      },
    });

    const wrapper = render();
    await control(wrapper).trigger("click");
    await flushPromises();

    expect(said()).toEqual(["Сейчас уже идет другой эвент"]);
    // Refused, so nothing moved: the same offer stands and is clickable again.
    expect(control(wrapper).text()).toBe("Начать");
    expect(control(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("falls back to its own sentence when the server sent none", async () => {
    stand("Astrellan", event());
    api.joinEvent.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });

    await control(render()).trigger("click");
    await flushPromises();

    expect(said()).toEqual(["Не удалось записаться на эвент"]);
  });
});
