<script setup lang="ts">
/**
 * TestimonialEditable — a website testimonial that can be rewritten where it
 * stands: a pencil in the top right corner of the bubble, beside the trash of
 * whoever may delete, and on click the card is replaced by the text in a
 * field with "Сохранить" / "Отмена". Escape cancels.
 *
 * It wraps the card rather than sitting in its #actions slot, because the
 * editor takes the place of the text and a corner control cannot reach it.
 * The card itself stays free of this: two other surfaces draw it read-only
 * (the home page gallery, both profile recommendation lists), and one of them
 * is not even a website testimonial.
 *
 * Who is shown the control is the server's decision, repeated and not widened:
 * WebsiteTestimonialIntentionResolver admits Edit to the named author or to
 * SeniorModerator and above. The text is plain — testimonials carry no BBCode
 * — so what is displayed is what is edited, with no source fetch in between.
 */
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

const props = defineProps<{
  testimonial: WebsiteTestimonial;
  /** Search query for highlighting, forwarded to the card. */
  searchQuery?: string;
}>();

const { user: currentUser } = storeToRefs(useAuthStore());
const testimonialStore = useTestimonialStore();

// The server compares identifiers, so this does too: a username can change,
// and the two sides would then disagree about the same person.
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
  // Plain text in, plain text out: a testimonial carries no BBCode, so what
  // the card displays is already the source and there is nothing to fetch.
  editText.value = props.testimonial.text.trim();
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
  saveResult.clear();
}

/** A refused save, under the buttons of the editor. */
const saveResult = useActionResult();

async function saveEdit() {
  if (!editText.value.trim() || saving.value) return;
  saving.value = true;
  saveResult.clear();
  const { error } = await testimonialStore.updateTestimonial(
    props.testimonial.id,
    editText.value.trim(),
  );
  saving.value = false;
  if (error) {
    // Keep the editor open with the text still in it, so a refusal costs
    // nothing that was typed, and say it under the buttons.
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
    <!-- Escape reaches the handler by bubbling out of the <textarea>: the
         listener lands on the component's root, which wraps it. -->
    <TextArea
      v-model="editText"
      placeholder="Текст отзыва"
      :max-length="1000"
      @keydown="handleEditKeydown"
    />
    <div class="edit-actions">
      <Button
        type="button"
        variant="primary"
        class="action-btn save-btn"
        :loading="saving"
        :disabled="!editText.trim()"
        @click="saveEdit"
      >
        Сохранить
      </Button>
      <Button type="button" class="action-btn" @click="cancelEdit">
        Отмена
      </Button>
    </div>
    <ActionResult :result="saveResult" class="edit-refusal" />
  </div>

  <TestimonialCard
    v-else
    :testimonial="testimonial"
    :search-query="searchQuery"
  >
    <!-- The pencil first, then whatever the page adds (the trash). -->
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
@use "@/assets/styles/Inputs" as *

.testimonial-edit
  display: flex
  flex-direction: column
  gap: $small

.edit-actions
  display: flex
  gap: $small

// Spaced by the gap of the column. The two buttons above it are the shared
// pair of every form on the site, the primary first.
.edit-refusal
  --action-result-margin: 0
</style>
