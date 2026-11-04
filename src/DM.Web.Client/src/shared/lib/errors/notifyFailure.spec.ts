import { beforeEach, describe, expect, it } from "vitest";
import { problem } from "@/shared/lib/testing/problem";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "./notifyFailure";

describe("notifyFailure", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it.each([
    ["an expired session", 401],
    ["a refused action", 403],
    ["a rate limit", 429],
    ["a server error", 500],
    ["a bad gateway", 502],
  ])(
    "stays quiet about %s, which the interceptor announced",
    (_case, status) => {
      notifyFailure(problem(status), "не удалось сохранить");

      expect(useToast().toasts.value).toHaveLength(0);
    },
  );

  it.each([
    ["a rejected request", 400],
    ["a missing entity", 404],
    ["a conflict", 409],
  ])("speaks about %s, which the interceptor left alone", (_case, status) => {
    notifyFailure(problem(status), "не удалось сохранить");

    const [toast] = useToast().toasts.value;
    expect(toast.message).toBe("не удалось сохранить");
    expect(toast.type).toBe("error");
  });

  // The edge refuses a body over its limit with an html page: the status is the
  // whole message.
  it("names the size of the file when the edge refused the body", () => {
    notifyFailure(problem(413), "не удалось загрузить");

    expect(useToast().toasts.value[0].message).toBe("Файл слишком большой");
  });

  // Below the title: a 413 the API issues itself carries a more specific one.
  it("still prefers a 413 the server described itself", () => {
    notifyFailure(
      problem(413, "Вложение больше, чем разрешено в этой игре"),
      "не удалось загрузить",
    );

    expect(useToast().toasts.value[0].message).toBe(
      "Вложение больше, чем разрешено в этой игре",
    );
  });

  it("prefers what the server named over the fallback", () => {
    notifyFailure(
      problem(409, "Игра с таким названием уже есть"),
      "не удалось создать",
    );

    expect(useToast().toasts.value[0].message).toBe(
      "Игра с таким названием уже есть",
    );
  });

  it("reads out the field codes when the request failed validation", () => {
    const rejected = {
      ...problem(400),
      errors: { title: ["Long"] },
    };

    notifyFailure(rejected, "не удалось создать");

    // The field rule beats the title and the fallback: it says what to correct.
    expect(useToast().toasts.value[0].message).toBe("Слишком длинное значение");
  });

  // A plate is not under a field, so it names the fields by their labels.
  it("names the refused fields it was given labels for", () => {
    const rejected = {
      ...problem(400, "Некорректные данные"),
      errors: {
        SystemName: ["Empty"],
        NarrativeSetting: ["Empty"],
        Title: ["Long"],
      },
    };

    notifyFailure(rejected, "Не удалось сохранить информацию игры", {
      title: "Название",
      systemName: "Система",
      narrativeSetting: "Сеттинг",
    });

    expect(useToast().toasts.value.map((t) => t.message)).toEqual([
      "Система, Сеттинг: обязательное поле. Название: слишком длинное значение",
    ]);
  });

  it("reads a field without a label, or a sentence of the server's, as it stands", () => {
    const rejected = {
      ...problem(400),
      errors: {
        Tags: ["TooMany"],
        Text: ["Тег [private] здесь не разрешен"],
      },
    };

    notifyFailure(rejected, "Не удалось сохранить комментарий", {
      text: "Текст комментария",
    });

    expect(useToast().toasts.value.map((t) => t.message)).toEqual([
      "Слишком много значений. Тег [private] здесь не разрешен",
    ]);
  });

  // Status 0 is the interceptor's: only it offers the other address of the site.
  it("stays quiet when the request never reached the server", () => {
    notifyFailure(problem(0), "нет соединения с сервером");

    expect(useToast().toasts.value).toHaveLength(0);
  });
});
