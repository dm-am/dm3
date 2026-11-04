<script setup lang="ts">
/**
 * The bar of the edit mode of a profile, sticky under the last field, and the
 * only place it saves from: the state in words, "Сохранить" while something
 * has changed, and "Отмена", or "Завершить" while nothing has.
 */
import Button from "@/shared/ui/Button/Button.vue";

defineProps<{
  /** The owner edits the profile. */
  shown: boolean;
  hasChanges: boolean;
  isSaving: boolean;
}>();

const emit = defineEmits<{
  save: [];
  cancel: [];
}>();
</script>

<template>
  <Transition name="save-bar">
    <div v-if="shown" class="save-bar">
      <span class="save-bar-status">
        {{ hasChanges ? "Несохраненные изменения" : "Режим редактирования" }}
      </span>
      <div class="save-bar-actions">
        <Button
          v-if="hasChanges"
          variant="primary"
          :loading="isSaving"
          @click="emit('save')"
        >
          Сохранить
        </Button>
        <Button :disabled="isSaving" @click="emit('cancel')">
          {{ hasChanges ? "Отмена" : "Завершить" }}
        </Button>
      </div>
    </div>
  </Transition>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *

// The words, then the buttons after them on the left; where the column is narrow the buttons go down a line together.
.save-bar
  position: sticky
  bottom: 0
  left: 0
  right: 0
  z-index: $z-sticky
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small $medium
  padding: $small $medium
  margin-top: $big
  background-color: $bg-element
  border-top: 1px solid $border
  box-shadow: 0 -2px 8px $shadow-color

.save-bar-status
  font-size: $secondary-font-size
  color: $text-muted

.save-bar-actions
  display: flex
  gap: $medium

.save-bar-enter-active,
.save-bar-leave-active
  transition: transform $transition-normal, opacity $transition-normal

.save-bar-enter-from,
.save-bar-leave-to
  transform: translateY(100%)
  opacity: 0
</style>
