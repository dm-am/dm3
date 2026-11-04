import { computed, ref } from "vue";
import dayjs from "dayjs";
import { usePollsStore } from "@/entities/poll";
import { refusalHere } from "@/shared/lib/errors";
import { fromSiteInput, toSiteInput } from "@/shared/lib/utils/datetime";

/** The form's labels, by the server's name of each field, for a refused field to be named. */
const FIELD_LABELS = {
  title: "Название",
  details: "Описание",
  startsUtc: "Начало",
  endsUtc: "Окончание",
  options: "Варианты ответа",
};

export function useCreatePoll() {
  const pollsStore = usePollsStore();

  const formExpanded = ref(false);

  const openedAt = ref(toSiteInput());

  const pollTitle = ref("");
  const pollDetails = ref("");
  const pollStartsUtc = ref(openedAt.value);
  const pollEndsUtc = ref(toSiteInput(dayjs().add(7, "day")));
  const pollIsAnonymous = ref(true);

  // The server's rules (CreatePollValidator), so a pair it would refuse is not sent.
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

  const startsReason = ref<string | null>(null);
  const endsReason = ref<string | null>(null);

  // Objects with ids, for stable v-for keys.
  let optionIdCounter = 0;
  const createOption = (text = "") => ({ id: ++optionIdCounter, text });
  const pollOptions = ref([createOption(), createOption()]);

  const isSubmitting = ref(false);
  const errorMessage = ref("");

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

    // A cleared field has no moment; `new Date` would throw past a raised isSubmitting.
    const starts = fromSiteInput(pollStartsUtc.value);
    const endsUtc = fromSiteInput(pollEndsUtc.value);
    if (!starts || !endsUtc) {
      errorMessage.value = "Укажите начало и окончание опроса";
      return;
    }
    if (startsReason.value || endsReason.value) {
      errorMessage.value = "Проверьте начало и окончание опроса";
      return;
    }
    // A start already past, as the minute offered at opening soon is, goes as the moment of sending.
    const now = new Date().toISOString();
    const startsUtc = starts < now ? now : starts;

    isSubmitting.value = true;
    errorMessage.value = "";

    const { error } = await pollsStore.createPoll({
      title: pollTitle.value,
      details: pollDetails.value.trim() ? pollDetails.value : null,
      startsUtc,
      endsUtc,
      isAnonymous: pollIsAnonymous.value,
      options: validOptions.map((o) => o.text),
    });

    isSubmitting.value = false;

    if (error) {
      errorMessage.value =
        refusalHere(error, "Не удалось создать опрос", FIELD_LABELS) ?? "";
    } else {
      resetForm();
      formExpanded.value = false;
    }
  }

  return {
    formExpanded,
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
