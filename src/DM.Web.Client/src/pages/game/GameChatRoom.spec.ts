/**
 * @vitest-environment jsdom
 */

// The frame of a chat room of a game and the name of the room in the heading.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { defineComponent, h, type Ref } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import type { Message } from "@/shared/api/models/common";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import {
  provideZoneMarker,
  provideZoneSection,
} from "@/shared/lib/composables/useZoneSection";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { ErrorState } from "@/shared/ui/ErrorState";
import GameChatRoom from "./GameChatRoom.vue";
import { mockGetChatMessages } from "./GameChatRoom.doubles";
import {
  NOTHING,
  message,
  openRoomAs,
  keep,
  page,
  standInRoomOne,
  under,
  unmountRoom,
} from "./GameChatRoom.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./GameChatRoom.doubles")).roomApi(),
);
vi.mock("vue-router", async () =>
  (await import("./GameChatRoom.doubles")).vueRouter(),
);

describe("GameChatRoom frame", () => {
  beforeEach(standInRoomOne);

  afterEach(unmountRoom);

  // A skeleton of another height would make the feed jump when the rows came.
  it.each([
    ["full", "ChatMessageSkeleton", "CommentSkeleton"],
    ["compact", "CommentSkeleton", "ChatMessageSkeleton"],
  ] as const)(
    "waits for the messages of the %s look in the skeleton of its rows",
    async (layout, shown, notShown) => {
      localStorage.setItem("dm_message_layout", layout);
      try {
        mockGetChatMessages.mockReturnValue(new Promise(() => {}));
        const wrapper = await openRoomAs();

        expect(wrapper.findComponent({ name: shown }).exists()).toBe(true);
        expect(wrapper.findComponent({ name: notShown }).exists()).toBe(false);
      } finally {
        localStorage.removeItem("dm_message_layout");
      }
    },
  );

  // Off the stylesheet: jsdom lays nothing out.
  it("sets the rows of the skeleton where the rows of the feed stand", () => {
    const rules = styleRules(
      componentCss(
        resolve(dirname(fileURLToPath(import.meta.url)), "GameChatRoom.vue"),
      ),
    );
    const padding = (selector: string) =>
      rules.find((rule) => rule.selector === selector)?.declarations.padding;

    expect(padding(".chat-skeleton :deep(.skeleton-message)")).toBe(
      padding(".chat-message-row"),
    );
  });

  // Under a plaque the height of a feed is an empty gap.
  it.each([
    [
      "a guest of a closed room",
      { signedIn: false, access: "Private" },
      null,
      false,
    ],
    ["a viewer the server turns away", { access: "Private" }, 404, false],
    ["a guest of an open room", { signedIn: false }, null, true],
    ["a reader of the room", {}, null, true],
  ] as const)(
    "keeps the height of a feed for %s only where there is one",
    async (_who, as, refusal, feed) => {
      if (refusal)
        mockGetChatMessages.mockResolvedValue({
          data: null,
          error: { status: refusal },
        });
      const wrapper = await openRoomAs(as);

      expect(wrapper.find(".chat-messages").exists()).toBe(feed);
      expect(wrapper.get(".chat-room").classes().includes("with-feed")).toBe(
        feed,
      );
    },
  );

  it("draws that height on a room with a feed, and on no other", () => {
    const rules = styleRules(
      componentCss(
        resolve(dirname(fileURLToPath(import.meta.url)), "GameChatRoom.vue"),
      ),
    );
    const on = (selector: string) =>
      rules
        .filter((rule) => rule.selector === selector)
        .map((rule) => rule.declarations["min-height"])
        .filter(Boolean);

    expect(on(".chat-room")).toEqual([]);
    expect(on(".chat-room.with-feed")).toEqual(["200px"]);
  });

  it("parts the days of the feed with the site's dashed line", async () => {
    const nextDay = { ...message("m-2"), createdUtc: "2026-07-02T10:00:00Z" };
    mockGetChatMessages.mockResolvedValue(
      page(message("m-1"), nextDay as unknown as Message),
    );
    const wrapper = await openRoomAs();

    const days = wrapper
      .findAllComponents(DashSeparator)
      .map((line) => line.props("label"));
    expect(days).toHaveLength(2);
    expect(new Set(days).size).toBe(2);
    expect(wrapper.findAll(".chat-message-row")).toHaveLength(2);
  });

  it("says a failed load in the site's error box, with a retry", async () => {
    mockGetChatMessages.mockResolvedValue({
      data: null,
      error: { status: 500 },
    });
    const wrapper = await openRoomAs();

    const box = wrapper.getComponent(ErrorState);
    expect(box.props("message")).toBe("Не удалось загрузить сообщения");

    mockGetChatMessages.mockResolvedValue(page(message("m-1")));
    await box.get(".error-retry").trigger("click");
    await flushPromises();

    expect(wrapper.findComponent(ErrorState).exists()).toBe(false);
    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
  });

  it("draws no line of who writes under a load that failed", async () => {
    mockGetChatMessages.mockResolvedValue({
      data: null,
      error: { status: 500 },
    });
    const wrapper = await openRoomAs();

    expect(wrapper.find(".chat-error").text()).toContain(
      "Не удалось загрузить сообщения",
    );
    expect(under(wrapper)).toEqual(NOTHING);
  });
});

describe("GameChatRoom heading", () => {
  beforeEach(standInRoomOne);

  afterEach(unmountRoom);

  it("names the room once, in the zone's heading, with its archive marker", async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    useGameDetailsStore().rooms = [
      { id: "r-1", roomNumber: 1, title: "Таверна", isArchived: true },
    ] as never;
    useAuthStore().user = { username: "player" } as never;

    let section!: Ref<string | undefined>;
    let marker!: Ref<string | undefined>;
    const Shell = defineComponent({
      setup() {
        section = provideZoneSection();
        marker = provideZoneMarker();
        return () => h(GameChatRoom);
      },
    });
    const room = keep(
      mount(Shell, {
        global: {
          plugins: [pinia],
          stubs: { RouterLink: true, BBCodeEditor: true, ChatMessage: true },
        },
      }),
    );
    await flushPromises();

    expect(section.value).toBe("Таверна");
    expect(marker.value).toBe("архив");
    expect(room.text()).not.toContain("Таверна");
  });
});
