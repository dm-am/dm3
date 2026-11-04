/**
 * @vitest-environment node
 */

import { describe, it, expect, vi, beforeEach } from "vitest";

const { createTestimonial } = vi.hoisted(() => ({
  createTestimonial: vi.fn(),
}));

vi.mock("@/entities/testimonial", () => ({
  useTestimonialStore: () => ({ createTestimonial }),
}));

import { useCreateTestimonial } from "./useCreateTestimonial";

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
      // Nothing was posted, so the form stays open with the name and the text.
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

    expect(form.authorError.value).toBe("У этого пользователя уже есть отзыв");
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

  it("says a refused length in the server's words", async () => {
    createTestimonial.mockResolvedValue({
      data: null,
      error: {
        status: 400,
        title: "Некорректные данные",
        errors: { Text: ["Отзыв от 1 до 10000 символов"] },
      },
    });

    const form = await submitted();

    expect(form.errorMessage.value).toBe("Отзыв от 1 до 10000 символов");
  });

  it("clears the form when the testimonial was posted", async () => {
    createTestimonial.mockResolvedValue({ data: { id: "t-1" }, error: null });

    const form = await submitted();

    expect(form.errorMessage.value).toBe("");
    expect(form.testimonialText.value).toBe("");
    expect(form.formExpanded.value).toBe(false);
  });
});
