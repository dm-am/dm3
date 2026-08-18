import { ref } from "vue";
import { useTestimonialStore } from "@/entities/testimonial";
import { describeFailure } from "@/shared/lib/errors";

export function useCreateTestimonial() {
  const testimonialStore = useTestimonialStore();

  const formExpanded = ref(false);

  // Who the testimonial is signed by. A testimonial is posted on behalf of a
  // participant, so the name is asked for rather than taken from the session:
  // without it the entry carried the moderator who typed it.
  const authorUsername = ref("");
  const testimonialText = ref("");
  const isSubmitting = ref(false);
  // Two messages, because there are two fields and a refusal that names one of
  // them read as a complaint about the other when they shared a line.
  const authorError = ref("");
  const errorMessage = ref("");

  // The reveal itself is the global CSS-only .expand-fold (Reset.sass) —
  // an inline tool form: unified animation tempo, no registry
  // (UI_STANDARDS, Animation Standards).
  function toggleForm() {
    formExpanded.value = !formExpanded.value;
  }

  async function submitTestimonial() {
    authorError.value = "";
    errorMessage.value = "";

    if (!authorUsername.value.trim()) {
      authorError.value = "Выберите автора отзыва";
      return;
    }

    if (!testimonialText.value.trim()) {
      errorMessage.value = "Заполните текст отзыва";
      return;
    }

    isSubmitting.value = true;

    const { error } = await testimonialStore.createTestimonial(
      authorUsername.value.trim(),
      testimonialText.value.trim(),
    );

    isSubmitting.value = false;

    if (error) {
      if (error.status === 409) {
        // The named participant, not the moderator submitting the form.
        authorError.value = "У этого участника уже есть отзыв";
      } else if (error.status === 404) {
        // The server checks the name exists; the picker can be typed past.
        authorError.value = describeFailure(error, "Пользователь не найден");
      } else if (error.status === 400) {
        errorMessage.value = "Некорректные данные (10-1000 символов)";
      } else if (error.status === 500) {
        errorMessage.value = "Внутренняя ошибка сервера. Попробуйте позже";
      } else {
        // The server names which refusal a 403 was; this sentence is for a
        // failure that named nothing.
        errorMessage.value = describeFailure(
          error,
          "Не удалось создать отзыв. Попробуйте позже",
        );
      }
    } else {
      authorUsername.value = "";
      testimonialText.value = "";
      formExpanded.value = false;
    }
  }

  return {
    formExpanded,
    authorUsername,
    testimonialText,
    isSubmitting,
    authorError,
    errorMessage,
    toggleForm,
    submitTestimonial,
  };
}
