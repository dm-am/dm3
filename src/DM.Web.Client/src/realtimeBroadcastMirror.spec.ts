/**
 * @vitest-environment node
 */

/**
 * The three global chat events go to every open connection, guests included,
 * and store no notification for anybody. The shell knows that as a switch group
 * with an empty body: the events fall past the bell instead of into the default
 * branch, where each of them would cost every open tab a request for a number
 * that has not moved.
 *
 * That group is a copy of the Broadcast set in the API, and until now a comment
 * was all that said so. A fourth broadcast event added server-side lands in the
 * default branch here, and one busy chat becomes a request per message from
 * every tab on the site again. The other way round is quieter and worse: an
 * event that stops being a broadcast keeps its empty case here and the bell
 * stops re-reading on it.
 *
 * Asserted on the sources, because both sides are lists of names, they live in
 * two languages, and no rendered page shows either of them.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

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
