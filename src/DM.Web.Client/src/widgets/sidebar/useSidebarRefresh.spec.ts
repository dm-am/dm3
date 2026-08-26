/**
 * @vitest-environment jsdom
 */

/**
 * The three moments a sidebar list refetches, in one place now.
 *
 * Five blocks had written them out one by one, and the middle one is the one
 * that gets dropped when a sixth block is copied from a fifth: without the
 * forced refetch on a change of viewer, the unread counters on the rows stay
 * the previous reader's for the rest of the session.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { defineComponent, nextTick } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useRoute } from "vue-router";
import { useAuthStore } from "@/shared/stores";
import { useSidebarRefresh } from "./useSidebarRefresh";
import type { User } from "@/shared/api/models/common/user";

// The block reads only fullPath, and it has to be reactive for the navigation
// half of the composable to be observable at all.
vi.mock("vue-router", async () => {
  const { reactive } = await vi.importActual<typeof import("vue")>("vue");
  const route = reactive({ fullPath: "/" });
  return { useRoute: () => route };
});

const viewer = (username: string) => ({ id: username, username }) as User;

function mountBlock(fetch: (force?: boolean) => void) {
  return mount(
    defineComponent({
      setup() {
        useSidebarRefresh(fetch);
        return () => null;
      },
    }),
  );
}

describe("useSidebarRefresh", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
    useRoute().fullPath = "/";
  });

  it("reads the list once on mount, unforced", () => {
    const fetch = vi.fn();
    mountBlock(fetch);

    expect(fetch.mock.calls).toEqual([[]]);
  });

  it("forces a refetch when the viewer changes", async () => {
    const fetch = vi.fn();
    mountBlock(fetch);
    fetch.mockClear();

    useAuthStore().updateUser(viewer("b"));
    await nextTick();

    expect(fetch.mock.calls).toEqual([[true]]);
  });

  it("asks again on navigation, unforced", async () => {
    const fetch = vi.fn();
    mountBlock(fetch);
    fetch.mockClear();

    useRoute().fullPath = "/games";
    await nextTick();

    expect(fetch.mock.calls).toEqual([[]]);
  });
});
