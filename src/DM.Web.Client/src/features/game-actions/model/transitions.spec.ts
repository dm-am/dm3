import { describe, it, expect } from "vitest";
import { ClosedReason, ModuleStatus } from "@/shared/api/models/common";
import {
  availableStatusTransitions,
  BLOG_STATUS_WORDS,
  GAME_STATUS_WORDS,
  type StatusMachineWords,
} from "./transitions";

/** What the machine offers, as the strip draws it: each label, a "!" after a dangerous one. */
const offered = (
  words: StatusMachineWords<string>,
  status: ModuleStatus | undefined,
  closedReason?: ClosedReason,
) =>
  availableStatusTransitions(words, status, closedReason).map(
    (o) => `${o.value} ${o.label}${o.danger ? " !" : ""}`,
  );

describe("availableStatusTransitions for a game", () => {
  const game = (status: ModuleStatus | undefined, reason?: ClosedReason) =>
    offered(GAME_STATUS_WORDS, status, reason);

  it("starts a draft", () => {
    expect(game(ModuleStatus.Draft)).toEqual(["Start Начать игру"]);
  });

  it("finishes, freezes or closes an active game, the closing asked first", () => {
    expect(game(ModuleStatus.Active)).toEqual([
      "Finish Завершить игру",
      "Freeze Заморозить игру",
      "Close Закрыть игру !",
    ]);
  });

  it("resumes a frozen game or closes it without reopening", () => {
    expect(game(ModuleStatus.Closed, ClosedReason.Frozen)).toEqual([
      "Reopen Возобновить игру",
      "Close Закрыть игру !",
    ]);
  });

  it.each([ClosedReason.Finished, ClosedReason.None, undefined])(
    "only re-opens a game closed for the reason %s",
    (reason) => {
      expect(game(ModuleStatus.Closed, reason)).toEqual([
        "Reopen Переоткрыть игру",
      ]);
    },
  );

  it("offers nothing when the status is unknown", () => {
    expect(game(undefined)).toEqual([]);
  });
});

// Finish is absent on purpose: the server would take it, the blog UI never asks for it.
describe("availableStatusTransitions for a blog", () => {
  const blog = (status: ModuleStatus | undefined, reason?: ClosedReason) =>
    offered(BLOG_STATUS_WORDS, status, reason);

  it("publishes a draft", () => {
    expect(blog(ModuleStatus.Draft)).toEqual(["Start Опубликовать блог"]);
  });

  it("freezes or closes an active blog, never finishes it, the closing asked first", () => {
    expect(blog(ModuleStatus.Active)).toEqual([
      "Freeze Заморозить блог",
      "Close Закрыть блог !",
    ]);
  });

  it("resumes a frozen blog or closes it without reopening", () => {
    expect(blog(ModuleStatus.Closed, ClosedReason.Frozen)).toEqual([
      "Reopen Возобновить блог",
      "Close Закрыть блог !",
    ]);
  });

  it.each([ClosedReason.Finished, ClosedReason.None, undefined])(
    "only re-opens a blog closed for the reason %s",
    (reason) => {
      expect(blog(ModuleStatus.Closed, reason)).toEqual([
        "Reopen Переоткрыть блог",
      ]);
    },
  );

  it("offers nothing when the status is unknown", () => {
    expect(blog(undefined)).toEqual([]);
  });
});
