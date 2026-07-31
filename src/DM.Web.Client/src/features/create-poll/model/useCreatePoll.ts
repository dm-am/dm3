import { ref } from "vue";
import dayjs from "dayjs";
import { usePollsStore } from "@/entities/poll";
import { describeFailure } from "@/shared/lib/errors";

export function useCreatePoll() {
  const pollsStore = usePollsStore();

  const formExpanded = ref(false);
  const formHovered = ref(false);

  const pollTitle = ref("");
  const pollDetails = ref("");
  const pollStartsUtc = ref(dayjs().format("YYYY-MM-DDTHH:mm"));
  const pollEndsUtc = ref(dayjs().add(7, "day").format("YYYY-MM-DDTHH:mm"));
  const pollIsAnonymous = ref(true);

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
    pollTitle.value = "";
    pollDetails.value = "";
    pollStartsUtc.value = dayjs().format("YYYY-MM-DDTHH:mm");
    pollEndsUtc.value = dayjs().add(7, "day").format("YYYY-MM-DDTHH:mm");
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

    isSubmitting.value = true;
    errorMessage.value = "";

    const { error } = await pollsStore.createPoll({
      title: pollTitle.value.trim(),
      details: pollDetails.value.trim() || null,
      startsUtc: new Date(pollStartsUtc.value).toISOString(),
      endsUtc: new Date(pollEndsUtc.value).toISOString(),
      isAnonymous: pollIsAnonymous.value,
      options: validOptions.map((o) => ({ text: o.text })) as any,
    });

    isSubmitting.value = false;

    if (error) {
      if (error.status === 400) {
        errorMessage.value =
          "Некорректные данные. Начало должно быть раньше окончания.";
      } else {
        // The server names which refusal a 403 was; this sentence is for a
        // failure that named nothing.
        errorMessage.value = describeFailure(error, "Не удалось создать опрос");
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
