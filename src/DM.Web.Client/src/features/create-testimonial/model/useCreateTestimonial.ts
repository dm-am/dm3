import { ref } from "vue";
import { useTestimonialStore } from "@/shared/stores/testimonials";

export function useCreateTestimonial() {
  const testimonialStore = useTestimonialStore();

  const formExpanded = ref(false);

  const testimonialText = ref("");
  const isSubmitting = ref(false);
  const errorMessage = ref("");

  // The reveal itself is the global CSS-only .expand-fold (Reset.sass) —
  // an inline tool form: unified animation tempo, no registry
  // (UI_STANDARDS, Animation Standards).
  function toggleForm() {
    formExpanded.value = !formExpanded.value;
  }

  async function submitTestimonial() {
    if (!testimonialText.value.trim()) {
      errorMessage.value = "Заполните текст отзыва";
      return;
    }

    isSubmitting.value = true;
    errorMessage.value = "";

    const { error } = await testimonialStore.createTestimonial(
      testimonialText.value.trim(),
    );

    isSubmitting.value = false;

    if (error) {
      if (error.status === 409) {
        errorMessage.value = "У вас уже есть отзыв";
      } else if (error.status === 400) {
        errorMessage.value = "Некорректные данные (10-1000 символов)";
      } else if (error.status === 403) {
        errorMessage.value = "Недостаточно прав";
      } else if (error.status === 500) {
        errorMessage.value = "Внутренняя ошибка сервера. Попробуйте позже";
      } else {
        errorMessage.value = "Не удалось создать отзыв. Попробуйте позже";
      }
    } else {
      testimonialText.value = "";
      formExpanded.value = false;
    }
  }

  return {
    formExpanded,
    testimonialText,
    isSubmitting,
    errorMessage,
    toggleForm,
    submitTestimonial,
  };
}
