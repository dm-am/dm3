import { describe, it, expect } from "vitest";
import { ref, nextTick } from "vue";
import { useCodeInput, type CodeMode } from "./useCodeInput";

/**
 * The second-factor field against the server's parse
 * (RecoveryCodeFactory.Normalize), through a real <input> and real events:
 * the filter runs on input to catch paste, autofill and soft keyboards, and
 * the caret survives the dashes.
 */
const field = (mode: CodeMode = "digits") => {
  const modeRef = ref<CodeMode>(mode);
  const element = document.createElement("input");
  document.body.append(element);

  const code = useCodeInput({ mode: modeRef });
  code.inputRef.value = element;
  // Listeners, not direct calls: what is under test is a field wired to the
  // events a browser actually sends.
  element.addEventListener("input", code.onInput);
  element.addEventListener("keydown", code.onKeydown);

  return { code, element, modeRef };
};

/** Text arriving in the field the way a paste or an autofill delivers it. */
const arrive = (
  element: HTMLInputElement,
  text: string,
  caret = text.length,
) => {
  element.value = text;
  element.setSelectionRange(caret, caret);
  element.dispatchEvent(new Event("input"));
};

/** The Backspace a browser sends before it deletes anything. */
const backspace = (element: HTMLInputElement, at: number) => {
  element.setSelectionRange(at, at);
  element.dispatchEvent(
    new KeyboardEvent("keydown", { key: "Backspace", cancelable: true }),
  );
};

/** Its twin, aimed the other way. */
const del = (element: HTMLInputElement, at: number) => {
  element.setSelectionRange(at, at);
  element.dispatchEvent(
    new KeyboardEvent("keydown", { key: "Delete", cancelable: true }),
  );
};

describe("the code from the authenticator", () => {
  it("keeps the digits out of whatever was pasted", () => {
    const { code, element } = field("digits");

    arrive(element, "123 456");

    expect(element.value).toBe("123456");
    expect(code.normalized.value).toBe("123456");
  });

  it("stops at six", () => {
    const { code, element } = field("digits");

    arrive(element, "1234567890");

    expect(element.value).toBe("123456");
    expect(code.isComplete.value).toBe(true);
  });

  it("is not complete until all six are there", () => {
    const { code, element } = field("digits");

    arrive(element, "12345");

    expect(code.isComplete.value).toBe(false);
  });
});

describe("the recovery code", () => {
  it("raises the case and groups by four", () => {
    const { code, element } = field("recovery");

    arrive(element, "7k3m9q2wxz8p4r6t");

    expect(element.value).toBe("7K3M-9Q2W-XZ8P-4R6T");
    expect(code.normalized.value).toBe("7K3M9Q2WXZ8P4R6T");
    expect(code.isComplete.value).toBe(true);
  });

  it("reads I and L as one and O as zero, as the server does", () => {
    const { code } = field("recovery");

    code.setValue("iLoT");

    expect(code.normalized.value).toBe("110T");
  });

  it("drops what the alphabet does not hold, dashes and spaces included", () => {
    const { code } = field("recovery");

    // U is out of Crockford's alphabet on purpose, so that no set of codes
    // accidentally spells something.
    code.setValue("7K3M-9Q2W u!");

    expect(code.normalized.value).toBe("7K3M9Q2W");
  });

  it("shows sixteen symbols in nineteen positions", () => {
    const { code } = field("recovery");

    code.setValue("7K3M9Q2WXZ8P4R6TZZZZ");

    expect(code.value.value).toBe("7K3M-9Q2W-XZ8P-4R6T");
    expect(code.value.value).toHaveLength(19);
    expect(code.maxLength.value).toBe(19);
  });

  it("rewrites what is already typed when the mode is switched", async () => {
    const { code, element, modeRef } = field("digits");

    arrive(element, "123456");
    modeRef.value = "recovery";
    await nextTick();

    expect(element.value).toBe("1234-56");
    expect(code.isComplete.value).toBe(false);
  });
});

describe("the caret", () => {
  it("stays where the reader put it when he edits the middle", () => {
    const { element } = field("digits");

    arrive(element, "1234");
    // "12|34", and a 9 typed into the gap.
    arrive(element, "12934", 3);

    expect(element.value).toBe("12934");
    expect(element.selectionStart).toBe(3);
  });

  it("steps over a dash that appeared in front of it", () => {
    const { element } = field("recovery");

    arrive(element, "7K3M9Q2W");
    expect(element.value).toBe("7K3M-9Q2W");

    // "7K3M|-59Q2W": the 5 is typed at the end of the first group, and the
    // dash the reformat puts back must not swallow the caret.
    arrive(element, "7K3M5-9Q2W", 5);

    expect(element.value).toBe("7K3M-59Q2-W");
    expect(element.selectionStart).toBe(6);
  });
});

describe("Backspace in front of a dash", () => {
  it("takes the symbol before the dash and not the dash", () => {
    const { element } = field("recovery");

    arrive(element, "7K3M9Q2W");
    // "7K3M-|9Q2W": the caret sits right after the dash.
    backspace(element, 5);

    expect(element.value).toBe("7K39-Q2W");
    expect(element.selectionStart).toBe(3);
  });

  it("leaves the key to the browser everywhere else", () => {
    const { element } = field("recovery");

    arrive(element, "7K3M9Q2W");
    const event = new KeyboardEvent("keydown", {
      key: "Backspace",
      cancelable: true,
    });
    element.setSelectionRange(3, 3);
    element.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(element.value).toBe("7K3M-9Q2W");
  });
});

// Delete in front of a dash, the mirror of Backspace.
describe("Delete in front of a dash", () => {
  it("takes the symbol after the dash and not the dash", () => {
    const { element } = field("recovery");

    arrive(element, "7K3M9Q2W");
    // "7K3M|-9Q2W": the caret sits right before the dash.
    del(element, 4);

    expect(element.value).toBe("7K3M-Q2W");
    expect(element.selectionStart).toBe(5);
  });

  it("leaves the key to the browser everywhere else", () => {
    const { element } = field("recovery");

    arrive(element, "7K3M9Q2W");
    const event = new KeyboardEvent("keydown", {
      key: "Delete",
      cancelable: true,
    });
    element.setSelectionRange(2, 2);
    element.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(element.value).toBe("7K3M-9Q2W");
  });
});

// An edit made here reaches the screen through the field's input event.
describe("an edit the composable made itself", () => {
  const withRefusal = (mode: CodeMode = "recovery") => {
    const built = field(mode);
    const refusal = { text: "Код не подошел" };
    // Attached after the code arrives, so only the edit under test clears it.
    const arm = () =>
      built.element.addEventListener("input", () => {
        refusal.text = "";
      });
    return { ...built, refusal, arm };
  };

  it("says so after Backspace over a dash", () => {
    const { element, refusal, arm } = withRefusal();

    arrive(element, "7K3M9Q2W");
    arm();
    backspace(element, 5);

    expect(element.value).toBe("7K39-Q2W");
    expect(refusal.text).toBe("");
  });

  it("says so after Delete over a dash", () => {
    const { element, refusal, arm } = withRefusal();

    arrive(element, "7K3M9Q2W");
    arm();
    del(element, 4);

    expect(element.value).toBe("7K3M-Q2W");
    expect(refusal.text).toBe("");
  });
});

// A recovery code pasted into the digit field fills it and reads as a device code.
describe("what the parse threw away", () => {
  it("is nothing when the digits arrive as digits", () => {
    const { code, element } = field("digits");

    arrive(element, "123 456");

    expect(code.isComplete.value).toBe(true);
    expect(code.droppedSymbols.value).toBe(false);
  });

  it("is nothing when a recovery code arrives in the recovery mode", () => {
    const { code, element } = field("recovery");

    arrive(element, "7K3M-9Q2W-XZ8P-4R6T");

    expect(code.isComplete.value).toBe(true);
    expect(code.droppedSymbols.value).toBe(false);
  });

  it("is the letters of a recovery code pasted into the digit field", () => {
    const { code, element } = field("digits");

    arrive(element, "7K3M-9Q2W-XZ8P-4R6T");

    // Six digits, and the field cannot tell them from a real one by length.
    expect(element.value).toBe("739284");
    expect(code.isComplete.value).toBe(true);
    expect(code.droppedSymbols.value).toBe(true);
  });

  it("is forgotten as soon as the reader types a code of his own", () => {
    const { code, element } = field("digits");

    arrive(element, "7K3M-9Q2W-XZ8P-4R6T");
    arrive(element, "123456");

    expect(code.droppedSymbols.value).toBe(false);
  });
});
