import { ref } from "vue";
import { useTestimonialStore } from "@/entities/testimonial";
import {
  describeFailure,
  refusalHere,
  REQUEST_FAILED,
} from "@/shared/lib/errors";

export function useCreateTestimonial() {
  const testimonialStore = useTestimonialStore();

  const formExpanded = ref(false);

  // Posted on behalf of a participant, so the name is asked for, not taken from the session.
  const authorUsername = ref("");
  const testimonialText = ref("");
  const isSubmitting = ref(false);
  // Two messages: a refusal naming one field read as a complaint about the other.
  const authorError = ref("");
  const errorMessage = ref("");

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
      testimonialText.value,
    );

    isSubmitting.value = false;

    if (error) {
      if (error.status === 409) {
        authorError.value = "У этого пользователя уже есть отзыв";
      } else if (error.status === 404) {
        // The server checks the name exists; the picker can be typed past.
        authorError.value = describeFailure(error, "Пользователь не найден");
      } else {
        // A refused length is said in the server's words, with its bound.
        errorMessage.value = refusalHere(error, REQUEST_FAILED) ?? "";
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
