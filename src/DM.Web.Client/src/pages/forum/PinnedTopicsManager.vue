<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { symbols } from "@/shared/lib/utils/icons";
import { useDialogShell } from "@/shared/lib/composables/useDialogShell";
import type { Topic } from "@/entities/forum";

const props = defineProps<{
  /** List of pinned topics */
  topics: Topic[];
  /** Whether save is in progress */
  saving: boolean;
}>();

const emit = defineEmits<{
  /** Emitted when order changes and should be saved */
  save: [topicIds: string[]];
  /** Emitted when modal should close */
  close: [];
}>();

// Local copy for reordering
const localTopics = ref<Topic[]>([]);

watch(
  () => props.topics,
  (newTopics) => {
    localTopics.value = [...newTopics];
  },
  { immediate: true },
);

// Drag and drop state
const draggedIndex = ref<number | null>(null);
const dragOverIndex = ref<number | null>(null);

function onDragStart(index: number, event: DragEvent) {
  draggedIndex.value = index;
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", String(index));
  }
}

function onDragOver(index: number, event: DragEvent) {
  event.preventDefault();
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = "move";
  }
  dragOverIndex.value = index;
}

function onDragLeave() {
  dragOverIndex.value = null;
}

function onDrop(targetIndex: number, event: DragEvent) {
  event.preventDefault();
  if (draggedIndex.value === null || draggedIndex.value === targetIndex) {
    draggedIndex.value = null;
    dragOverIndex.value = null;
    return;
  }

  // Reorder array
  const items = [...localTopics.value];
  const [draggedItem] = items.splice(draggedIndex.value, 1);
  items.splice(targetIndex, 0, draggedItem);
  localTopics.value = items;

  draggedIndex.value = null;
  dragOverIndex.value = null;
}

function onDragEnd() {
  draggedIndex.value = null;
  dragOverIndex.value = null;
}

// Move with buttons (alternative to drag)
function moveUp(index: number) {
  if (index === 0) return;
  const items = [...localTopics.value];
  [items[index - 1], items[index]] = [items[index], items[index - 1]];
  localTopics.value = items;
}

function moveDown(index: number) {
  if (index === localTopics.value.length - 1) return;
  const items = [...localTopics.value];
  [items[index], items[index + 1]] = [items[index + 1], items[index]];
  localTopics.value = items;
}

function handleSave() {
  const topicIds = localTopics.value.map((t) => String(t.id));
  emit("save", topicIds);
}

function handleClose() {
  emit("close");
}

// This overlay was a modal in look only: no role, no aria-modal, no Escape, no
// focus trap, no focus returned to the button that opened it. It is mounted by
// its parent's v-if, so from its own side it is open for as long as it exists —
// which is what the shell is told.
const modal = ref<HTMLElement | null>(null);
const closeBtn = ref<HTMLElement | null>(null);
const shell = useDialogShell({
  show: computed(() => true),
  container: modal,
  initialFocus: () => closeBtn.value,
  onDismiss: handleClose,
});
</script>

<template>
  <div
    class="pinned-manager-overlay"
    @click="shell.handleBackdropClick"
    @keydown="shell.handleKeydown"
  >
    <!-- tabindex="-1": the keydown listener is on the overlay, and a click on
         the modal's own heading or hint drops focus to <body>, an ancestor of
         the overlay that the event would then never reach. -->
    <div
      ref="modal"
      class="pinned-manager-modal"
      tabindex="-1"
      role="dialog"
      aria-modal="true"
      aria-label="Порядок закрепленных топиков"
    >
      <div class="modal-header">
        <h3>Порядок закрепленных топиков</h3>
        <Tooltip text="Закрыть">
          <button
            ref="closeBtn"
            class="close-button"
            aria-label="Закрыть"
            @click="handleClose"
          >
            {{ symbols.close }}
          </button>
        </Tooltip>
      </div>

      <div class="modal-content">
        <p class="hint">
          Перетащите топики для изменения порядка или используйте кнопки
        </p>

        <ul class="topics-list">
          <li
            v-for="(topic, index) in localTopics"
            :key="topic.id"
            class="topic-item"
            :class="{
              dragging: draggedIndex === index,
              'drag-over': dragOverIndex === index && draggedIndex !== index,
            }"
            draggable="true"
            @dragstart="onDragStart(index, $event)"
            @dragover="onDragOver(index, $event)"
            @dragleave="onDragLeave"
            @drop="onDrop(index, $event)"
            @dragend="onDragEnd"
          >
            <span class="drag-handle" title="Перетащите для перемещения"
              >⋮⋮</span
            >
            <span class="topic-title">{{ topic.title }}</span>
            <div class="move-buttons">
              <Tooltip text="Переместить вверх">
                <button
                  class="move-button"
                  aria-label="Переместить вверх"
                  :disabled="index === 0"
                  @click="moveUp(index)"
                >
                  {{ symbols.arrowUp }}
                </button>
              </Tooltip>
              <Tooltip text="Переместить вниз">
                <button
                  class="move-button"
                  aria-label="Переместить вниз"
                  :disabled="index === localTopics.length - 1"
                  @click="moveDown(index)"
                >
                  {{ symbols.arrowDown }}
                </button>
              </Tooltip>
            </div>
          </li>
        </ul>

        <p v-if="!localTopics.length" class="empty-message">
          Нет закрепленных топиков
        </p>
      </div>

      <div class="modal-footer">
        <button class="cancel-button" :disabled="saving" @click="handleClose">
          Отмена
        </button>
        <Button
          :loading="saving"
          :disabled="!localTopics.length"
          @click="handleSave"
        >
          Сохранить порядок
        </Button>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/ZIndex"

.pinned-manager-overlay
  position: fixed
  top: 0
  left: 0
  right: 0
  bottom: 0
  // The scrim of the dialog tier, from the token both other backdrops take.
  // It was the one hand-mixed colour left outside the dev catalogue.
  background: $overlay-bg
  display: flex
  align-items: center
  justify-content: center
  z-index: $z-dialog

.pinned-manager-modal
  background: $bg-page
  border-radius: $border-radius
  box-shadow: 0 4px 20px $shadow-color
  width: 90%
  max-width: 500px
  max-height: 80vh
  display: flex
  flex-direction: column

.modal-header
  display: flex
  justify-content: space-between
  align-items: center
  padding: $medium
  border-bottom: 1px solid $border

  h3
    margin: 0
    font-size: 18px

.close-button
  background: none
  border: none
  cursor: pointer
  color: $text-muted
  padding: 4px
  display: flex
  align-items: center
  justify-content: center

  &:hover
    color: $text

.modal-content
  padding: $medium
  overflow-y: auto
  flex: 1

.hint
  color: $text-muted
  font-size: 14px
  margin-bottom: $medium

.topics-list
  list-style: none
  padding: 0
  margin: 0

.topic-item
  display: flex
  align-items: center
  gap: $small
  padding: $small $medium
  background: $bg-element
  border: 1px solid $border
  border-radius: 4px
  margin-bottom: $small
  cursor: grab
  transition: opacity 0.2s ease

  &:hover
    border-color: $link

  &.dragging
    opacity: 0.5
    cursor: grabbing

  &.drag-over
    border-color: $border-focus
    background: $hover-overlay

.drag-handle
  color: $text-muted
  cursor: grab
  font-size: 12px
  letter-spacing: -2px

.topic-title
  flex: 1
  overflow: hidden
  text-overflow: ellipsis
  white-space: nowrap

.move-buttons
  display: flex
  gap: 4px

.move-button
  padding: $tiny $small
  +button

.empty-message
  color: $text-muted
  padding: $large

.modal-footer
  display: flex
  justify-content: flex-end
  gap: $small
  padding: $medium
  border-top: 1px solid $border

.cancel-button
  +button
</style>
