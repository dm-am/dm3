/**
 * @vitest-environment node
 */

/**
 * One refusal, one carrier.
 *
 * The form has two lines of its own — one per field — and the response
 * interceptor has a plate. For a refused action, a rate limit, a dead server, a
 * dead connection or an expired session the plate speaks and the form keeps
 * quiet. The 500 is the one this was written for: the form printed "Ошибка
 * сервера" into its own line while the plate above it said the same words, and
 * the two only looked like one sentence because both read the same constant.
 *
 * What the form still answers for is what was typed into it: an unknown author,
 * a participant who already has a testimonial, a text outside the length the
 * server takes.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";

const { createTestimonial } = vi.hoisted(() => ({
  createTestimonial: vi.fn(),
}));

vi.mock("@/entities/testimonial", () => ({
  useTestimonialStore: () => ({ createTestimonial }),
}));

import { useCreateTestimonial } from "./useCreateTestimonial";

/** An open form, filled in well enough to reach the server. */
async function submitted() {
  const form = useCreateTestimonial();
  form.toggleForm();
  form.authorUsername.value = "Solohin";
  form.testimonialText.value = "Сайт живет и это главное";
  await form.submitTestimonial();
  return form;
}

describe("useCreateTestimonial", () => {
  beforeEach(() => createTestimonial.mockReset());

  // The statuses the response interceptor used to plate stand in the form's
  // line now, and the plate is taken down (refusalHere; the owner's rule of
  // 26 September 2026). No plate is raised in these tests - the interceptor
  // lives in the axios instance and the store is mocked away.
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])("says a failure of status %i in the form", async (status, line) => {
    createTestimonial.mockResolvedValue({
      data: null,
      error: { status, title: "Ошибка сервера" },
    });

    const form = await submitted();

    expect(form.errorMessage.value).toBe(line);
    expect(form.formExpanded.value).toBe(true);
    expect(form.testimonialText.value).toBe("Сайт живет и это главное");
  });

  // An expired session and a dead connection are about the reader and keep
  // the plate; the form says nothing about them.
  it.each([0, 401])(
    "leaves a failure of status %i to the interceptor",
    async (status) => {
      createTestimonial.mockResolvedValue({
        data: null,
        error: { status, title: "Ошибка сервера" },
      });

      const form = await submitted();

      expect(form.errorMessage.value).toBe("");
      expect(form.authorError.value).toBe("");
      // Silent about the failure, not about the attempt: nothing was posted, so
      // the form stays open with the name and the text still in it.
      expect(form.formExpanded.value).toBe(true);
      expect(form.testimonialText.value).toBe("Сайт живет и это главное");
    },
  );

  it("names the author when the participant already has a testimonial", async () => {
    createTestimonial.mockResolvedValue({
      data: null,
      error: { status: 409, title: "Conflict" },
    });

    const form = await submitted();

    expect(form.authorError.value).toBe("У этого участника уже есть отзыв");
    expect(form.errorMessage.value).toBe("");
  });

  it("names the author when the server has no such participant", async () => {
    createTestimonial.mockResolvedValue({
      data: null,
      error: { status: 404, title: "Пользователь не найден" },
    });

    const form = await submitted();

    expect(form.authorError.value).toBe("Пользователь не найден");
  });

  it("keeps its own sentence about the length the server takes", async () => {
    createTestimonial.mockResolvedValue({
      data: null,
      error: { status: 400, title: "Validation failed" },
    });

    const form = await submitted();

    expect(form.errorMessage.value).toBe(
      "Некорректные данные (10-1000 символов)",
    );
  });

  it("clears the form when the testimonial was posted", async () => {
    createTestimonial.mockResolvedValue({ data: { id: "t-1" }, error: null });

    const form = await submitted();

    expect(form.errorMessage.value).toBe("");
    expect(form.testimonialText.value).toBe("");
    expect(form.formExpanded.value).toBe(false);
  });
});
