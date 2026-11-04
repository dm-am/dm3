// How the frame stand stops: a refusal says what is wrong and ends the run
// with 2 (visual.mjs says it at the end of main()), and a step that runs a
// command refuses when the command fails.

import { spawnSync } from "node:child_process";

/** A refusal: said at the end of main(), and the run ends with 2. */
export class Refusal extends Error {}

export function fail(message) {
  throw new Refusal(message);
}

export function attempt(command, args, cwd) {
  console.log(`> ${command} ${args.join(" ")}`);
  return spawnSync(command, args, { cwd, stdio: "inherit" }).status;
}

export function run(command, args, cwd) {
  const status = attempt(command, args, cwd);
  if (status !== 0) fail(`${command} ${args.join(" ")} exited with ${status}`);
}
