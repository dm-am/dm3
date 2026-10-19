/**
 * @vitest-environment jsdom
 */

/**
 * "Личные настройки" of the account page: three values read from the server
 * and saved with one button. The form is drawn only once they are read, so no
 * default stands as if saved; the theme saved here is the start of a new
 * device and leaves this one's alone; the button is what says the save went
 * through, so the tests press on how it could lie.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { personalApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { Theme, type Preferences } from "@/shared/api/models/personal";
import { useUiStore } from "@/shared/stores/ui";
import {
  accountTimeZone,
  setAccountTimeZone,
} from "@/shared/lib/utils/timeZone";
import AccountSettingsSection from "./AccountSettingsSection.vue";

/** The account refresh is stubbed: the lists read the size off the viewer it brings. */
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  signIn: vi.fn(),
  completeSecondFactor: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

const ZONE = "Europe/Moscow";

/** What the server holds for the account, none of it the default. */
const SAVED: Preferences = {
  theme: Theme.Dark,
  pageSize: 50,
  timeZoneId: ZONE,
};

type Read = { data: unknown; error: unknown };

const answered = (preferences: Preferences = SAVED): Read => ({
  data: { resource: preferences },
  error: null,
});

const failed = (): Read => ({
  data: null,
  error: { status: 500, title: "Ошибка сервера", type: "", traceId: "" },
});

/** Mounts the block, the read answering with `reads` in turn. */
async function render(...reads: Read[]) {
  const pinia = createPinia();
  setActivePinia(pinia);

  const read = vi.spyOn(personalApi, "getMyPreferences");
  for (const answer of reads.length ? reads : [answered()])
    read.mockResolvedValueOnce(answer as never);
  const update = vi
    .spyOn(personalApi, "updateMyPreferences")
    .mockResolvedValue({ data: {}, error: null } as never);

  const wrapper = mount(AccountSettingsSection, {
    global: { plugins: [pinia] },
  });
  await flushPromises();
  return { wrapper, update, read };
}

/** The control the form is saved with. */
const saveButton = (wrapper: VueWrapper) => wrapper.get("button.save-button");

/** The value a select of the form stands at. */
const valueOf = (wrapper: VueWrapper, id: string) =>
  (wrapper.get(`#${id}`).element as HTMLSelectElement).value;

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

/** Presses the save and lets the request and the refresh settle. */
async function press(wrapper: VueWrapper) {
  await saveButton(wrapper).trigger("click");
  await flushPromises();
}

beforeEach(() => {
  const { toasts, dismiss } = useToast();
  for (const toast of [...toasts.value]) dismiss(toast.id);
  vi.restoreAllMocks();
  fetchUser.mockReset();
  localStorage.clear();
});

describe("the fields of the personal settings", () => {
  it("are titled and captioned as the reader reads them", async () => {
    const { wrapper } = await render();

    expect(wrapper.get("h2").text()).toBe("Личные настройки");
    expect(
      wrapper.findAll(".form-field-label label").map((label) => label.text()),
    ).toEqual([
      "Сколько показывать на странице",
      "Часовой пояс",
      "Тема на новом устройстве",
    ]);
    expect(
      wrapper.findAll(".form-field-hint").map((hint) => hint.text()),
    ).toEqual([
      "Посты в комнате, комментарии, топики, сообщения в переписке и строки списков: игры, блоги, пользователи, пульс, уведомления",
      "По нему показываются даты и время на сайте и в письмах",
      "Тема этого устройства, верстка и перенос слов меняются в настройках сайта у шестеренки",
    ]);
    // The participation in the rating is the profile's: what others see of the owner.
    expect(wrapper.text()).not.toContain("Участие в рейтинге");
    expect(wrapper.find('input[type="checkbox"]').exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Цветовая схема");
    expect(wrapper.text()).not.toContain("Размер страниц");
  });

  it("offers the page sizes the server takes", async () => {
    const { wrapper } = await render();

    expect(
      wrapper
        .findAll("#account-page-size option")
        .map((option) => option.text()),
    ).toEqual(["5", "10", "20", "30", "40", "50", "100", "200"]);
  });

  it("stand at what the server holds", async () => {
    const { wrapper } = await render();

    expect(valueOf(wrapper, "account-page-size")).toBe("50");
    expect(valueOf(wrapper, "account-time-zone")).toBe(ZONE);
    expect(valueOf(wrapper, "account-theme")).toBe(Theme.Dark);
  });
});

describe("the read of the personal settings", () => {
  it("draws no field and no save until the server answers", async () => {
    setActivePinia(createPinia());
    vi.spyOn(personalApi, "getMyPreferences").mockReturnValue(
      new Promise(() => {}) as never,
    );

    const wrapper = mount(AccountSettingsSection);
    await flushPromises();

    expect(wrapper.find(".account-skeleton").exists()).toBe(true);
    expect(wrapper.find("select").exists()).toBe(false);
    expect(wrapper.find("button.save-button").exists()).toBe(false);
  });

  it("says a failed read instead of the form, and reads again on Повторить", async () => {
    const { wrapper, read } = await render(failed(), answered());

    expect(wrapper.find("select").exists()).toBe(false);
    expect(wrapper.get(".error-state").text()).toContain("Ошибка сервера");

    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(read).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(valueOf(wrapper, "account-page-size")).toBe("50");
  });
});

describe("the save of the personal settings", () => {
  it("offers a save while nothing has been saved yet", async () => {
    const { wrapper } = await render();

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("sends the three values and says it worked on the button and nowhere else", async () => {
    const { wrapper, update } = await render();

    await wrapper.get("#account-page-size").setValue("10");
    await press(wrapper);

    expect(update).toHaveBeenCalledWith({
      theme: Theme.Dark,
      pageSize: 10,
      timeZoneId: ZONE,
    });
    expect(saveButton(wrapper).text()).toBe("Сохранено");
    expect(plates()).toEqual([]);
  });

  it("reads the viewer again, so the lists open with the new size", async () => {
    const { wrapper } = await render();

    await wrapper.get("#account-page-size").setValue("10");
    await press(wrapper);

    expect(fetchUser).toHaveBeenCalledTimes(1);
  });

  // The theme of this device changes at the gear; the account's is only where
  // a new device starts.
  it("leaves the theme of this device as it is", async () => {
    const { wrapper } = await render();
    useUiStore().updateTheme(Theme.Light);

    await wrapper.get("#account-theme").setValue(Theme.Dark);
    await press(wrapper);

    expect(useUiStore().theme).toBe(Theme.Light);
  });

  it("takes no further press while it reads Сохранено", async () => {
    const { wrapper, update } = await render();
    await wrapper.get("#account-page-size").setValue("10");
    await press(wrapper);

    await press(wrapper);

    expect(saveButton(wrapper).attributes("disabled")).toBeDefined();
    expect(update).toHaveBeenCalledTimes(1);
  });

  it("offers the save again as soon as a field changes", async () => {
    const { wrapper, update } = await render();
    await wrapper.get("#account-page-size").setValue("10");
    await press(wrapper);

    await wrapper.get("#account-theme").setValue(Theme.Light);

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();

    await press(wrapper);
    expect(update).toHaveBeenCalledTimes(2);
  });

  it("keeps offering the save when the server refused it", async () => {
    const { wrapper, update } = await render();
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "Недопустимое число на странице" },
    } as never);

    await wrapper.get("#account-page-size").setValue("10");
    await press(wrapper);

    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
    expect(wrapper.get(".action-result").text()).toBe(
      "Недопустимое число на странице",
    );
    expect(fetchUser).not.toHaveBeenCalled();
  });

  /** A refusal is said in the line beside the button, which offers the save again. */
  it("says a 500 beside the button", async () => {
    const { wrapper, update } = await render();
    update.mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    } as never);

    await wrapper.get("#account-page-size").setValue("10");
    await press(wrapper);

    expect(wrapper.get(".action-result").text()).toBe("Ошибка сервера");
    expect(saveButton(wrapper).text()).toBe("Сохранить");
    expect(saveButton(wrapper).attributes("disabled")).toBeUndefined();
  });
});

/** The zone draws every date of the site, so a save hands it over at once. */
describe("the zone of the account", () => {
  beforeEach(() => {
    setAccountTimeZone(ZONE);
  });

  it("is applied to the site as soon as it is saved", async () => {
    const { wrapper } = await render();

    await wrapper.get("#account-time-zone").setValue("Asia/Tokyo");
    await press(wrapper);

    expect(accountTimeZone.value).toBe("Asia/Tokyo");
  });

  it("is left as it was when the save was refused", async () => {
    const { wrapper, update } = await render();
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "Неизвестный часовой пояс" },
    } as never);

    await wrapper.get("#account-time-zone").setValue("Asia/Tokyo");
    await press(wrapper);

    expect(accountTimeZone.value).toBe(ZONE);
  });
});
