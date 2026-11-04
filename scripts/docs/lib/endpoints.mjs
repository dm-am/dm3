// The server's endpoints: artifacts/openapi-routes.json, the committed snapshot
// the API integration suite writes from the running application
// (OpenApiContractShould) and keeps equal to it. The per-module documents under
// artifacts/openapi/ hold the same routes but are not committed, so a count
// from them would differ between a clean clone and a machine that ran the suite.
import { join } from "node:path";
import { ROOT, read } from "./repo.mjs";

const SNAPSHOT = join(ROOT, "artifacts", "openapi-routes.json");

export function readEndpoints() {
  return JSON.parse(read(SNAPSHOT)).map((line) => {
    const [method, path] = line.split(" ");
    return { method, path };
  });
}
