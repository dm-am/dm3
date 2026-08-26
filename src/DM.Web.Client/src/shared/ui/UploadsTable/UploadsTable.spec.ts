/**
 * @vitest-environment jsdom
 */

/**
 * One table draws the uploads of an owner and the uploads of the whole site.
 *
 * "Загруженное" and "Все загруженные файлы" carried a byte-identical copy of
 * five cells each — preview, file, date, size, delete — and of the delete call
 * behind them. They now share this component, and the rows below are what the
 * two pages rendered before the merge, character for character: a merge of
 * visible markup is not proved by a test that only calls the code behind it.
 *
 * Both call shapes are mounted, because that is where the pages legitimately
 * differ: the moderation one adds a "Загрузил" column it fills itself and
 * counts 25 rows to a page against the profile's 20. The layer rules keep the
 * two pages out of one spec (a shared component may not import a page, and one
 * page may not import another), so the column sets are spelled out here as the
 * pages spell them.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import type { Upload } from "@/shared/api/models/common/upload";
import type { Column } from "@/shared/ui/DataTable";
import {
  uploadPreviewColumn,
  uploadFileColumn,
} from "@/shared/lib/utils/upload";
import UploadsTable from "./UploadsTable.vue";

/** The column set of pages/profile/ProfileUploadsPage.vue. */
const PROFILE_COLUMNS: Column[] = [
  uploadPreviewColumn,
  uploadFileColumn,
  { key: "date", label: "Дата", width: "16%" },
  { key: "size", label: "Размер", width: "14%", align: "right" },
  { key: "actions", label: "", width: "14%", align: "center" },
];

/** The column set of pages/moderation/ModerationUploads.vue. */
const MODERATION_COLUMNS: Column[] = [
  uploadPreviewColumn,
  uploadFileColumn,
  { key: "uploader", label: "Загрузил", width: "18%" },
  { key: "date", label: "Дата", width: "14%", hideOnMobile: true },
  { key: "size", label: "Размер", width: "12%", align: "right" },
  { key: "actions", label: "", width: "12%", align: "center" },
];

const image: Upload = {
  id: "u-1",
  userId: "user-1",
  type: "Avatar",
  originalFileName: "portrait.png",
  contentType: "image/png",
  sizeBytes: 204800,
  status: "Confirmed",
  url: "https://files.example/portrait.png",
  contentUrl: "/v1/uploads/u-1/content",
  createdUtc: "2026-05-01T12:00:00",
};

const archive: Upload = {
  ...image,
  id: "u-2",
  originalFileName: "archive.zip",
  contentType: "application/zip",
  url: "https://files.example/archive.zip",
};

/**
 * The markup as this file reads it: scope ids are compile-time tokens (the
 * styles moved with the cells they paint), and the indentation is the test
 * renderer's pretty-printing rather than anything in the DOM.
 */
const markup = (html: string) =>
  html
    .replace(/ data-v-[0-9a-f]+=""/g, "")
    .split("\n")
    .map((line) => line.trim())
    .join("\n");

function render(columns: Column[], withUploader = false) {
  return mount(UploadsTable, {
    props: { columns, uploads: [image, archive], loading: false },
    slots: withUploader
      ? { "cell-uploader": '<span class="who">кто-то</span>' }
      : {},
    global: {
      stubs: {
        RouterLink: { props: ["to"], template: "<a><slot /></a>" },
      },
    },
  });
}

describe("UploadsTable", () => {
  it("draws the owner's row as his page had it", () => {
    const wrapper = render(PROFILE_COLUMNS);

    expect(markup(wrapper.findAll("tr.table-row")[0].html())).toBe(
      [
        '<tr class="table-row">',
        "<!--v-if-->",
        '<td class="col col-preview align-center"><img src="https://files.example/portrait.png" alt="portrait.png" class="upload-thumb" loading="lazy"></td>',
        '<td class="col col-file align-left"><a href="https://files.example/portrait.png" target="_blank" rel="noopener" class="upload-name">portrait.png</a></td>',
        '<td class="col col-date align-left">01.05.2026</td>',
        '<td class="col col-size align-right">200.0 КБ</td>',
        '<td class="col col-actions align-center"><button type="button" class="delete-button"> Удалить </button></td>',
        "</tr>",
      ].join("\n"),
    );
  });

  it("draws a file that is not an image by its extension", () => {
    const wrapper = render(PROFILE_COLUMNS);

    expect(markup(wrapper.findAll("tr.table-row")[1].html())).toBe(
      [
        '<tr class="table-row">',
        "<!--v-if-->",
        '<td class="col col-preview align-center"><span class="upload-ext">ZIP</span></td>',
        '<td class="col col-file align-left"><a href="https://files.example/archive.zip" target="_blank" rel="noopener" class="upload-name">archive.zip</a></td>',
        '<td class="col col-date align-left">01.05.2026</td>',
        '<td class="col col-size align-right">200.0 КБ</td>',
        '<td class="col col-actions align-center"><button type="button" class="delete-button"> Удалить </button></td>',
        "</tr>",
      ].join("\n"),
    );
  });

  it("draws the moderation row with the column that page adds", () => {
    const wrapper = render(MODERATION_COLUMNS, true);

    expect(markup(wrapper.findAll("tr.table-row")[0].html())).toBe(
      [
        '<tr class="table-row">',
        "<!--v-if-->",
        '<td class="col col-preview align-center"><img src="https://files.example/portrait.png" alt="portrait.png" class="upload-thumb" loading="lazy"></td>',
        '<td class="col col-file align-left"><a href="https://files.example/portrait.png" target="_blank" rel="noopener" class="upload-name">portrait.png</a></td>',
        '<td class="col col-uploader align-left"><span class="who">кто-то</span></td>',
        '<td class="col col-date align-left hide-mobile">01.05.2026</td>',
        '<td class="col col-size align-right">200.0 КБ</td>',
        '<td class="col col-actions align-center"><button type="button" class="delete-button"> Удалить </button></td>',
        "</tr>",
      ].join("\n"),
    );
  });

  it("has no uploader cell where the page asks for no such column", () => {
    const wrapper = render(PROFILE_COLUMNS);

    expect(wrapper.find("td.col-uploader").exists()).toBe(false);
  });

  it("names the row the delete button was pressed on", async () => {
    const wrapper = render(PROFILE_COLUMNS);

    await wrapper.findAll("button.delete-button")[1].trigger("click");

    expect(wrapper.emitted("remove")).toEqual([[archive]]);
  });

  it("says the same thing on both pages when there is nothing to show", () => {
    const wrapper = mount(UploadsTable, {
      props: { columns: PROFILE_COLUMNS, uploads: [], loading: false },
    });

    expect(wrapper.find(".table-empty-row").text()).toBe(
      "Загруженных файлов пока нет",
    );
    expect(wrapper.find("table").attributes("aria-label")).toBe(
      "Загруженные файлы",
    );
  });
});
