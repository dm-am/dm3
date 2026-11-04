/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import type { PostAttachment } from "@/entities/game";
import type { UploadAddressee } from "@/shared/api/models/common/upload";
import {
  everyReader,
  type AudienceCandidate,
  type PendingAttachment,
} from "../model/attachmentAudience";
import AttachmentsEditor from "./AttachmentsEditor.vue";

enableAutoUnmount(afterEach);

const held = (
  id: string,
  fileName: string,
  over: Partial<PostAttachment> = {},
) =>
  ({
    id,
    fileName,
    contentType: "image/png",
    sizeBytes: 2048,
    url: `/v1/uploads/${id}/content`,
    ...over,
  }) as PostAttachment;

const VASYA = { id: "c-vasya", name: "Вася" };
// A character the mark keeps who is no longer let into the room.
const GONE = { id: "c-gone", name: "Ушедший" };

const map = held("a-1", "Карта.png");
const plan = held("a-2", "План.png", {
  recipients: "DungeonMaster, Assistant, Вася",
  addressees: [VASYA],
});

const CANDIDATES = [VASYA, { id: "c-petya", name: "Петя" }];

function editor(
  props: {
    attachments?: PostAttachment[];
    busy?: boolean;
    isAuthor?: boolean;
    full?: boolean;
    pending?: PendingAttachment[];
    candidates?: readonly AudienceCandidate[];
  } = {},
) {
  return mount(AttachmentsEditor, {
    attachTo: document.body,
    props: {
      attachments: [map, plan],
      pending: [],
      busy: false,
      isAuthor: true,
      full: false,
      error: null,
      candidates: CANDIDATES,
      ...props,
    },
  });
}

const who = (wrapper: ReturnType<typeof editor>) =>
  wrapper.findAll(".attachment-audience");
const panel = (wrapper: ReturnType<typeof editor>) =>
  wrapper.find("[role=dialog]");
const done = (wrapper: ReturnType<typeof editor>) =>
  panel(wrapper)
    .findAll("button")
    .find((b) => b.text() === "Готово")!
    .trigger("click");

const picked = (name: string): PendingAttachment => ({
  file: new File([""], name, { type: "image/png" }),
  audience: everyReader(),
});

describe("the files of an edit", () => {
  it("hands the host the files picked and empties the picker", async () => {
    const wrapper = editor();
    const input = wrapper.get<HTMLInputElement>(".attachment-input").element;
    const chosen = [
      new File([""], "one.png", { type: "image/png" }),
      new File([""], "two.png", { type: "image/png" }),
    ];
    // jsdom lets no script choose files: the list is laid on the element.
    let emptiedTo: string | undefined;
    Object.defineProperty(input, "files", { value: chosen });
    Object.defineProperty(input, "value", {
      get: () => "C:\fakepathone.png",
      set: (value: string) => (emptiedTo = value),
    });

    await wrapper.get(".attachment-input").trigger("change");

    expect(wrapper.emitted("choose")).toEqual([[chosen]]);
    expect(emptiedTo).toBe("");
  });

  it("hands the host the file whose убрать was pressed", async () => {
    const wrapper = editor();

    await wrapper.findAll(".attachment-remove")[1].trigger("click");

    expect(wrapper.emitted("remove")).toEqual([[plan]]);
  });

  it("offers Прикрепить файл and who sees a file only to the author", () => {
    const author = editor({ isAuthor: true });
    expect(author.get(".attachment-add").text()).toBe("Прикрепить файл");
    expect(
      author.findAll(".attachment-line").map((line) => line.text()),
    ).toEqual([
      "Карта.png 2,0 КБ | видят все | убрать",
      "План.png 2,0 КБ | видят: Вася | убрать",
    ]);

    const other = editor({ isAuthor: false });
    expect(other.find(".attachment-add").exists()).toBe(false);
    expect(other.text()).not.toContain("Прикрепить файл");
    expect(other.find(".attachment-audience").exists()).toBe(false);
    expect(
      other.findAll(".attachment-line").map((line) => line.text()),
    ).toEqual(["Карта.png 2,0 КБ | убрать", "План.png 2,0 КБ | убрать"]);
  });

  it("holds the picker, убрать and who sees while a request is on its way", () => {
    const wrapper = editor({ busy: true, pending: [picked("new.png")] });

    expect(wrapper.get(".attachment-input").attributes()).toHaveProperty(
      "disabled",
    );
    for (const cross of wrapper.findAll(".attachment-remove"))
      expect(cross.attributes()).toHaveProperty("disabled");
    expect(who(wrapper)).toHaveLength(3);
    for (const word of who(wrapper))
      expect(word.attributes()).toHaveProperty("disabled");
  });

  it("holds the picker, and not the crosses, at the limit", () => {
    const full = editor({ full: true });
    expect(full.get(".attachment-input").attributes()).toHaveProperty(
      "disabled",
    );
    for (const cross of full.findAll(".attachment-remove"))
      expect(cross.attributes()).not.toHaveProperty("disabled");

    const open = editor();
    expect(open.get(".attachment-input").attributes()).not.toHaveProperty(
      "disabled",
    );
  });
});

describe("who sees a file of an edit", () => {
  it("shows an attached file as the server marked it", async () => {
    const wrapper = editor();

    expect(who(wrapper).map((word) => word.text())).toEqual([
      "видят все",
      "видят: Вася",
    ]);
    await who(wrapper)[1].trigger("click");
    const boxes = panel(wrapper).findAll<HTMLInputElement>("input");
    expect(boxes.map((box) => box.element.checked)).toEqual([true, false]);
  });

  // The hidden block's mark: green, its readers in the tooltip, the server's words.
  it("names a file kept to some readers in the green of a hidden block", () => {
    const wrapper = editor({ isAuthor: false });
    const [mapName, planName] = wrapper.findAll(".attachment-name");

    expect(mapName.classes()).not.toContain("private-message");
    expect(mapName.attributes("href")).toMatch(/\/v1\/uploads\/a-1\/content$/);
    expect(planName.classes()).toContain("private-message");
    expect(planName.attributes("data-recipients")).toBe(
      "DungeonMaster, Assistant, Вася",
    );
  });

  it("hands the host a changed mark of an attached file by Готово", async () => {
    const wrapper = editor();

    await who(wrapper)[1].trigger("click");
    await panel(wrapper).findAll("input")[0].setValue(false);
    await done(wrapper);

    expect(wrapper.emitted("mark")).toEqual([[plan, everyReader()]]);
  });

  // Against the boxes the panel opened with, the one gone from the room among them.
  it.each([[[VASYA]], [[GONE, VASYA]], [[GONE]]])(
    "hands the host nothing for Готово that changed nothing, the mark naming %j",
    async (addressees: UploadAddressee[]) => {
      const wrapper = editor({
        attachments: [held("a-3", "Схема.png", { addressees })],
      });

      await who(wrapper)[0].trigger("click");
      await done(wrapper);

      expect(wrapper.emitted("mark")).toBeUndefined();
      expect(panel(wrapper).exists()).toBe(false);
    },
  );

  // A refused mark: the server did not change the file, so the line still says what it did.
  it("keeps the line as the host keeps the file", async () => {
    const wrapper = editor();

    await who(wrapper)[0].trigger("click");
    await panel(wrapper).findAll("input")[1].setValue(true);
    await done(wrapper);

    expect(wrapper.emitted("mark")).toEqual([
      [map, { visibleTo: "Selected", characterIds: ["c-petya"] }],
    ]);
    expect(who(wrapper)[0].text()).toBe("видят все");
  });

  it("keeps a file chosen in the edit with its mark until Сохранить", async () => {
    const wrapper = editor({ pending: [picked("new.png")] });

    expect(wrapper.findAll(".attachment-line")[2].text()).toBe(
      "new.png 0 Б | видят все | убрать",
    );
    expect(
      wrapper.findAll(".attachment-name")[2].attributes("href"),
    ).toBeUndefined();
    await who(wrapper)[2].trigger("click");
    await panel(wrapper).findAll("input")[1].setValue(true);
    await done(wrapper);
    await wrapper.findAll(".attachment-remove")[2].trigger("click");

    expect(wrapper.emitted("markPending")).toEqual([
      [0, { visibleTo: "Selected", characterIds: ["c-petya"] }],
    ]);
    expect(wrapper.emitted("unchoose")).toEqual([[0]]);
  });
});

describe("who sees a file marked for a character gone from the room", () => {
  // The server's words, leads first, then the mark as it froze them.
  const scheme = held("a-3", "Схема.png", {
    recipients: "DungeonMaster, Assistant, Вася, Ушедший",
    addressees: [VASYA, GONE],
  });

  const boxes = (wrapper: ReturnType<typeof editor>) =>
    panel(wrapper).findAll<HTMLInputElement>("input");

  it("checks him by the name of the mark, and every line names him", async () => {
    const wrapper = editor({ attachments: [scheme] });

    expect(who(wrapper)[0].text()).toBe("видят: Вася, Ушедший");
    expect(wrapper.get(".attachment-name").attributes("data-recipients")).toBe(
      "DungeonMaster, Assistant, Вася, Ушедший",
    );
    await who(wrapper)[0].trigger("click");
    expect(
      panel(wrapper)
        .findAll("label")
        .map((l) => l.text()),
    ).toEqual(["Вася", "Петя", "Ушедший"]);
    expect(boxes(wrapper).map((box) => box.element.checked)).toEqual([
      true,
      false,
      true,
    ]);
    expect(panel(wrapper).get(".audience-recipients").text()).toBe(
      "Получатели: DungeonMaster, Assistant, Вася, Ушедший",
    );
  });

  it("hands the host the mark without him once his box is unchecked", async () => {
    const wrapper = editor({ attachments: [scheme] });

    await who(wrapper)[0].trigger("click");
    await boxes(wrapper)[2].setValue(false);
    await done(wrapper);

    expect(wrapper.emitted("mark")).toEqual([
      [scheme, { visibleTo: "Selected", characterIds: ["c-vasya"] }],
    ]);
  });

  it("keeps him on the mark while another box changes", async () => {
    const wrapper = editor({ attachments: [scheme] });

    await who(wrapper)[0].trigger("click");
    await boxes(wrapper)[1].setValue(true);
    await done(wrapper);

    expect(wrapper.emitted("mark")).toEqual([
      [
        scheme,
        {
          visibleTo: "Selected",
          characterIds: ["c-vasya", "c-petya", "c-gone"],
        },
      ],
    ]);
  });

  // In a room with nobody else to check, his box is the way back to every reader.
  it("gives the file back to every reader when his box was the last", async () => {
    const note = held("a-4", "Записка.png", {
      recipients: "DungeonMaster, Assistant, Ушедший",
      addressees: [GONE],
    });
    const wrapper = editor({ attachments: [note], candidates: [] });

    expect(who(wrapper)[0].text()).toBe("видят: Ушедший");
    await who(wrapper)[0].trigger("click");
    await boxes(wrapper)[0].setValue(false);
    expect(panel(wrapper).find(".audience-recipients").exists()).toBe(false);
    await done(wrapper);

    expect(wrapper.emitted("mark")).toEqual([[note, everyReader()]]);
  });
});
