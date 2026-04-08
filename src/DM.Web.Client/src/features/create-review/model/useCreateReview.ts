import { ref } from "vue";
import { useTestimonialStore } from "@/shared/stores/testimonials";

export function useCreateReview() {
  const testimonialStore = useTestimonialStore();

  const formExpanded = ref(false);
  const formHovered = ref(false);
  const formContent = ref<HTMLElement | null>(null);

  const authorUsername = ref("");
  const reviewText = ref("");
  const isSubmitting = ref(false);
  const errorMessage = ref("");

  function toggleForm() {
    formExpanded.value = !formExpanded.value;
    if (formContent.value) {
      if (formExpanded.value) {
        formContent.value.style.height = "auto";
        const expectedHeight = formContent.value.clientHeight;
        formContent.value.style.height = "0";
        setTimeout(() => {
          if (formContent.value)
            formContent.value.style.height = `${expectedHeight}px`;
        }, 0);
        setTimeout(() => {
          if (formContent.value) formContent.value.style.height = "auto";
        }, 200);
      } else {
        formContent.value.style.height = `${formContent.value.clientHeight}px`;
        setTimeout(() => {
          if (formContent.value) formContent.value.style.height = "0";
        }, 0);
      }
    }
  }

  async function submitReview() {
    if (!reviewText.value.trim()) {
      errorMessage.value = "Заполните текст отзыва";
      return;
    }

    isSubmitting.value = true;
    errorMessage.value = "";

    const { error } = await testimonialStore.createTestimonial(
      reviewText.value.trim(),
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
      reviewText.value = "";
      formExpanded.value = false;
    }
  }

  return {
    formExpanded,
    formHovered,
    formContent,
    authorUsername,
    reviewText,
    isSubmitting,
    errorMessage,
    toggleForm,
    submitReview,
  };
}
