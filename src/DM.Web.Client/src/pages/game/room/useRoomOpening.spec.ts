/**
 * @vitest-environment jsdom
 */

// The post a room opens on is brought up a frame after its page is drawn, and
// the room can be off the page before the frame comes. The rest of the opening
// is GameRoom.opening.spec.ts's.
import { it, expect, vi, afterEach } from "vitest";
import { defineComponent, h, ref } from "vue";
import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { setScrollContainer } from "@/shared/lib/scroll";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useRoomOpening } from "./useRoomOpening";

afterEach(() => {
  setScrollContainer(null);
  vi.restoreAllMocks();
  document.body.innerHTML = "";
});

it("brings nothing up once the room is gone before the frame it waits for", async () => {
  const pinia = createPinia();
  setActivePinia(pinia);
  // The post the address names stands on the first page, the one read.
  const store = useGameDetailsStore();
  vi.spyOn(store, "locatePostPage").mockResolvedValue(1);
  vi.spyOn(store, "loadPostsByRoomNumber").mockResolvedValue(undefined);
  const container = document.createElement("div");
  const scrollTo = vi.fn();
  container.scrollTo = scrollTo;
  setScrollContainer(container);
  // The frame the post waits for comes when the test lets it.
  const frames: FrameRequestCallback[] = [];
  vi.spyOn(window, "requestAnimationFrame").mockImplementation((frame) =>
    frames.push(frame),
  );
  const Room = defineComponent({
    setup() {
      useRoomOpening({
        gameId: ref("g-1"),
        roomNum: ref(1),
        page: ref(1),
        apiFilter: ref({}),
        isFilteredView: ref(false),
        ensureRooms: () => Promise.resolve(),
        roomRefused: ref(false),
      });
      return () => h("article", { id: "post-p-3" });
    },
  });
  const { wrapper } = await mountPage(Room, "/game/g-1/rooms/1#post-p-3", {
    routes: [["game-room", "/game/:id/rooms/:num"]],
    pinia,
    attachTo: document.body,
  });
  await flushPromises();
  expect(frames).toHaveLength(1);

  wrapper.unmount();
  // A post of another page under the same id.
  const stranger = document.createElement("article");
  stranger.id = "post-p-3";
  document.body.appendChild(stranger);
  frames[0](0);
  await flushPromises();

  expect(scrollTo).not.toHaveBeenCalled();
});
