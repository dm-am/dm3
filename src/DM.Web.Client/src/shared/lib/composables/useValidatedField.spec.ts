import { describe, it, expect, vi } from "vitest";
import { nextTick } from "vue";
import { useValidatedField, validators } from "./useValidatedField";

/** An answer of the server check that comes when the test says so. */
function heldCheck() {
  const answers: Array<(value: string | null) => void> = [];
  const check = vi.fn(
    () => new Promise<string | null>((resolve) => answers.push(resolve)),
  );
  return { check, answer: (value: string | null) => answers.shift()!(value) };
}

const TAKEN = "Имя занято";

/** A name field the server checks, with a name typed in. */
const checkedName = (
  check: (value: string) => Promise<string | null>,
  initialValue = "анна",
) => useValidatedField({ asyncValidate: check, initialValue });

describe("useValidatedField: the check of the form", () => {
  it("says nothing before the field is left, however wrong it is", () => {
    const field = useValidatedField({ validate: validators.required("Нужно") });

    expect(field.isValid.value).toBe(false);
    expect(field.error.value).toBe("");
  });

  it("says the reason once the field is left, and drops it at the next key", async () => {
    const field = useValidatedField({
      validate: validators.minLength(3),
    });
    field.value.value = "ab";

    await field.onBlur();
    expect(field.error.value).toBe("Минимум 3 символа");

    field.value.value = "abc";
    field.onInput();
    expect(field.error.value).toBe("");
  });

  it("reads the value without the spaces around it", async () => {
    const validate = vi.fn(validators.minLength(3));
    const field = useValidatedField({ validate, initialValue: "  ab  " });

    await field.onBlur();

    expect(validate).toHaveBeenLastCalledWith("ab");
    expect(field.error.value).toBe("Минимум 3 символа");
  });

  it("counts an empty reason as a fault: the field is not valid and says nothing", async () => {
    const field = useValidatedField({ validate: validators.required() });

    await field.onBlur();

    expect(field.isValid.value).toBe(false);
    expect(field.isReady.value).toBe(false);
    expect(field.error.value).toBe("");
  });

  it("is ready at once when it has no server check", () => {
    const field = useValidatedField({
      validate: validators.required(),
      initialValue: "имя",
    });

    expect(field.isReady.value).toBe(true);
  });
});

describe("useValidatedField: the server check", () => {
  it("asks the server only once the field is left valid, and waits for it", async () => {
    const { check, answer } = heldCheck();
    const field = useValidatedField({
      validate: validators.minLength(3),
      asyncValidate: check,
      initialValue: "ab",
    });

    await field.onBlur();
    expect(check).not.toHaveBeenCalled();

    field.value.value = " анна ";
    field.onInput();
    const leaving = field.onBlur();
    await nextTick();
    expect(check).toHaveBeenCalledWith("анна");
    expect(field.isChecking.value).toBe(true);
    expect(field.isReady.value).toBe(false);

    answer(null);
    await leaving;
    expect(field.isChecking.value).toBe(false);
    expect(field.isReady.value).toBe(true);
  });

  it("says the server's reason and is not ready", async () => {
    const { check, answer } = heldCheck();
    const field = checkedName(check);

    const leaving = field.onBlur();
    answer(TAKEN);
    await leaving;

    expect(field.error.value).toBe(TAKEN);
    expect(field.isReady.value).toBe(false);
  });

  it("does not ask again for a value the server has passed", async () => {
    const check = vi.fn(async () => null);
    const field = checkedName(check);

    await field.onBlur();
    await field.onBlur();
    expect(await field.validate()).toBe(true);

    expect(check).toHaveBeenCalledTimes(1);
  });

  it("asks again for a value the server refused", async () => {
    const check = vi.fn(async () => TAKEN);
    const field = checkedName(check);

    await field.onBlur();
    await field.onBlur();

    expect(check).toHaveBeenCalledTimes(2);
  });

  it("forgets the pass once the value changes, and asks for the new one", async () => {
    const check = vi.fn(async () => null);
    const field = checkedName(check);
    await field.onBlur();
    expect(field.isReady.value).toBe(true);

    field.value.value = "мария";
    field.onInput();
    expect(field.isReady.value).toBe(false);

    await field.onBlur();
    expect(check).toHaveBeenLastCalledWith("мария");
    expect(field.isReady.value).toBe(true);
  });

  it("keeps quiet and stays not ready when the check itself fails", async () => {
    const check = vi.fn(async () => {
      throw new Error("Network Error");
    });
    const field = checkedName(check);

    await field.onBlur();

    expect(field.error.value).toBe("");
    expect(field.isChecking.value).toBe(false);
    expect(field.isReady.value).toBe(false);
  });

  it("takes the server's reason off once a later check passes", async () => {
    const check = vi.fn().mockResolvedValueOnce(TAKEN).mockResolvedValue(null);
    const field = checkedName(check);
    await field.onBlur();
    expect(field.error.value).toBe(TAKEN);

    await field.onBlur();

    expect(field.error.value).toBe("");
    expect(field.isReady.value).toBe(true);
  });

  it("does not ask about an empty field", async () => {
    const check = vi.fn(async () => null);
    const field = checkedName(check, "  ");

    await field.onBlur();

    expect(check).not.toHaveBeenCalled();
    expect(field.isReady.value).toBe(false);
  });
});

describe("useValidatedField: the form's own moves", () => {
  it("answers a press of the form with the sync reason first", async () => {
    const check = vi.fn(async () => TAKEN);
    const field = useValidatedField({
      validate: validators.required("Введите имя"),
      asyncValidate: check,
    });

    expect(await field.validate()).toBe(false);
    expect(field.error.value).toBe("Введите имя");
    expect(check).not.toHaveBeenCalled();
  });

  it("answers a press of the form with the server's reason, then with a pass", async () => {
    const check = vi.fn().mockResolvedValueOnce(TAKEN).mockResolvedValue(null);
    const field = checkedName(check);

    expect(await field.validate()).toBe(false);
    expect(field.error.value).toBe(TAKEN);

    expect(await field.validate()).toBe(true);
    expect(field.error.value).toBe("");
  });

  it("takes a reason the server gave on submit", () => {
    const field = useValidatedField();

    field.setError("Неверный пароль");

    expect(field.error.value).toBe("Неверный пароль");
  });

  it("starts over on a reset: the value, the reason and the pass", async () => {
    const check = vi.fn(async () => null);
    const field = checkedName(check);
    await field.onBlur();
    field.setError("Неверный пароль");

    field.reset();
    expect(field.value.value).toBe("анна");
    expect(field.error.value).toBe("");
    expect(field.isReady.value).toBe(false);

    field.reset("мария");
    expect(field.value.value).toBe("мария");
    await field.onBlur();
    expect(check).toHaveBeenCalledTimes(2);
  });
});

describe("validators", () => {
  it.each([
    ["", "Нужно"],
    ["   ", "Нужно"],
    ["x", null],
  ])("required reads %j as %j", (value, result) => {
    expect(validators.required("Нужно")(value)).toBe(result);
  });

  it.each([
    ["", null],
    ["reader@dm.am", null],
    ["Reader.Name+tag@mail.example.RU", null],
    ["reader@dm", "Неверный формат почты"],
    ["reader dm.am", "Неверный формат почты"],
  ])("email reads %j as %j", (value, result) => {
    expect(validators.email()(value)).toBe(result);
  });

  it.each([
    [1, "a", null],
    [1, "", null],
    [2, "a", "Минимум 2 символа"],
    [5, "abcd", "Минимум 5 символов"],
    [21, "a", "Минимум 21 символ"],
  ])("minLength %i reads %j as %j", (min, value, result) => {
    expect(validators.minLength(min)(value)).toBe(result);
  });

  it("minLength says the reason it is given", () => {
    expect(validators.minLength(8, "Короткий пароль")("abc")).toBe(
      "Короткий пароль",
    );
  });

  it("combine says the first reason, an empty one included", () => {
    const both = validators.combine(
      validators.required(""),
      validators.email("Почта"),
    );

    expect(both("")).toBe("");
    expect(both("reader@dm")).toBe("Почта");
    expect(both("reader@dm.am")).toBeNull();
  });
});
