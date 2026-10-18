/**
 * @vitest-environment node
 */

/**
 * One refusal, one carrier.
 *
 * The poll form has a line of its own and the response interceptor has a plate.
 * For a refused action, a rate limit, a dead server, a dead connection or an
 * expired session the plate speaks and the form keeps quiet — before this the
 * 500 stood in both places at once. The form still answers for what was typed
 * into it: a rejected pair of dates is its own sentence, and the server's title
 * is read out for anything else it names.
 */

// The device is in Moscow for the file, set before anything reads the clock:
// the fields of the form are filled in on the site's clock, and a reader whose
// account is in Tokyo is only told apart from the device on a device elsewhere.
const MACHINE_ZONE = process.env.TZ;
process.env.TZ = "Europe/Moscow";

import {
  describe,
  it,
  expect,
  vi,
  beforeEach,
  afterEach,
  afterAll,
} from "vitest";
import { setAccountTimeZone } from "@/shared/lib/utils/timeZone";

const { createPoll } = vi.hoisted(() => ({ createPoll: vi.fn() }));

vi.mock("@/entities/poll", () => ({
  usePollsStore: () => ({ createPoll }),
}));

import { useCreatePoll } from "./useCreatePoll";

/** An open form, filled in well enough to reach the server. */
async function submitted() {
  const form = useCreatePoll();
  form.toggleForm();
  form.pollTitle.value = "Любимый модуль года";
  form.pollOptions.value[0].text = "Первый";
  form.pollOptions.value[1].text = "Второй";
  await form.submitPoll();
  return form;
}

describe("useCreatePoll", () => {
  beforeEach(() => createPoll.mockReset());

  // The statuses the response interceptor used to plate stand in the form's
  // line now, and the plate is taken down (refusalHere; the owner's rule of
  // 26 September 2026).
  it.each([
    [403, "Недостаточно прав для этого действия"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Недостаточно прав для этого действия"],
    [503, "Недостаточно прав для этого действия"],
  ])("says a failure of status %i in the form", async (status, line) => {
    createPoll.mockResolvedValue({
      data: null,
      error: { status, title: "Недостаточно прав для этого действия" },
    });

    const form = await submitted();

    expect(form.errorMessage.value).toBe(line);
    expect(form.formExpanded.value).toBe(true);
    expect(form.pollTitle.value).toBe("Любимый модуль года");
  });

  // An expired session and a dead connection keep the plate.
  it.each([0, 401])(
    "leaves a failure of status %i to the interceptor",
    async (status) => {
      createPoll.mockResolvedValue({
        data: null,
        error: { status, title: "Недостаточно прав для этого действия" },
      });

      const form = await submitted();

      expect(form.errorMessage.value).toBe("");
      // Silent about the failure, not about the attempt: the poll was not
      // created, so the form stays open with everything that was typed in it.
      expect(form.formExpanded.value).toBe(true);
      expect(form.pollTitle.value).toBe("Любимый модуль года");
    },
  );

  it("keeps its own sentence for a rejected pair of dates", async () => {
    createPoll.mockResolvedValue({
      data: null,
      error: { status: 400, title: "Validation failed" },
    });

    const form = await submitted();

    expect(form.errorMessage.value).toBe("Начало должно быть раньше окончания");
  });

  it("reads out a refusal the interceptor says nothing about", async () => {
    createPoll.mockResolvedValue({
      data: null,
      error: { status: 409, title: "Такой опрос уже идет" },
    });

    const form = await submitted();

    expect(form.errorMessage.value).toBe("Такой опрос уже идет");
  });

  it("clears the form when the poll was created", async () => {
    createPoll.mockResolvedValue({ data: { id: "p-1" }, error: null });

    const form = await submitted();

    expect(form.errorMessage.value).toBe("");
    expect(form.pollTitle.value).toBe("");
    expect(form.formExpanded.value).toBe(false);
  });
});

describe("useCreatePoll dates", () => {
  beforeEach(() => {
    createPoll.mockReset();
    createPoll.mockResolvedValue({ data: { id: "p-1" }, error: null });
  });

  afterEach(() => {
    vi.useRealTimers();
    setAccountTimeZone(null);
  });

  afterAll(() => {
    if (MACHINE_ZONE === undefined) delete process.env.TZ;
    else process.env.TZ = MACHINE_ZONE;
  });

  // The card of the poll prints its dates by the account's clock; the form
  // the dates were typed into has to read them by the same one.
  it("sends 20:00 typed by a reader in Tokyo on a device in Moscow as 20:00 in Tokyo", async () => {
    // Before the start typed: a start the clock has passed goes out as now.
    vi.useFakeTimers({ now: new Date("2026-09-27T12:00:00Z") });
    setAccountTimeZone("Asia/Tokyo");
    const form = useCreatePoll();
    form.pollTitle.value = "Любимый модуль года";
    form.pollOptions.value[0].text = "Первый";
    form.pollOptions.value[1].text = "Второй";
    form.pollStartsUtc.value = "2026-10-01T20:00";
    form.pollEndsUtc.value = "2026-10-08T20:00";

    await form.submitPoll();

    expect(createPoll).toHaveBeenCalledWith(
      expect.objectContaining({
        startsUtc: "2026-10-01T11:00:00.000Z",
        endsUtc: "2026-10-08T11:00:00.000Z",
      }),
    );
  });

  it("opens on the account's clock", () => {
    vi.useFakeTimers({ now: new Date("2026-09-27T12:00:00Z") });
    setAccountTimeZone("Asia/Tokyo");

    const form = useCreatePoll();

    expect(form.pollStartsUtc.value).toBe("2026-09-27T21:00");
    expect(form.pollEndsUtc.value).toBe("2026-10-04T21:00");
  });

  it("asks for a cleared date instead of hanging the form", async () => {
    const form = useCreatePoll();
    form.pollTitle.value = "Любимый модуль года";
    form.pollOptions.value[0].text = "Первый";
    form.pollOptions.value[1].text = "Второй";
    form.pollStartsUtc.value = "";

    await form.submitPoll();

    expect(createPoll).not.toHaveBeenCalled();
    expect(form.errorMessage.value).toBe("Укажите начало и окончание опроса");
    expect(form.isSubmitting.value).toBe(false);
  });
});

/**
 * The bounds of a new poll are the server's (CreatePollValidator): it starts
 * no earlier than now, ends more than a day after it starts, and within a
 * year. The fields take nothing else and say why under them; the form does
 * not send while they say anything.
 */
describe("useCreatePoll bounds", () => {
  beforeEach(() => {
    createPoll.mockReset();
    createPoll.mockResolvedValue({ data: { id: "p-1" }, error: null });
    vi.useFakeTimers({ now: new Date("2026-10-06T02:30:00Z") });
    // The bounds are read on the site's clock, which for a signed-in reader is
    // the zone of the account. Pinned here rather than through the device: the
    // TZ of the file above is honoured by Date on this machine, but a vitest
    // worker on Linux keeps the zone it was started with, and CI runs in UTC.
    setAccountTimeZone("Europe/Moscow");
  });

  afterEach(() => {
    vi.useRealTimers();
    setAccountTimeZone(null);
  });

  const filled = () => {
    const form = useCreatePoll();
    form.pollTitle.value = "Любимый модуль года";
    form.pollOptions.value[0].text = "Первый";
    form.pollOptions.value[1].text = "Второй";
    return form;
  };

  it("offers now as the start, and takes no earlier one", () => {
    const form = filled();
    // Moscow, the zone of the account.
    expect(form.pollStartsUtc.value).toBe("2026-10-06T05:30");
    expect(form.startsMin.value).toBe("2026-10-06T05:30");
  });

  it("asks the end to come more than a day after the start, within a year", () => {
    const form = filled();
    form.pollStartsUtc.value = "2026-10-10T12:00";
    expect(form.endsMin.value).toBe("2026-10-11T12:01");
    expect(form.endsMax.value).toBe("2027-10-06T05:29");
  });

  it("does not send while a field says what is wrong with it", async () => {
    const form = filled();
    form.endsReason.value = "Не раньше 07.10.2026 05:31";

    await form.submitPoll();

    expect(createPoll).not.toHaveBeenCalled();
    expect(form.errorMessage.value).toBe("Проверьте начало и окончание опроса");
  });

  it("sends the start it offered as the moment the form is sent", async () => {
    // The field is read to the minute and the server takes no start before
    // its own clock: by the time the form is sent, the minute it offered is
    // behind.
    const form = filled();
    vi.setSystemTime(new Date("2026-10-06T02:41:17Z"));

    await form.submitPoll();

    expect(createPoll).toHaveBeenCalledWith(
      expect.objectContaining({ startsUtc: "2026-10-06T02:41:17.000Z" }),
    );
  });

  it("sends a start ahead as it was typed", async () => {
    const form = filled();
    form.pollStartsUtc.value = "2026-10-07T09:00";

    await form.submitPoll();

    expect(createPoll).toHaveBeenCalledWith(
      expect.objectContaining({ startsUtc: "2026-10-07T06:00:00.000Z" }),
    );
  });
});
