<script setup lang="ts">
/**
 * TestimonialEditable — a website testimonial that can be rewritten where it
 * stands, by the comment idiom: a "Редактировать" control in the card footer
 * next to "Удалить", and on click the card body is replaced by the text in a
 * field with "Сохранить" / "Отмена". Escape cancels.
 *
 * It wraps the card rather than sitting in its #controls slot, because the
 * editor takes the place of the text and a footer control cannot reach it.
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
import { notifyFailure } from "@/shared/lib/errors";

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
}

async function saveEdit() {
  if (!editText.value.trim() || saving.value) return;
  saving.value = true;
  const { error } = await testimonialStore.updateTestimonial(
    props.testimonial.id,
    editText.value.trim(),
  );
  saving.value = false;
  if (error) {
    // Keep the editor open with the text still in it, so a refusal costs
    // nothing that was typed.
    notifyFailure(error, "Не удалось сохранить отзыв");
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
      <button
        type="button"
        class="action-btn save-btn"
        :disabled="!editText.trim() || saving"
        @click="saveEdit"
      >
        Сохранить
      </button>
      <button type="button" class="action-btn" @click="cancelEdit">
        Отмена
      </button>
    </div>
  </div>

  <TestimonialCard
    v-else
    :testimonial="testimonial"
    :search-query="searchQuery"
  >
    <template #controls
      ><template v-if="canEdit"
        ><span class="copy-space">{{ " " }}</span
        ><secondary-text class="testimonial-controls"
          ><button
            type="button"
            class="testimonial-edit-btn"
            @click="startEdit"
          >
            Редактировать
          </button></secondary-text
        ></template
      ><slot name="controls"></slot
    ></template>
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

// Zero-width preserved space: invisible in layout (font-size: 0) but
// Selection.toString() still emits a real " " before the control — the same
// device the delete control next to it uses.
.copy-space
  white-space: pre
  font-size: 0

.testimonial-controls
  display: inline
  margin-left: $small

// "Редактировать" — a real <button> reset to the inline link appearance,
// matching "Удалить" beside it.
.testimonial-edit-btn
  +inline-link-button

.action-btn
  padding: $tiny $small
  font-size: $secondary-font-size
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted

  &:hover:not(:disabled)
    color: $link

  &:disabled
    cursor: default
    opacity: 0.6

  &.save-btn
    background-color: $button-bg
    color: $button-text
    border-radius: $tiny
</style>
