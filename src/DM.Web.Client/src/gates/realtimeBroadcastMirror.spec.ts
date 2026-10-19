/**
 * @vitest-environment node
 */

/**
 * The three global chat events go to every open connection, guests included,
 * and store no notification: the shell lists them as a switch group with an
 * empty body, past the bell, and that group mirrors the Broadcast set of the
 * API. A broadcast event added server-side would land in the default branch
 * and cost every open tab a request per message; one that stops being a
 * broadcast would keep its empty case and the bell would stop re-reading on it.
 * Asserted on the sources: both sides are lists of names in two languages.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

const APP = readFileSync(join(CLIENT_SRC, "app", "App.vue"), "utf8");
const PROCESSOR = readFileSync(
  join(
    CLIENT_SRC,
    "..",
    "..",
    "DM.Web.API",
    "Realtime",
    "RealtimeNotificationProcessor.cs",
  ),
  "utf8",
);

const CASE_LABEL = /^\s*case NotificationType\.(\w+):\s*$/;
const COMMENT_LINE = /^\s*(\/\/|\/\*|\*)/;

/**
 * The event names of the switch group whose whole body is `break;`. Consecutive
 * labels share a body, so the group is read as it is written: labels collected
 * until a line of body, then kept if that line was the break and dropped if the
 * group acts on its events.
 */
function inertCaseLabels(source: string): string[] {
  let labels: string[] = [];
  for (const line of source.split("\n")) {
    const label = CASE_LABEL.exec(line);
    if (label) {
      labels.push(label[1]);
      continue;
    }
    if (labels.length === 0) continue;
    if (line.trim() === "" || COMMENT_LINE.test(line)) continue;
    if (line.trim() === "break;") return labels;
    labels = [];
  }
  return [];
}

/** The names listed in the Broadcast collection initializer of the processor. */
function serverBroadcast(source: string): string[] {
  const block = /HashSet<EventType>\s+Broadcast\s*=\s*\[([^\]]*)\]/.exec(
    source,
  );
  if (!block) return [];
  return [...block[1].matchAll(/EventType\.(\w+)/g)].map((match) => match[1]);
}

describe("the broadcast set the shell mirrors", () => {
  it("names the same events the API broadcasts", () => {
    const server = serverBroadcast(PROCESSOR);
    const client = inertCaseLabels(APP);

    // A regex that matched nothing would agree with anything.
    expect(server.length).toBeGreaterThan(0);
    expect(client.length).toBeGreaterThan(0);

    // Named rather than compared as two sorted lists: what a failure has to say
    // is which side is short, and what to do about it. An event the API
    // broadcasts and the shell does not know about belongs in the empty case in
    // handleNotification (App.vue); one the shell still lists and the API no
    // longer broadcasts is addressed to somebody now, and its case has to go so
    // the default branch re-reads the bell again.
    expect({
      broadcastByTheApiOnly: server.filter((name) => !client.includes(name)),
      inertInTheShellOnly: client.filter((name) => !server.includes(name)),
    }).toEqual({ broadcastByTheApiOnly: [], inertInTheShellOnly: [] });
  });
});
