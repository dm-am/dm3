import { describe, it, expect, beforeEach } from "vitest";
import {
  registerExpandable,
  expandAll,
  collapseAll,
  allExpanded,
  hasAny,
  clearRegistry,
  notifyExpandableChanged,
  type ExpandableHandle,
} from "./useExpandableRegistry";

function createHandle(
  initialExpanded = false,
): ExpandableHandle & { state: boolean } {
  const handle: ExpandableHandle & { state: boolean } = {
    id: Symbol("test"),
    state: initialExpanded,
    isExpanded() {
      return this.state;
    },
    expand() {
      this.state = true;
    },
    collapse() {
      this.state = false;
    },
  };
  return handle;
}

describe("useExpandableRegistry", () => {
  beforeEach(() => {
    clearRegistry();
  });

  it("registers and tracks handles", () => {
    expect(hasAny.value).toBe(false);
    const h = createHandle();
    const unreg = registerExpandable(h);
    expect(hasAny.value).toBe(true);
    unreg();
    expect(hasAny.value).toBe(false);
  });

  it("expandAll expands all handles", () => {
    const h1 = createHandle();
    const h2 = createHandle();
    const h3 = createHandle();
    registerExpandable(h1);
    registerExpandable(h2);
    registerExpandable(h3);

    expandAll();

    expect(h1.state).toBe(true);
    expect(h2.state).toBe(true);
    expect(h3.state).toBe(true);
    expect(allExpanded.value).toBe(true);
  });

  it("collapseAll collapses all handles", () => {
    const h1 = createHandle(true);
    const h2 = createHandle(true);
    registerExpandable(h1);
    registerExpandable(h2);

    collapseAll();

    expect(h1.state).toBe(false);
    expect(h2.state).toBe(false);
    expect(allExpanded.value).toBe(false);
  });

  /**
   * THE BUG SCENARIO:
   * On /pulse, each GamePost registers a handle. When user changes sort,
   * router.replace() fires router.afterEach → clearRegistry().
   * If components DON'T remount (same :key), handles are gone and never
   * re-registered. expandAll() then has no handles to expand.
   *
   * FIX: clearRegistry only on route NAME change, not query-only changes.
   * This test verifies that handles survive a clearRegistry-free scenario
   * and expandAll works on all of them.
   */
  it("expandAll works on all handles even when expand callbacks call notifyExpandableChanged", () => {
    // Simulate GamePost handles where expand() calls notifyExpandableChanged()
    // (the old buggy behavior — should still work now with snapshot+restore)
    const handles: Array<ExpandableHandle & { state: boolean }> = [];
    for (let i = 0; i < 5; i++) {
      const h = createHandle();
      // Override expand to simulate old GamePost behavior: calling notifyExpandableChanged
      const origExpand = h.expand.bind(h);
      h.expand = () => {
        origExpand();
        notifyExpandableChanged(); // This used to kill pendingAction mid-loop
      };
      handles.push(h);
      registerExpandable(h);
    }

    expandAll();

    // ALL 5 handles must be expanded, not just the first one
    for (let i = 0; i < 5; i++) {
      expect(handles[i].state, `Handle ${i} should be expanded`).toBe(true);
    }
    expect(allExpanded.value).toBe(true);
  });

  it("late-registering handles auto-expand after expandAll", () => {
    const h1 = createHandle();
    registerExpandable(h1);

    expandAll();
    expect(h1.state).toBe(true);

    // Late arrival — should auto-expand
    const h2 = createHandle();
    registerExpandable(h2);
    expect(h2.state).toBe(true);
  });

  it("late-registering handles auto-expand even when expand callbacks called notifyExpandableChanged", () => {
    const h1 = createHandle();
    h1.expand = () => {
      h1.state = true;
      notifyExpandableChanged();
    };
    registerExpandable(h1);

    expandAll();
    expect(h1.state).toBe(true);

    // pendingAction must survive despite notifyExpandableChanged in expand callback
    const h2 = createHandle();
    registerExpandable(h2);
    expect(h2.state).toBe(true);
  });

  it("clearRegistry wipes all handles and pendingAction", () => {
    const h1 = createHandle();
    registerExpandable(h1);
    expandAll();

    clearRegistry();

    expect(hasAny.value).toBe(false);

    // New handle should NOT auto-expand (pendingAction was cleared)
    const h2 = createHandle();
    registerExpandable(h2);
    expect(h2.state).toBe(false);
  });

  it("handles survive when clearRegistry is NOT called (query-only navigation)", () => {
    // Register 5 handles (simulating 5 GamePost components)
    const handles: Array<ExpandableHandle & { state: boolean }> = [];
    for (let i = 0; i < 5; i++) {
      const h = createHandle();
      handles.push(h);
      registerExpandable(h);
    }

    // DO NOT call clearRegistry — simulates query-only route change
    // (sort/filter change via router.replace with same route name)

    // expandAll should work on all 5
    expandAll();
    for (let i = 0; i < 5; i++) {
      expect(
        handles[i].state,
        `Handle ${i} should be expanded after sort`,
      ).toBe(true);
    }
  });
});
