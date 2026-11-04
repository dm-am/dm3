<script setup lang="ts">
import { computed } from "vue";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const props = defineProps<{
  editedAt: string | null;
  canEdit: boolean;
  canDelete: boolean;
  canWarn: boolean;
}>();

const emit = defineEmits<{ edit: []; delete: []; warn: [] }>();

const formattedEditDate = computed(() =>
  props.editedAt ? formatDateFull(props.editedAt) : "",
);
</script>

<template>
  <div class="comment-edit-line">
    <CaptionLine>
      <span v-if="editedAt">Отредактировано {{ formattedEditDate }}</span>
      <CaptionAction v-if="canEdit" @click="emit('edit')"
        >Редактировать</CaptionAction
      >
      <CaptionAction v-if="canDelete" @click="emit('delete')"
        >Удалить</CaptionAction
      >
      <CaptionAction v-if="canWarn" @click="emit('warn')"
        >Предупреждение</CaptionAction
      >
    </CaptionLine>
  </div>
</template>

<style scoped lang="sass">
// Placed in the text column by CommentItem; the caption's size on the block, so its line is the caption's 15px.
.comment-edit-line
  font-size: $tertiary-font-size
  line-height: 15px
  text-align: left
</style>
