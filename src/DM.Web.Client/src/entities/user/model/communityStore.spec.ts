/**
 * The community store: the users list it searches, the page it fetches ahead,
 * and the profile it loads. What the list reads off it at each moment of a
 * search: the rows, the loading flag and the refusal line.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { userApi } from "../api";
import { useCommunityStore, type UsersSearchParams } from "./communityStore";
import type { Username } from "./types";

const page = (...names: string[]) => ({
  resources: names.map((username) => ({ id: username, username })),
  paging: { total: names.length },
});

/** A request that answers when the test says so. */
function heldRequest() {
  const answers: Array<(value: unknown) => void> = [];
  const request = vi
    .spyOn(userApi, "getUsers")
    .mockImplementation(
      () => new Promise((resolve) => answers.push(resolve)) as never,
    );
  return {
    request,
    answer: (at: number, value: unknown) => answers[at](value),
  };
}

const ok = (data: unknown) => ({ data, error: null });
const refused = { data: null, error: { status: 500, title: "Ошибка" } };

let store: ReturnType<typeof useCommunityStore>;

beforeEach(() => {
  setActivePinia(createPinia());
  store = useCommunityStore();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe("communityStore: what the search asks the server", () => {
  it.each<[string, UsersSearchParams, object]>([
    ["nothing but the first page of twenty", {}, { take: 20 }],
    [
      "a later page as the rows to skip",
      { number: 3, size: 15 },
      { take: 15, skip: 30 },
    ],
    [
      "every filter by its own name",
      {
        search: "анна",
        activity: "inactive",
        isOnline: true,
        role: "Mentor",
        isNewbie: false,
        minRating: 0,
        maxRating: 90,
        minGamesHosting: 1,
        maxGamesHosting: 5,
        minGamesPlaying: 2,
        maxGamesPlaying: 6,
        minBlogsHosting: 0,
        maxBlogsHosting: 3,
        registeredFromUtc: "2020-01-01T00:00:00Z",
        registeredToUtc: "2021-01-01T23:59:59Z",
        sortBy: "username",
        sortOrder: "asc",
      },
      {
        take: 20,
        search: "анна",
        activity: "Inactive",
        isOnline: true,
        role: "Mentor",
        isNewbie: false,
        minRating: 0,
        maxRating: 90,
        minGamesHosting: 1,
        maxGamesHosting: 5,
        minGamesPlaying: 2,
        maxGamesPlaying: 6,
        minBlogsHosting: 0,
        maxBlogsHosting: 3,
        registeredFromUtc: "2020-01-01T00:00:00Z",
        registeredToUtc: "2021-01-01T23:59:59Z",
        sortBy: "Name",
        sortOrder: "asc",
      },
    ],
    [
      "every role, every activity and the offline as nothing to filter by",
      { role: "all", activity: "all", isOnline: false },
      { take: 20, activity: "All" },
    ],
    [
      "a sort it does not know as no sort",
      { sortBy: "shoeSize", sortOrder: "desc" },
      { take: 20, sortOrder: "desc" },
    ],
  ])("asks for %s", async (_what, params, asked) => {
    const request = vi
      .spyOn(userApi, "getUsers")
      .mockResolvedValue(ok(page("anna")) as never);

    await store.searchUsers(params);

    expect(request).toHaveBeenCalledWith(asked);
  });

  it.each([
    ["lastActivity", "LastActivity"],
    ["rating", "Rating"],
    ["registered", "Registered"],
    ["gamesHosting", "GamesHosting"],
    ["gamesPlaying", "GamesPlaying"],
    ["popularity", "Popularity"],
    ["blogsHosting", "BlogsHosting"],
  ])("names the sort %s %s", async (sortBy, named) => {
    const request = vi
      .spyOn(userApi, "getUsers")
      .mockResolvedValue(ok(page()) as never);

    await store.searchUsers({ sortBy });

    expect(request.mock.calls[0][0]).toMatchObject({ sortBy: named });
  });
});

describe("communityStore: what the list reads during a search", () => {
  it("is loading while the request is out, then holds the rows", async () => {
    const { answer } = heldRequest();

    const search = store.searchUsers({ search: "a" });
    expect(store.searchLoading).toBe(true);
    expect(store.searchResult).toBeNull();

    answer(0, ok(page("anna")));
    await search;
    expect(store.searchLoading).toBe(false);
    expect(store.searchResult).toEqual(page("anna"));
    expect(store.searchError).toBeNull();
  });

  it("answers the same search again from memory for thirty seconds", async () => {
    vi.useFakeTimers();
    const request = vi
      .spyOn(userApi, "getUsers")
      .mockResolvedValue(ok(page("anna")) as never);
    await store.searchUsers({ search: "a", number: 1 });

    vi.advanceTimersByTime(30_000);
    await store.searchUsers({ number: 1, search: "a" });

    expect(request).toHaveBeenCalledOnce();
    expect(store.searchLoading).toBe(false);
  });

  it("shows the old rows of a search while it asks again after thirty seconds", async () => {
    vi.useFakeTimers();
    vi.spyOn(userApi, "getUsers")
      .mockResolvedValueOnce(ok(page("anna")) as never)
      .mockResolvedValueOnce(ok(page("vera")) as never);
    await store.searchUsers({ search: "a" });
    await store.searchUsers({ search: "v" });
    vi.advanceTimersByTime(30_001);
    const { answer } = heldRequest();

    const search = store.searchUsers({ search: "a" });
    expect(store.searchResult).toEqual(page("anna"));
    expect(store.searchLoading).toBe(true);

    answer(0, ok(page("anna", "boris")));
    await search;
    expect(store.searchResult).toEqual(page("anna", "boris"));
  });

  it("keeps the rows it has and says why when the server refuses", async () => {
    vi.spyOn(userApi, "getUsers")
      .mockResolvedValueOnce(ok(page("anna")) as never)
      .mockResolvedValueOnce(refused as never);
    await store.searchUsers({ search: "a" });

    await store.searchUsers({ search: "b" });

    expect(store.searchResult).toEqual(page("anna"));
    expect(store.searchLoading).toBe(false);
    expect(store.searchError).toBe("Не удалось загрузить пользователей");
  });

  it("takes the refusal away with the next search, an answer from memory included", async () => {
    vi.spyOn(userApi, "getUsers")
      .mockResolvedValueOnce(ok(page("anna")) as never)
      .mockResolvedValueOnce(refused as never);
    await store.searchUsers({ search: "a" });
    await store.searchUsers({ search: "b" });

    await store.searchUsers({ search: "a" });

    expect(store.searchError).toBeNull();
    expect(store.searchResult).toEqual(page("anna"));
  });

  it("drops the answer to a search the reader has already replaced", async () => {
    const { answer } = heldRequest();
    const first = store.searchUsers({ search: "a" });
    const second = store.searchUsers({ search: "b" });

    answer(1, ok(page("boris")));
    await second;
    answer(0, ok(page("anna")));
    await first;

    expect(store.searchResult).toEqual(page("boris"));
    expect(store.searchLoading).toBe(false);
  });

  // The order the other way round: the older answer comes in while the newer
  // search is still out. The flag, the rows and the line belong to the newer
  // one, so the older answer moves none of them, a refusal included.
  it.each([
    ["answered", ok(page("anna"))],
    ["refused", refused],
  ])(
    "holds the newer search against an older answer %s while it is out",
    async (_how, older) => {
      const { answer } = heldRequest();
      const first = store.searchUsers({ search: "a" });
      const second = store.searchUsers({ search: "b" });

      answer(0, older);
      await first;
      expect(store.searchLoading).toBe(true);
      expect(store.searchResult).toBeNull();
      expect(store.searchError).toBeNull();

      answer(1, ok(page("boris")));
      await second;
      expect(store.searchLoading).toBe(false);
      expect(store.searchResult).toEqual(page("boris"));
    },
  );

  it("asks the server again once the memory is cleared", async () => {
    const request = vi
      .spyOn(userApi, "getUsers")
      .mockResolvedValue(ok(page("anna")) as never);
    await store.searchUsers({ search: "a" });

    store.clearSearchCache();
    await store.searchUsers({ search: "a" });

    expect(request).toHaveBeenCalledTimes(2);
  });
});

describe("communityStore: the page fetched ahead", () => {
  it("does nothing before a search", async () => {
    const request = vi.spyOn(userApi, "getUsers");

    await store.prefetchPage(2);

    expect(request).not.toHaveBeenCalled();
  });

  it("fetches another page of the last search, and the search then takes it from memory", async () => {
    const request = vi
      .spyOn(userApi, "getUsers")
      .mockResolvedValueOnce(ok(page("anna")) as never)
      .mockResolvedValueOnce(ok(page("boris")) as never);
    await store.searchUsers({ search: "a", size: 10 });

    await store.prefetchPage(2);
    expect(request).toHaveBeenLastCalledWith({
      take: 10,
      skip: 10,
      search: "a",
    });

    await store.searchUsers({ search: "a", size: 10, number: 2 });
    expect(request).toHaveBeenCalledTimes(2);
    expect(store.searchResult).toEqual(page("boris"));
  });

  it("does not fetch a page it holds fresh", async () => {
    const request = vi
      .spyOn(userApi, "getUsers")
      .mockResolvedValue(ok(page("anna")) as never);
    await store.searchUsers({ search: "a", number: 2 });

    await store.prefetchPage(2);

    expect(request).toHaveBeenCalledOnce();
  });

  it("leaves the list alone, the refusal line too, whatever the answer", async () => {
    vi.spyOn(userApi, "getUsers")
      .mockResolvedValueOnce(ok(page("anna")) as never)
      .mockResolvedValueOnce(refused as never);
    await store.searchUsers({ search: "a" });

    await store.prefetchPage(2);

    expect(store.searchResult).toEqual(page("anna"));
    expect(store.searchError).toBeNull();
  });
});

describe("communityStore: the profile", () => {
  const profile = { id: "u1", username: "anna", picture: { small: "s.png" } };

  it("loads the profile out of its envelope", async () => {
    vi.spyOn(userApi, "getUserProfile").mockResolvedValue(
      ok({ resource: profile }) as never,
    );

    const refusal = await store.trySelectProfile("anna" as Username);

    expect(refusal).toBeNull();
    expect(store.selectedUser).toEqual(profile);
    expect(store.loadingProfile).toBe(false);
  });

  it("hands the refusal back and draws no profile", async () => {
    vi.spyOn(userApi, "getUserProfile").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Нет такого" },
    } as never);

    const refusal = await store.trySelectProfile("ghost" as Username);

    expect(refusal).toEqual({ status: 404, title: "Нет такого" });
    expect(store.selectedUser).toBeNull();
  });

  it("drops the answer for a profile the reader has left", async () => {
    const answers: Array<(value: unknown) => void> = [];
    vi.spyOn(userApi, "getUserProfile").mockImplementation(
      () => new Promise((resolve) => answers.push(resolve)) as never,
    );
    const left = store.trySelectProfile("anna" as Username);
    const current = store.trySelectProfile("boris" as Username);

    answers[1](ok({ resource: { ...profile, id: "u2", username: "boris" } }));
    await current;
    answers[0](ok({ resource: profile }));

    expect(await left).toBeNull();
    expect(store.selectedUser?.username).toBe("boris");
  });

  it("repaints the picture of the profile on screen and of no other", async () => {
    vi.spyOn(userApi, "getUserProfile").mockResolvedValue(
      ok({ resource: profile }) as never,
    );
    await store.trySelectProfile("anna" as Username);

    store.applySelectedUserPicture("u2", { small: "other.png" } as never);
    expect(store.selectedUser?.picture).toEqual({ small: "s.png" });

    store.applySelectedUserPicture("u1", { small: "new.png" } as never);
    expect(store.selectedUser?.picture).toEqual({ small: "new.png" });
  });
});
