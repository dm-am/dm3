/**
 * The harness of the blocks that show a rated post (the best of the week, the
 * latest rated, a profile's best): the requests held until the test answers
 * them, and the block mounted with its post, skeleton and empty text stubbed.
 */
import { expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { Component } from "vue";
import postApi from "../api/postApi";

type Answer =
  | { data: { resources: { id: string }[] }; error: null }
  | { data: null; error: { status: number } };

/**
 * Each request waits until the test answers it, by the sort it asked for.
 * `answer(sortBy, "fail")` refuses it, `answer(sortBy, ids)` lists those posts.
 */
export function holdRatedPosts() {
  const pending = new Map<string, (answer: Answer) => void>();
  const requests = vi
    .spyOn(postApi, "getRatedPosts")
    .mockImplementation(
      (params) =>
        new Promise<Answer>((resolve) =>
          pending.set(params?.sortBy ?? "", resolve),
        ) as never,
    );
  const answer = (sortBy: string, ids: string[] | "fail") =>
    pending.get(sortBy)!(
      ids === "fail"
        ? { data: null, error: { status: 500 } }
        : { data: { resources: ids.map((id) => ({ id })) }, error: null },
    );
  return { requests, answer };
}

/** The block on a fresh store; its retry button is real, so a click reaches the retry. */
export function mountRatedPostBlock(
  block: Component,
  props: Record<string, unknown> = {},
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  return mount(block, {
    shallow: true,
    props,
    global: {
      plugins: [pinia],
      stubs: {
        GamePost: {
          props: ["post"],
          template: `<article class="post-stub">{{ post.id }}</article>`,
        },
        GamePostSkeleton: { template: `<div class="skeleton-stub" />` },
        SecondaryText: { template: `<p class="empty-stub"><slot /></p>` },
        ErrorState: false,
      },
    },
  });
}

/**
 * The retry after a refusal shows the skeleton, not the empty text, until it
 * answers: the refusal said nothing about posts. `refuse` fails the block's
 * first load, `answerEmpty` answers the retry with no posts.
 */
export async function expectSkeletonDuringRetry(
  wrapper: ReturnType<typeof mountRatedPostBlock>,
  refuse: () => void,
  answerEmpty: () => void,
) {
  await flushPromises();
  refuse();
  await flushPromises();

  await wrapper.get(".error-retry").trigger("click");
  await flushPromises();

  expect(wrapper.find(".skeleton-stub").exists()).toBe(true);
  expect(wrapper.find(".empty-stub").exists()).toBe(false);

  answerEmpty();
  await flushPromises();

  expect(wrapper.find(".empty-stub").exists()).toBe(true);
}
