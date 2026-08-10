/**
 * @vitest-environment jsdom
 */

/**
 * The room row is where "the game has this room" and "you may open it" come
 * apart. The menu names every room, closed ones included, so a room the
 * viewer has no access to is a name and a grey lock and nothing else — not a
 * link, because the page behind it answers 404 to exactly the viewers who
 * see it locked.
 *
 * Both halves used to be unreachable. The listing dropped closed rooms
 * altogether, so the grey lock had no state to be in and the master saw a
 * grey one on the room he owns; the title was a router-link with no other
 * branch. The star had a failure of its own: it read `pendings`, a field no
 * endpoint has ever sent, so no room ever showed a turn waiting.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import GameRoomLink from "./GameRoomLink.vue";
import { useAuthStore } from "@/entities/user";
import {
  RoomAccessType,
  RoomType,
  type PostPendency,
  type Room,
} from "@/entities/game";

config.global.stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: {
    template: '<span class="tooltip"><slot /></span>',
    props: ["text"],
  },
};

const room = (overrides: Partial<Room> = {}): Room =>
  ({
    id: "r-1",
    roomNumber: 1,
    title: "Таверна",
    type: RoomType.Default,
    access: RoomAccessType.Open,
    unreadPostsCount: 0,
    ...overrides,
  }) as unknown as Room;

const pendency = (overrides: Partial<PostPendency> = {}): PostPendency =>
  ({
    id: "p-1",
    roomId: "r-1",
    characterId: "c-1",
    characterName: "Гоблин",
    createdUtc: "2026-07-01T10:00:00Z",
    waitingFor: { username: "player" },
    ...overrides,
  }) as unknown as PostPendency;

const row = (target: Room, prefix?: string) =>
  mount(GameRoomLink, {
    props:
      prefix === undefined
        ? { room: target, gamePublicId: "abcde" }
        : { room: target, gamePublicId: "abcde", prefix },
  });

/** What the row copies as: a non-breaking space is still a space. */
const copied = (wrapper: ReturnType<typeof row>) =>
  wrapper.text().replace(/\u00a0/g, " ");

function signedInAs(username: string) {
  const auth = useAuthStore();
  auth.user = { username } as unknown as NonNullable<typeof auth.user>;
}

describe("GameRoomLink", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("links to a room the viewer may open", () => {
    const wrapper = row(room());
    expect(wrapper.find(".router-link").exists()).toBe(true);
    expect(wrapper.find(".router-link").text()).toBe("Таверна");
  });

  it("renders a room the viewer may not open as text, not a link", () => {
    const wrapper = row(
      room({ access: RoomAccessType.Private, canView: false }),
    );
    expect(wrapper.find(".router-link").exists()).toBe(false);
    expect(wrapper.find(".title").element.tagName).toBe("SPAN");
    expect(copied(wrapper)).toBe("- Таверна (0)");
  });

  it("locks a private room and colours the lock by access", () => {
    // The lock is rendered for every room and hidden on the public ones, so
    // that the titles of a list start at one x. Asserted through the class
    // rather than through presence: dropping the element is what left the
    // column ragged.
    const publicRoom = row(room());
    expect(publicRoom.find(".lock").exists()).toBe(true);
    expect(publicRoom.find(".lock").classes()).toContain("public");

    const closed = row(
      room({ access: RoomAccessType.Private, canView: false }),
    );
    expect(closed.find(".lock").classes()).not.toContain("public");
    expect(closed.find(".lock").classes()).not.toContain("granted");

    const open = row(room({ access: RoomAccessType.Private, canView: true }));
    expect(open.find(".lock").classes()).toContain("granted");
    expect(open.find(".lock").classes()).not.toContain("public");
  });

  it("counts unread posts, zero included", () => {
    const quiet = row(room());
    expect(quiet.find(".counter").text()).toBe("(0)");

    const busy = row(room({ unreadPostsCount: 5 }));
    expect(busy.find(".counter").text()).toBe("(5)");
  });

  it("copies as one human line, nested or not", () => {
    expect(copied(row(room({ unreadPostsCount: 5 })))).toBe("- Таверна (5)");
    expect(copied(row(room({ unreadPostsCount: 5 }), ""))).toBe("Таверна (5)");
  });

  it("stars a room that waits for the viewer, rightmost in the row", () => {
    signedInAs("player");
    const wrapper = row(room({ pendencies: [pendency()] }));
    expect(wrapper.text()).toContain("★");
    expect(wrapper.element.lastElementChild?.textContent).toBe("★");
  });

  it("ignores a fulfilled pendency and one that waits for somebody else", () => {
    signedInAs("player");
    const fulfilled = row(
      room({
        pendencies: [pendency({ fulfilledUtc: "2026-07-02T10:00:00Z" })],
      }),
    );
    expect(fulfilled.text()).not.toContain("★");

    const someoneElse = row(
      room({
        pendencies: [
          pendency({
            waitingFor: { username: "other" } as PostPendency["waitingFor"],
          }),
        ],
      }),
    );
    expect(someoneElse.text()).not.toContain("★");
  });
});
