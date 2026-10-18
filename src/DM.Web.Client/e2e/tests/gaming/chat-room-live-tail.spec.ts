import type { APIRequestContext } from "@playwright/test";
import { test, expect } from "../../fixtures/auth";
import { AT_BOTTOM_THRESHOLD_PX } from "../../../src/shared/lib/utils/liveTail";

/**
 * A message arriving in a game chat room must not drag a reader who has
 * scrolled up back down to the tail.
 *
 * The unit tests hold this rule already, and they hold it over numbers written
 * by hand: jsdom lays nothing out, so `scrollHeight`, `clientHeight` and
 * `scrollTop` are whatever the spec defines them to be, and all three are zero
 * otherwise. Which leaves three things only a browser can answer, and this test
 * is here for exactly those:
 *
 *  - that the message window is a scrolling element at all. A container that
 *    grows with its content instead of scrolling reports "at the bottom"
 *    forever, and the rule passes every unit test while doing nothing.
 *  - that the threshold below the tail is a real distance in real pixels. The
 *    numbers a browser reports are fractional and a scroll lands where the
 *    layout puts it, not where the test wishes.
 *  - that appending rows underneath the reader does not move him by itself.
 *    That is the browser's own behaviour, and the page's guard is worth
 *    nothing if the platform moves him anyway.
 *
 * Headless by construction: it drives the preview build through the standard
 * fixtures and never asks for a window.
 */

const API_URL = process.env.VITE_API_URL || "http://localhost:5000";

/**
 * How long the page may take to notice a message that is already on the
 * server. A chat room has no push of its own: it re-reads its newest page on
 * the site-wide live-tail cadence (LIVE_TAIL_POLL_INTERVAL_MS, thirty seconds
 * at the time of writing), so an arrival is due within one interval of being
 * written, and the rest is the request itself. The wait below is a poll, not a
 * sleep: it ends the moment the row appears.
 *
 * The interval is spelled here instead of imported because that constant lives
 * in the API client module, which pulls axios, the interceptors and the whole
 * alias graph into a test loader that resolves none of it. The threshold above
 * IS imported: its module is a leaf, and a copied threshold is the exact
 * mistake its own comment was written about.
 */
const TAIL_ARRIVAL_TIMEOUT_MS = 45_000;

/** Opening a game room is a whole page, and the run may not have it to itself. */
const PAGE_LOAD_TIMEOUT_MS = 30_000;

/** Both arrivals wait for a tick of their own, and the page has to load first. */
const TEST_TIMEOUT_MS = 180_000;

/** A chat room the account reads and writes in, resolved against the stand. */
interface ChatRoomToWriteIn {
  gameId: string;
  roomId: string;
  roomNumber: number;
}

/**
 * Finds a game with a chat room the signed-in account may read and write in.
 *
 * The test writes into the room (say, below), so reading is not enough. An open
 * chat room is read by everybody who sees the game, and written in only by the
 * leads and by whoever the room lets write (RoomIntentionResolver.SendMessage),
 * so the room is taken in a game the account leads: the master and the
 * assistants write in every chat room of their game, open or closed, whatever
 * the seed granted.
 *
 * Resolved rather than written down: which game the account leads depends on
 * the account the run is signed in as and on what the stand was seeded with.
 * An id pinned in a spec file is an id that stops existing at the next reset.
 */
async function resolveChatRoomToWriteIn(
  api: APIRequestContext,
): Promise<ChatRoomToWriteIn> {
  const gamesResponse = await api.get(`${API_URL}/v1/games?take=50`);
  expect(
    gamesResponse.ok(),
    `GET /v1/games answered ${gamesResponse.status()}`,
  ).toBeTruthy();
  const { resources: games } = await gamesResponse.json();

  for (const game of games ?? []) {
    // Only a game the viewer leads: Authority is the master or an assistant
    // (GameParticipation of the API). A player or a reader of a game may read
    // its open chat room and still be refused the message this test writes,
    // and asking the rooms of every one of fifty games is fifty requests spent.
    if (!(game.participation ?? []).includes("Authority")) continue;

    const roomsResponse = await api.get(`${API_URL}/v1/games/${game.id}/rooms`);
    if (!roomsResponse.ok()) continue;
    const { resources: rooms } = await roomsResponse.json();

    for (const room of rooms ?? []) {
      if (room.type !== "Chat") continue;
      const probe = await api.get(
        `${API_URL}/v1/chat-rooms/${room.id}/messages?limit=50`,
      );
      if (!probe.ok()) continue;
      return {
        gameId: game.id,
        roomId: room.id,
        roomNumber: room.roomNumber,
      };
    }
  }

  throw new Error(
    "No game on this stand that the signed-in account leads has a chat room that answers its messages. " +
      "Is the database seeded (powershell -File .\\scripts\\dm.ps1 seed)?",
  );
}

/** Writes one message into the room, the way another player would. */
async function say(
  api: APIRequestContext,
  roomId: string,
  text: string,
): Promise<string> {
  const response = await api.post(
    `${API_URL}/v1/chat-rooms/${roomId}/messages`,
    { headers: { "Content-Type": "application/json" }, data: { text } },
  );
  // An unmet precondition is a failure, not a reason to stand down: a test
  // that cannot put a message in the room proves nothing about what the room
  // does when one arrives.
  expect(
    response.status(),
    `POST of a room message answered ${response.status()}`,
  ).toBe(201);
  const { resource } = await response.json();
  return resource.id;
}

test.describe("the live tail of a game chat room", () => {
  test("leaves a reader who has scrolled up where he is standing", async ({
    authenticatedPage: page,
    authContext: api,
  }) => {
    test.setTimeout(TEST_TIMEOUT_MS);

    const room = await resolveChatRoomToWriteIn(api);
    // A short window over a tall room is what the rule is about, and the
    // window is `calc(100vh - 360px)`: the height is pinned so the run does
    // not depend on the browser the project happens to be.
    await page.setViewportSize({ width: 1280, height: 640 });
    await page.goto(`/game/${room.gameId}/chat-rooms/${room.roomNumber}`);

    const messages = page.locator(".chat-messages");
    // The room opening is a precondition, not an assertion, and it is a whole
    // page: the shell, the game's room list and then the messages. The default
    // five seconds is a measurement of how loaded the machine is - three
    // browser projects against one preview server ran past it - so the wait is
    // given room. It is a poll either way and ends when the room is on screen.
    await expect(messages.locator(".chat-message-row").first()).toBeVisible({
      timeout: PAGE_LOAD_TIMEOUT_MS,
    });

    const metrics = () =>
      messages.evaluate((el) => ({
        scrollTop: el.scrollTop,
        scrollHeight: el.scrollHeight,
        clientHeight: el.clientHeight,
      }));
    const scrollTo = (top: number) =>
      messages.evaluate((el, value) => {
        el.scrollTop = value;
      }, top);
    const distanceFromTail = (m: {
      scrollTop: number;
      scrollHeight: number;
      clientHeight: number;
    }) => m.scrollHeight - m.scrollTop - m.clientHeight;

    const opened = await metrics();
    // The window really does scroll, and by more than the threshold is wide.
    // A container that grows with its content answers every question in this
    // test with "at the bottom" and proves nothing.
    expect(
      opened.scrollHeight - opened.clientHeight,
      "the message window is not scrollable on this stand: too little in the room",
    ).toBeGreaterThan(AT_BOTTOM_THRESHOLD_PX * 4);

    // The reader scrolls up into what was said before. Far enough that the
    // page cannot mistake him for someone standing at the tail, and the
    // distance is read back off the browser rather than assumed: this is the
    // threshold meeting real numbers.
    const parkedTop =
      opened.scrollHeight - opened.clientHeight - AT_BOTTOM_THRESHOLD_PX * 4;
    await scrollTo(parkedTop);
    const parked = await metrics();
    expect(distanceFromTail(parked)).toBeGreaterThan(AT_BOTTOM_THRESHOLD_PX);

    const runId = Date.now();
    const arrivedId = await say(
      api,
      room.roomId,
      `Проверка живого хвоста ${runId}, читатель отлистал вверх`,
    );
    await expect(page.locator(`[id="msg-${arrivedId}"]`)).toHaveCount(1, {
      timeout: TAIL_ARRIVAL_TIMEOUT_MS,
    });

    const afterArrival = await metrics();
    // The row went in underneath him: the window grew.
    expect(afterArrival.scrollHeight).toBeGreaterThan(parked.scrollHeight);
    // And he did not move. Not "roughly did not move": appending below the
    // fold moves nothing on its own, so the only thing that could have moved
    // him is the page deciding to follow the tail down over his head.
    expect(afterArrival.scrollTop).toBe(parked.scrollTop);

    // The other half of the same rule, without which the first is satisfied
    // by a page that never follows anything: a reader standing at the tail is
    // reading the tail, and is taken to what arrives.
    await scrollTo(afterArrival.scrollHeight);
    const atTail = await metrics();
    expect(distanceFromTail(atTail)).toBeLessThan(AT_BOTTOM_THRESHOLD_PX);

    const followedId = await say(
      api,
      room.roomId,
      `Проверка живого хвоста ${runId}, читатель у хвоста`,
    );
    await expect(page.locator(`[id="msg-${followedId}"]`)).toHaveCount(1, {
      timeout: TAIL_ARRIVAL_TIMEOUT_MS,
    });

    const followed = await metrics();
    expect(followed.scrollHeight).toBeGreaterThan(atTail.scrollHeight);
    expect(followed.scrollTop).toBeGreaterThan(atTail.scrollTop);
    expect(distanceFromTail(followed)).toBeLessThan(AT_BOTTOM_THRESHOLD_PX);
  });
});
