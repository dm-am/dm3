/**
 * @vitest-environment node
 */

/**
 * The store had no test and its own cache.
 *
 * The cache was a single slot keyed by `JSON.stringify(query)`. Two things
 * followed from that and neither was visible from the file: the key was field
 * ORDER, so `{ number, take }` and `{ take, number }` were two different
 * searches of the same page and each missed the other's entry; and one slot
 * meant that paging away and back always went to the network, which is the one
 * thing a sixty-second cache exists to prevent.
 *
 * Both are properties of the bookkeeping, and the bookkeeping is shared
 * (`shared/lib/utils/keyedCache`). What this file holds is the behaviour that
 * change is worth: the same search is one entry however it was spelt, a second
 * search does not evict the first, and a mutation drops every page rather than
 * only the one on screen.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import type { WebsiteTestimonial } from "@/shared/api/models/community";

const {
  mockGetTestimonials,
  mockDeleteTestimonial,
  mockCreateTestimonial,
  mockUpdateTestimonial,
} = vi.hoisted(() => ({
  mockGetTestimonials: vi.fn(),
  mockDeleteTestimonial: vi.fn(),
  mockCreateTestimonial: vi.fn(),
  mockUpdateTestimonial: vi.fn(),
}));

vi.mock("../api", () => ({
  testimonialApi: {
    getTestimonials: mockGetTestimonials,
    deleteTestimonial: mockDeleteTestimonial,
    createTestimonial: mockCreateTestimonial,
    updateTestimonial: mockUpdateTestimonial,
  },
}));

import { useTestimonialStore } from "./store";

const entry = (id: string) =>
  ({ id, text: `Отзыв ${id}` }) as unknown as WebsiteTestimonial;

const page = (...ids: string[]) => ({
  data: { resources: ids.map(entry), paging: null },
  error: null,
});

describe("useTestimonialStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  it("reads the same search once, however the query was spelt", async () => {
    mockGetTestimonials.mockResolvedValue(page("t-1"));
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1, take: 10 });
    await store.fetchTestimonials({ take: 10, number: 1 });

    expect(mockGetTestimonials).toHaveBeenCalledTimes(1);
  });

  it("treats an absent filter and a cleared one as one search", async () => {
    mockGetTestimonials.mockResolvedValue(page("t-1"));
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.fetchTestimonials({ number: 1, take: undefined });

    expect(mockGetTestimonials).toHaveBeenCalledTimes(1);
  });

  it("keeps a page it has already read when another is asked for", async () => {
    mockGetTestimonials
      .mockResolvedValueOnce(page("t-1"))
      .mockResolvedValueOnce(page("t-2"));
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.fetchTestimonials({ number: 2 });
    await store.fetchTestimonials({ number: 1 });

    expect(mockGetTestimonials).toHaveBeenCalledTimes(2);
    expect(store.testimonials?.resources.map((r) => r.id)).toEqual(["t-1"]);
  });

  it("asks again when the caller forces it", async () => {
    mockGetTestimonials.mockResolvedValue(page("t-1"));
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.fetchTestimonials({ number: 1 }, true);

    expect(mockGetTestimonials).toHaveBeenCalledTimes(2);
  });

  it("does not cache a refusal", async () => {
    mockGetTestimonials
      .mockResolvedValueOnce({ data: null, error: { status: 500 } })
      .mockResolvedValueOnce(page("t-1"));
    const store = useTestimonialStore();

    expect(await store.fetchTestimonials({ number: 1 })).toBe(false);
    expect(store.error).toBeTruthy();

    expect(await store.fetchTestimonials({ number: 1 })).toBe(true);
    expect(store.error).toBeNull();
  });

  it("drops every cached page when one is removed", async () => {
    mockGetTestimonials
      .mockResolvedValueOnce(page("t-1", "t-2"))
      .mockResolvedValueOnce(page("t-3"))
      .mockResolvedValueOnce(page("t-2"));
    mockDeleteTestimonial.mockResolvedValue({ error: null });
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.fetchTestimonials({ number: 2 });
    await store.fetchTestimonials({ number: 1 });
    await store.removeTestimonial("t-1" as never);

    // Page two still holds the list as it was before the removal, so serving
    // it from the cache would show the reader what they just deleted.
    await store.fetchTestimonials({ number: 2 });

    expect(mockGetTestimonials).toHaveBeenCalledTimes(3);
  });

  it("drops every cached page when one is added", async () => {
    // A fresh envelope per call: the store unshifts into the one it holds, and
    // a mock handing back the same object would be asserting on that mutation
    // rather than on what the second read returned.
    mockGetTestimonials.mockImplementation(() =>
      Promise.resolve(page("t-1", "t-9")),
    );
    mockCreateTestimonial.mockResolvedValue({
      data: entry("t-9"),
      error: null,
    });
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.createTestimonial("Solohin", "Спасибо за сайт");
    await store.fetchTestimonials({ number: 1 });

    expect(mockGetTestimonials).toHaveBeenCalledTimes(2);
    expect(store.testimonials?.resources.map((r) => r.id)).toEqual([
      "t-1",
      "t-9",
    ]);
  });

  /**
   * A testimonial is posted on behalf of a participant the moderator names, so
   * the name has to reach the request. It used to be absent from the whole
   * call, and the server signed the entry with whoever was logged in.
   */
  it("signs a new testimonial with the participant it was told about", async () => {
    mockGetTestimonials.mockResolvedValue(page("t-1"));
    mockCreateTestimonial.mockResolvedValue({
      data: { resource: entry("t-9") },
      error: null,
    });
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.createTestimonial("Solohin", "Спасибо за сайт");

    expect(mockCreateTestimonial).toHaveBeenCalledWith({
      authorUsername: "Solohin",
      text: "Спасибо за сайт",
    });
    // The endpoint answers with an envelope; the head of the list must be the
    // testimonial, not the envelope around it.
    expect(store.testimonials?.resources[0]).toEqual(entry("t-9"));
  });

  it("puts the saved text in place of the row it edited", async () => {
    mockGetTestimonials.mockResolvedValue(page("t-1", "t-2"));
    mockUpdateTestimonial.mockResolvedValue({
      data: {
        resource: { ...entry("t-2"), text: "Переписанный отзыв" },
      },
      error: null,
    });
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.updateTestimonial("t-2" as never, "Переписанный отзыв");

    expect(mockUpdateTestimonial).toHaveBeenCalledWith("t-2", {
      text: "Переписанный отзыв",
    });
    expect(store.testimonials?.resources.map((r) => r.text)).toEqual([
      "Отзыв t-1",
      "Переписанный отзыв",
    ]);
  });

  it("drops every cached page when one is edited", async () => {
    mockGetTestimonials
      .mockResolvedValueOnce(page("t-1"))
      .mockResolvedValueOnce(page("t-2"))
      .mockResolvedValueOnce(page("t-2"));
    mockUpdateTestimonial.mockResolvedValue({
      data: { resource: entry("t-1") },
      error: null,
    });
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    await store.fetchTestimonials({ number: 2 });
    await store.updateTestimonial("t-1" as never, "Переписанный отзыв");

    // Page two was read before the edit; served from the cache it would show
    // the text that no longer exists.
    await store.fetchTestimonials({ number: 2 });

    expect(mockGetTestimonials).toHaveBeenCalledTimes(3);
  });

  it("keeps the row untouched when the server refuses the edit", async () => {
    mockGetTestimonials.mockResolvedValue(page("t-1"));
    mockUpdateTestimonial.mockResolvedValue({
      data: null,
      error: { status: 403, title: "Недостаточно прав" },
    });
    const store = useTestimonialStore();

    await store.fetchTestimonials({ number: 1 });
    const { error } = await store.updateTestimonial(
      "t-1" as never,
      "Переписанный отзыв",
    );

    expect(error).not.toBeNull();
    expect(store.testimonials?.resources[0].text).toBe("Отзыв t-1");
  });
});
