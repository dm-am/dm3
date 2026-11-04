/**
 * The page the editor's specs mount it on: a vi.fn localStorage, a
 * ResizeObserver that watches nothing and Teleport in place. The help needs
 * vueFinalModalStub.ts only when opened.
 *
 *   const { localStorageMock, clean, reset } = editorShell();
 *   beforeEach(clean);
 *   afterEach(reset);
 */
import { join } from "node:path";
import { vi } from "vitest";
import { config } from "@vue/test-utils";
import { CLIENT_SRC } from "./sourceTree";
import { componentCss } from "./styles";

/** The editor and its parts, read together so a line moving between them is still read. */
const EDITOR_SHEETS = [
  "BBCodeEditor.vue",
  "BBCodeEditorToolbar.vue",
  "EditorModeSwitch.vue",
  "EditorStatusLine.vue",
  "MarkupCheck.vue",
].map((file) => join(CLIENT_SRC, "shared/ui/BBCodeEditor", file));

/** The editor's stylesheet with its parts', compiled one after the other. */
export const editorCss = (): string =>
  EDITOR_SHEETS.map(componentCss).join("\n");

/** A store of vi.fn over a plain object, emptied by clear(). */
function storageMock() {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] || null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
    get length() {
      return Object.keys(store).length;
    },
    key: vi.fn((index: number) => Object.keys(store)[index] || null),
  };
}

export function editorShell() {
  const localStorageMock = storageMock();
  Object.defineProperty(window, "localStorage", { value: localStorageMock });

  // A function rather than an arrow: the editor watches its mode tabs with
  // one, built with `new`, and vitest will not construct an arrow.
  vi.stubGlobal(
    "ResizeObserver",
    vi.fn(function () {
      return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
    }),
  );

  // Teleport renders its content in place, not in <body>.
  config.global.stubs = { Teleport: true };

  return {
    localStorageMock,
    /** Before each test: an empty store and no calls on record. */
    clean() {
      localStorageMock.clear();
      vi.clearAllMocks();
    },
    /** After each test: every mock as it was built. */
    reset() {
      // Since vitest 4, restoreAllMocks touches only vi.spyOn spies, so a
      // mockImplementation left on the vi.fn-based localStorage mock would
      // leak into the next test. resetAllMocks puts the original
      // implementations back.
      vi.resetAllMocks();
      vi.restoreAllMocks();
    },
  };
}
