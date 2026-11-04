// What a section's card says when the read of its state fails.
import { describe, expect, it } from "vitest";
import type { GeneralError } from "@/shared/api/models/common";
import { describeLoadFailure } from "./loadFailure";

const FALLBACK = "Не удалось загрузить раздел";

const failure = (status: number, retryAfterSeconds?: number): GeneralError => ({
  type: "about:blank",
  title: "Раздел недоступен",
  status,
  traceId: "trace",
  retryAfterSeconds,
});

describe("describeLoadFailure", () => {
  it("says the wait with its seconds, not the title, for a limit", () => {
    expect(describeLoadFailure(failure(429, 30), FALLBACK)).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("keeps to the fallback for an ended session, which the interceptor speaks for", () => {
    expect(describeLoadFailure(failure(401), FALLBACK)).toBe(FALLBACK);
  });

  it("keeps to the fallback for a request that never left", () => {
    expect(describeLoadFailure(failure(0), FALLBACK)).toBe(FALLBACK);
  });

  it("gives the server's sentence for any other refusal", () => {
    expect(describeLoadFailure(failure(500), FALLBACK)).toBe(
      "Раздел недоступен",
    );
  });
});
