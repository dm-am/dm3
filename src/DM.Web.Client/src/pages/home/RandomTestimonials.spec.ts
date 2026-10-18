/**
 * @vitest-environment jsdom
 */

/**
 * The testimonials of the home page: one at a time, a random one first, the
 * next every ten seconds, held while the reader is on it, a further page
 * fetched at the end of the list, and the words for a list that failed or
 * holds nothing.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  flushPromises,
  mount,
  type DOMWrapper,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { ref } from "vue";
import { testimonialApi, useTestimonialStore } from "@/entities/testimonial";
import RandomTestimonials from "./RandomTestimonials.vue";

const expanded = ref(false);

const stubs = {
  TestimonialCard: {
    props: ["testimonial"],
    setup: () => ({ isExpanded: expanded, measureContent: () => {} }),
    template: "<p class='card'>{{ testimonial.id }}</p>",
  },
  TestimonialSkeleton: { template: "<p class='skeleton' />" },
};

const testimonial = (id: string) => ({ id, text: `Отзыв ${id}` });

const page = (ids: string[], total = ids.length) => ({
  resources: ids.map(testimonial),
  paging: { total },
});

let wrapper: VueWrapper | null = null;

async function gallery(
  first: ReturnType<typeof page> | null,
  { ok = true, random = 0 } = {},
) {
  vi.spyOn(Math, "random").mockReturnValue(random);
  const store = useTestimonialStore();
  vi.spyOn(store, "fetchTestimonials").mockImplementation(async () => {
    store.testimonials = first as never;
    return ok;
  });
  wrapper = mount(RandomTestimonials, { global: { stubs } });
  await flushPromises();
  return wrapper;
}

const shown = () => wrapper!.find(".card").text();

async function tenSeconds() {
  await vi.advanceTimersByTimeAsync(10_000);
}

function setHidden(hidden: boolean) {
  Object.defineProperty(document, "hidden", {
    value: hidden,
    configurable: true,
  });
  document.dispatchEvent(new Event("visibilitychange"));
}

beforeEach(() => {
  vi.useFakeTimers();
  setActivePinia(createPinia());
  expanded.value = false;
});

afterEach(() => {
  wrapper?.unmount();
  wrapper = null;
  setHidden(false);
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("RandomTestimonials: the turn of the testimonials", () => {
  it("starts on a random one and turns to the next every ten seconds, round to the first", async () => {
    await gallery(page(["a", "b", "c"]), { random: 0.5 });
    expect(shown()).toBe("b");

    await tenSeconds();
    expect(shown()).toBe("c");
    await tenSeconds();
    expect(shown()).toBe("a");
  });

  it.each([
    [
      "the pointer is over it",
      (w: Pick<DOMWrapper<Element>, "trigger">) => w.trigger("mouseenter"),
    ],
    [
      "the keyboard is in it",
      (w: Pick<DOMWrapper<Element>, "trigger">) => w.trigger("focusin"),
    ],
    [
      "it is opened whole",
      () => {
        expanded.value = true;
      },
    ],
  ])("holds while %s", async (_what, hold) => {
    const on = await gallery(page(["a", "b"]));

    await hold(on.get(".testimonials-gallery"));
    await tenSeconds();

    expect(shown()).toBe("a");
  });

  it("turns again once the pointer leaves", async () => {
    const on = await gallery(page(["a", "b"]));
    await on.get(".testimonials-gallery").trigger("mouseenter");
    await tenSeconds();

    await on.get(".testimonials-gallery").trigger("mouseleave");
    await tenSeconds();

    expect(shown()).toBe("b");
  });

  it("stands still while the tab is hidden, and waits a whole turn on return", async () => {
    await gallery(page(["a", "b"]));
    await vi.advanceTimersByTimeAsync(6_000);

    setHidden(true);
    await tenSeconds();
    expect(shown()).toBe("a");

    setHidden(false);
    await vi.advanceTimersByTimeAsync(6_000);
    expect(shown()).toBe("a");
    await vi.advanceTimersByTimeAsync(4_000);
    expect(shown()).toBe("b");
  });
});

describe("RandomTestimonials: the pages after the first", () => {
  it("fetches the next page at the end of the list and goes on into it", async () => {
    const more = vi
      .spyOn(testimonialApi, "getTestimonials")
      .mockResolvedValue({ data: page(["k"], 11), error: null } as never);
    const ten = "abcdefghij".split("");
    await gallery(page(ten, 11), { random: 0.95 });
    expect(shown()).toBe("j");

    await tenSeconds();

    expect(more).toHaveBeenCalledWith({ take: 10, number: 2 });
    expect(shown()).toBe("k");
  });

  it("goes back to the first when the next page fails", async () => {
    vi.spyOn(testimonialApi, "getTestimonials").mockResolvedValue({
      data: null,
      error: { status: 500, title: "" },
    } as never);
    await gallery(page(["a", "b"], 5), { random: 0.6 });
    expect(shown()).toBe("b");

    await tenSeconds();

    expect(shown()).toBe("a");
  });
});

describe("RandomTestimonials: no testimonials to show", () => {
  it("holds the place of one while they load", () => {
    vi.spyOn(useTestimonialStore(), "fetchTestimonials").mockReturnValue(
      new Promise(() => {}) as never,
    );
    wrapper = mount(RandomTestimonials, { global: { stubs } });

    expect(wrapper.find(".skeleton").exists()).toBe(true);
  });

  it("says there are none yet", async () => {
    const on = await gallery(page([]));

    expect(on.text()).toBe("Отзывов пока нет");
  });

  it("says the load failed, and loads again on a retry", async () => {
    const on = await gallery(null, { ok: false });
    expect(on.text()).toContain("Не удалось загрузить отзывы");

    await on.get(".error-retry").trigger("click");
    await flushPromises();

    expect(useTestimonialStore().fetchTestimonials).toHaveBeenCalledTimes(2);
  });
});
