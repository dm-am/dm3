import { beforeEach, describe, expect, it } from "vitest";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "./notifyFailure";

function problem(status: number, title = ""): GeneralError {
  return { type: "", title, status, traceId: "t" };
}

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

  // The edge refuses a body over its limit and answers an html page, so the API
  // never sees the request and there is no problem document to read: the status is
  // the whole message. The fallback names the action ("не удалось загрузить"),
  // which tells the reader nothing about why.
  it("names the size of the file when the edge refused the body", () => {
    notifyFailure(problem(413), "не удалось загрузить");

    expect(useToast().toasts.value[0].message).toBe("Файл слишком большой");
  });

  // Below the title, not above it: a 413 the API issues itself carries a document,
  // and the sentence in it is more specific than the one this branch supplies.
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
      errors: { title: ["TitleTooLong"] },
    };

    notifyFailure(rejected, "не удалось создать");

    // The specific rule beats both the title and the fallback: it is the only
    // one of the three that says what to correct.
    expect(useToast().toasts.value[0].message).not.toBe("не удалось создать");
  });

  /**
   * A plate is not under a field, and "Обязательное поле" on it left the
   * reader to find which field it meant. The call knows the fields it sent,
   * by the server's names for them, and hands their labels over.
   */
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
      "Система, Сеттинг: не заполнено. Название: слишком длинное значение",
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

  // send() maps a missing response to status 0, and the interceptor always
  // speaks for that one: it is the only sentence that knows the other address
  // of the site and offers the press that goes there. A second one from the
  // call site was the same event said twice, in words that offer nothing.
  it("stays quiet when the request never reached the server", () => {
    notifyFailure(problem(0), "нет соединения с сервером");

    expect(useToast().toasts.value).toHaveLength(0);
  });
});
