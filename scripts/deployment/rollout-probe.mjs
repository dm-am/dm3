#!/usr/bin/env node
// The probe of the release proof (release design 3.16): what a visitor sees while a release
// rolls out, and the verdict on it. A rollout is promised to cost no failed request, so every
// request has one exact expected answer: a 401, 404 or 429 is a failure like a 5xx or no answer.
//
//   rollout-probe.mjs run --base <url> --auth <user:password> --asset <path> --volume-file <path> --out <file>
//     Runs until SIGTERM. /v1/boards and /_ready once a second each, which keeps the probe at
//     120 a minute under the API's limit of 300 per address; /nginx-health, the document, one
//     hashed asset and one file placed only in the assets volume, ten a second together; one
//     hub connection, opened again a second after it closes. Every request on a new connection,
//     so a reload of the edge is met the way a new visitor meets it. One JSON line per answer.
//
//   rollout-probe.mjs verdict --log <file> [--windows <file>] [--floor <n>] [--max-disconnects <n>]
//     Exits 0 when the log holds no failure, at least <floor> answers inside each window (one
//     "<start ms> <end ms>" per line, the switches of the edge), every hub close followed by an
//     open within 10 s, and at most <max-disconnects> closes. Prints the reasons otherwise.
import fs from "node:fs";
import http from "node:http";

const REQUEST_TIMEOUT_MS = 10_000;
const RECONNECT_WITHIN_MS = 10_000;

function options(argv) {
  const parsed = {};
  for (let i = 0; i < argv.length; i += 2) {
    if (!argv[i].startsWith("--") || argv[i + 1] === undefined) {
      throw new Error(`expected --name value pairs, got ${argv.slice(i).join(" ")}`);
    }
    parsed[argv[i].slice(2)] = argv[i + 1];
  }
  return parsed;
}

function run(opts) {
  for (const name of ["base", "auth", "asset", "volume-file", "out"]) {
    if (!opts[name]) throw new Error(`run needs --${name}`);
  }
  const base = new URL(opts.base);
  const authorization = `Basic ${Buffer.from(opts.auth).toString("base64")}`;
  const out = fs.createWriteStream(opts.out, { flags: "a" });
  const record = (entry) => out.write(`${JSON.stringify({ t: Date.now(), ...entry })}\n`);
  let stopping = false;
  let inFlight = 0;

  const ask = (path) => {
    inFlight++;
    const started = Date.now();
    const done = (entry) => {
      inFlight--;
      record({ path, ms: Date.now() - started, ...entry });
    };
    // agent: false is a new connection per request.
    const request = http.request(
      { host: base.hostname, port: base.port || 80, path, agent: false, timeout: REQUEST_TIMEOUT_MS,
        headers: { Authorization: authorization, Connection: "close" } },
      (response) => {
        response.resume();
        response.on("end", () => done({ status: response.statusCode }));
        response.on("error", (error) => done({ error: error.message }));
      });
    request.on("timeout", () => request.destroy(new Error("no answer within 10 s")));
    request.on("error", (error) => done({ error: error.message }));
    request.end();
  };

  const slow = setInterval(() => { ask("/v1/boards"); ask("/_ready"); }, 1000);
  const fastPaths = ["/nginx-health", "/", opts.asset, opts["volume-file"]];
  let next = 0;
  const fast = setInterval(() => { ask(fastPaths[next]); next = (next + 1) % fastPaths.length; }, 100);

  // SignalR's JSON protocol over a WebSocket, without negotiation: the handshake, then a ping
  // every ten seconds so the server keeps the connection.
  let socket;
  let pinger;
  const connect = () => {
    const url = `${base.protocol === "https:" ? "wss" : "ws"}://${base.host}/hubs/notifications`;
    socket = new WebSocket(url, { headers: { Authorization: authorization } });
    socket.addEventListener("open", () => {
      record({ hub: "open" });
      socket.send('{"protocol":"json","version":1}\u001e');
      pinger = setInterval(() => socket.send('{"type":6}\u001e'), 10_000);
    });
    socket.addEventListener("close", () => {
      clearInterval(pinger);
      if (stopping) return;
      record({ hub: "close" });
      setTimeout(connect, 1000);
    });
    socket.addEventListener("error", () => {});
  };
  connect();

  process.on("SIGTERM", () => {
    stopping = true;
    clearInterval(slow);
    clearInterval(fast);
    socket.close();
    const finish = () => (inFlight > 0 ? setTimeout(finish, 100) : out.end(() => process.exit(0)));
    finish();
  });
}

// Every answer that is not a 200, the first twenty by name, and an empty log as a failure of its own.
function answerReasons(answers, failures) {
  const reasons = failures.slice(0, 20).map((failure) => `${new Date(failure.t).toISOString()} ${failure.path}: ${failure.status ?? failure.error}`);
  if (failures.length > 20) reasons.push(`and ${failures.length - 20} more failures`);
  if (answers.length === 0) reasons.push("the probe got no answer at all");
  return reasons;
}

// One "<start ms> <end ms>" per line; no file means no windows.
function readWindows(file) {
  if (!file || !fs.existsSync(file)) return [];
  return fs.readFileSync(file, "utf8").split("\n").filter(Boolean).map((line) => line.split(/\s+/).map(Number));
}

// A switch of the edge the probe went quiet through: fewer than floor answers inside it.
function windowReasons(answers, windows, floor) {
  const reasons = [];
  for (const [start, end] of windows) {
    const inside = answers.filter((entry) => entry.t >= start && entry.t <= end).length;
    if (inside < floor) {
      reasons.push(`only ${inside} answers between ${new Date(start).toISOString()} and ${new Date(end).toISOString()}, fewer than ${floor}`);
    }
  }
  return reasons;
}

// The hub connected at all, closed no more than allowed, and opened again within 10 s of each
// close, or the log went on for 10 s without it.
function hubReasons(entries, closes, maxDisconnects) {
  const reasons = [];
  const hub = entries.filter((entry) => entry.hub !== undefined);
  const last = entries.reduce((latest, entry) => Math.max(latest, entry.t), 0);
  if (!hub.some((entry) => entry.hub === "open")) reasons.push("the hub never connected");
  if (closes.length > maxDisconnects) reasons.push(`the hub disconnected ${closes.length} times, more than ${maxDisconnects}`);
  for (const close of closes) {
    const reopened = hub.find((entry) => entry.hub === "open" && entry.t > close.t);
    if (reopened ? reopened.t - close.t > RECONNECT_WITHIN_MS : last - close.t > RECONNECT_WITHIN_MS) {
      reasons.push(`the hub did not reconnect within 10 s of ${new Date(close.t).toISOString()}`);
    }
  }
  return reasons;
}

function verdict(opts) {
  if (!opts.log) throw new Error("verdict needs --log");
  const floor = Number(opts.floor ?? 10);
  const maxDisconnects = Number(opts["max-disconnects"] ?? 2);
  const entries = fs.readFileSync(opts.log, "utf8").split("\n").filter(Boolean).map((line) => JSON.parse(line));

  const answers = entries.filter((entry) => entry.path !== undefined);
  const failures = answers.filter((entry) => entry.status !== 200);
  const windows = readWindows(opts.windows);
  const closes = entries.filter((entry) => entry.hub === "close");
  const reasons = [
    ...answerReasons(answers, failures),
    ...windowReasons(answers, windows, floor),
    ...hubReasons(entries, closes, maxDisconnects),
  ];

  console.log(`probe: ${answers.length} answers, ${failures.length} failures, ${closes.length} hub disconnects, ${windows.length} windows`);
  if (reasons.length > 0) {
    for (const reason of reasons) console.log(`  red: ${reason}`);
    process.exit(1);
  }
  console.log("probe: green");
}

const [mode, ...rest] = process.argv.slice(2);
try {
  if (mode === "run") run(options(rest));
  else if (mode === "verdict") verdict(options(rest));
  else throw new Error("usage: rollout-probe.mjs run ... | verdict ...");
} catch (error) {
  console.error(`rollout-probe: ${error.message}`);
  process.exit(2);
}
