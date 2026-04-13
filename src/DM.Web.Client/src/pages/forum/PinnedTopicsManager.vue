<script setup lang="ts">
import { ref, watch } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { symbols } from "@/shared/lib/utils/icons";
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
</script>

<template>
  <div class="pinned-manager-overlay" @click.self="handleClose">
    <div class="pinned-manager-modal">
      <div class="modal-header">
        <h3>Порядок закрепленных топиков</h3>
        <Tooltip text="Закрыть">
          <button class="close-button" @click="handleClose">
            {{ symbols.close }}
          </button>
        </Tooltip>
      </div>

      <div class="modal-content">
        <p class="hint">Перетащите топики для изменения порядка или используйте кнопки</p>

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
            <span class="drag-handle" title="Перетащите для перемещения">⋮⋮</span>
            <span class="topic-title">{{ topic.title }}</span>
            <div class="move-buttons">
              <Tooltip text="Переместить вверх">
                <button
                  class="move-button"
                  :disabled="index === 0"
                  @click="moveUp(index)"
                >
                  {{ symbols.arrowUp }}
                </button>
              </Tooltip>
              <Tooltip text="Переместить вниз">
                <button
                  class="move-button"
                  :disabled="index === localTopics.length - 1"
                  @click="moveDown(index)"
                >
                  {{ symbols.arrowDown }}
                </button>
              </Tooltip>
            </div>
          </li>
        </ul>

        <p v-if="!localTopics.length" class="empty-message">Нет закрепленных топиков</p>
      </div>

      <div class="modal-footer">
        <button class="cancel-button" :disabled="saving" @click="handleClose">Отмена</button>
        <button class="save-button" :disabled="saving || !localTopics.length" @click="handleSave">
          {{ saving ? "Сохранение..." : "Сохранить порядок" }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Variables"
@import "@/assets/styles/Themes"
@import "@/assets/styles/ZIndex"

.pinned-manager-overlay
  position: fixed
  top: 0
  left: 0
  right: 0
  bottom: 0
  background: rgba(0, 0, 0, 0.5)
  display: flex
  align-items: center
  justify-content: center
  z-index: $z-modal

.pinned-manager-modal
  background: $bg-page
  border-radius: 8px
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3)
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
    border-color: $link
    background: $bg-element-hover

.drag-handle
  color: $text-muted
  cursor: grab
  user-select: none
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
  background: none
  border: 1px solid $border
  border-radius: 4px
  padding: 2px 8px
  cursor: pointer
  color: $text-muted
  font-size: 14px

  &:hover:not(:disabled)
    color: $link
    border-color: $link

  &:disabled
    opacity: 0.3
    cursor: not-allowed

.empty-message
  text-align: center
  color: $text-muted
  padding: $large

.modal-footer
  display: flex
  justify-content: flex-end
  gap: $small
  padding: $medium
  border-top: 1px solid $border

.cancel-button
  padding: $small $medium
  background: transparent
  border: 1px solid $border
  border-radius: 4px
  cursor: pointer
  color: $text

  &:hover:not(:disabled)
    background: $bg-element-hover

  &:disabled
    opacity: 0.5
    cursor: not-allowed

.save-button
  padding: $small $medium
  background: $link
  border: none
  border-radius: 4px
  cursor: pointer
  color: white

  &:hover:not(:disabled)
    opacity: 0.9

  &:disabled
    opacity: 0.5
    cursor: not-allowed
</style>
