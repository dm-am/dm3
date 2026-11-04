import { afterEach } from "vitest";
import { cleanupBbcodeInteractive } from "@/shared/lib/utils/bbcodeCleanup";

/**
 * Puts server markup on the page, brought to life by `init`, and takes it
 * down with its listeners after each test. Called at the top of a describe.
 */
export function bbcodeStage(
  init: (container: HTMLElement) => void,
): (html: string) => HTMLElement {
  const containers: HTMLElement[] = [];
  afterEach(() => {
    for (const container of containers.splice(0)) {
      cleanupBbcodeInteractive(container);
      container.remove();
    }
  });
  return (html) => {
    const container = document.createElement("div");
    container.innerHTML = html;
    document.body.appendChild(container);
    containers.push(container);
    init(container);
    return container;
  };
}
