<script setup lang="ts">
// Wraps the card: the editor takes the place of the text, which a corner control cannot reach.
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { TestimonialCard, useTestimonialStore } from "@/entities/testimonial";
import { useAuthStore, userIsSeniorModerator } from "@/entities/user";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import { BubbleAction } from "@/shared/ui/SpeechBubble";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { TESTIMONIAL_TEXT_MAX_LENGTH } from "@/shared/lib/constants/community";

const props = defineProps<{
  testimonial: WebsiteTestimonial;
  searchQuery?: string;
}>();

const { user: currentUser } = storeToRefs(useAuthStore());
const testimonialStore = useTestimonialStore();

// The server compares identifiers: a username can change.
const isAuthor = computed(
  () =>
    !!currentUser.value &&
    currentUser.value.id === props.testimonial.author?.id,
);

const canEdit = computed(
  () => isAuthor.value || userIsSeniorModerator(currentUser.value),
);

const isEditing = ref(false);
const editText = ref("");
const saving = ref(false);

function startEdit() {
  if (isEditing.value) return;
  // A testimonial carries no BBCode: the displayed text is the source.
  editText.value = props.testimonial.text;
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
  saveResult.clear();
}

const saveResult = useActionResult();

async function saveEdit() {
  if (!editText.value.trim() || saving.value) return;
  saving.value = true;
  saveResult.clear();
  const { error } = await testimonialStore.updateTestimonial(
    props.testimonial.id,
    editText.value,
  );
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось сохранить отзыв");
    return;
  }
  isEditing.value = false;
  editText.value = "";
}

function handleEditKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") cancelEdit();
}
</script>

<template>
  <div v-if="isEditing" class="testimonial-edit">
    <!-- Escape bubbles out of the <textarea> to the component's root. -->
    <TextArea
      v-model="editText"
      placeholder="Текст отзыва"
      :max-length="TESTIMONIAL_TEXT_MAX_LENGTH"
      @keydown="handleEditKeydown"
    />
    <div class="edit-actions">
      <Button
        type="button"
        variant="primary"
        :loading="saving"
        :disabled="!editText.trim()"
        @click="saveEdit"
      >
        Сохранить
      </Button>
      <Button type="button" @click="cancelEdit"> Отмена </Button>
    </div>
    <ActionResult :result="saveResult" class="edit-refusal" />
  </div>

  <TestimonialCard
    v-else
    :testimonial="testimonial"
    :search-query="searchQuery"
  >
    <template #actions>
      <BubbleAction
        v-if="canEdit"
        icon="pencil"
        label="Редактировать"
        @click="startEdit"
      />
      <slot name="actions" />
    </template>
  </TestimonialCard>
</template>

<style scoped lang="sass">

.testimonial-edit
  display: flex
  flex-direction: column
  gap: $small

.edit-actions
  display: flex
  gap: $small

.edit-refusal
  --action-result-margin: 0
</style>
