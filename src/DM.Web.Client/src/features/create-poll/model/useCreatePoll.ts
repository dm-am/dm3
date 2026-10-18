import { computed, ref } from "vue";
import dayjs from "dayjs";
import { usePollsStore } from "@/entities/poll";
import { refusalHere } from "@/shared/lib/errors";
import { fromSiteInput, toSiteInput } from "@/shared/lib/utils/datetime";

export function useCreatePoll() {
  const pollsStore = usePollsStore();

  const formExpanded = ref(false);
  const formHovered = ref(false);

  // The moment the form was set out, on the site's clock: the start it offers,
  // and the earliest it takes.
  const openedAt = ref(toSiteInput());

  const pollTitle = ref("");
  const pollDetails = ref("");
  const pollStartsUtc = ref(openedAt.value);
  const pollEndsUtc = ref(toSiteInput(dayjs().add(7, "day")));
  const pollIsAnonymous = ref(true);

  // The server's rules for a new poll (CreatePollValidator): it starts no
  // earlier than now, it ends more than a day after it starts, and it ends
  // within a year. The fields take nothing else, and say why under them, so a
  // pair of dates the server would refuse is not sent to be refused.
  const startsMin = computed(() => openedAt.value);
  const endsMin = computed(() => {
    const starts = fromSiteInput(pollStartsUtc.value);
    return starts
      ? toSiteInput(dayjs(starts).add(1, "day").add(1, "minute"))
      : undefined;
  });
  const endsMax = computed(() =>
    toSiteInput(
      dayjs(fromSiteInput(openedAt.value))
        .add(365, "day")
        .subtract(1, "minute"),
    ),
  );

  // What the two fields say is wrong with them, shown under each.
  const startsReason = ref<string | null>(null);
  const endsReason = ref<string | null>(null);

  // Use objects with unique IDs for stable v-for keys
  let optionIdCounter = 0;
  const createOption = (text = "") => ({ id: ++optionIdCounter, text });
  const pollOptions = ref([createOption(), createOption()]);

  const isSubmitting = ref(false);
  const errorMessage = ref("");

  // The reveal itself is the global CSS-only .expand-fold (Reset.sass) —
  // an inline tool form: unified animation tempo, no registry
  // (UI_STANDARDS, Animation Standards).
  function toggleForm() {
    formExpanded.value = !formExpanded.value;
  }

  function addOption() {
    pollOptions.value.push(createOption());
  }

  function removeOption(id: number) {
    if (pollOptions.value.length > 2) {
      const index = pollOptions.value.findIndex((o) => o.id === id);
      if (index !== -1) pollOptions.value.splice(index, 1);
    }
  }

  function resetForm() {
    openedAt.value = toSiteInput();
    pollTitle.value = "";
    pollDetails.value = "";
    pollStartsUtc.value = openedAt.value;
    pollEndsUtc.value = toSiteInput(dayjs().add(7, "day"));
    pollIsAnonymous.value = true;
    optionIdCounter = 0;
    pollOptions.value = [createOption(), createOption()];
    errorMessage.value = "";
  }

  async function submitPoll() {
    if (!pollTitle.value.trim()) {
      errorMessage.value = "Введите название опроса";
      return;
    }

    const validOptions = pollOptions.value.filter((o) => o.text.trim());
    if (validOptions.length < 2) {
      errorMessage.value = "Нужно минимум 2 варианта ответа";
      return;
    }

    // The fields are filled in on the site's clock, the one the poll's card
    // prints its dates by (fromSiteInput). A cleared field has no moment; read
    // by `new Date` it threw past a raised isSubmitting and the form hung.
    const starts = fromSiteInput(pollStartsUtc.value);
    const endsUtc = fromSiteInput(pollEndsUtc.value);
    if (!starts || !endsUtc) {
      errorMessage.value = "Укажите начало и окончание опроса";
      return;
    }
    // The lines under the fields say what is wrong; this one says why the
    // press did nothing.
    if (startsReason.value || endsReason.value) {
      errorMessage.value = "Проверьте начало и окончание опроса";
      return;
    }
    // The field is read to the minute and the server takes no start before
    // its own clock: the start the form offered is "now", and by the time the
    // form is sent that minute is behind. A start the clock has passed while
    // the form was being filled in is the moment it is sent.
    const now = new Date().toISOString();
    const startsUtc = starts < now ? now : starts;

    isSubmitting.value = true;
    errorMessage.value = "";

    const { error } = await pollsStore.createPoll({
      title: pollTitle.value.trim(),
      details: pollDetails.value.trim() || null,
      startsUtc,
      endsUtc,
      isAnonymous: pollIsAnonymous.value,
      options: validOptions.map((o) => ({ text: o.text })) as any,
    });

    isSubmitting.value = false;

    if (error) {
      if (error.status === 400) {
        errorMessage.value = "Начало должно быть раньше окончания";
      } else {
        // The form stays on screen with what was typed in it, and the line in
        // it says every refusal, the plate the interceptor held for it taken
        // down. An expired session and a dead connection stay with the plate.
        errorMessage.value =
          refusalHere(error, "Не удалось создать опрос") ?? "";
      }
    } else {
      resetForm();
      formExpanded.value = false;
    }
  }

  return {
    formExpanded,
    formHovered,
    pollTitle,
    pollDetails,
    pollStartsUtc,
    pollEndsUtc,
    startsMin,
    endsMin,
    endsMax,
    startsReason,
    endsReason,
    pollIsAnonymous,
    pollOptions,
    isSubmitting,
    errorMessage,
    toggleForm,
    addOption,
    removeOption,
    submitPoll,
  };
}
