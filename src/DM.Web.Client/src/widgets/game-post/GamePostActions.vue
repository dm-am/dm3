<script setup lang="ts">
/** The edit line of a post; the line asks, the host decides. */
import { computed } from "vue";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const props = defineProps<{
  /** When the post was last edited, if the page knows. */
  editedAt: string | null;
  isEdited: boolean;
  canEdit: boolean;
  canDelete: boolean;
  /** The source of the edit is being read: "Редактировать" waits for it. */
  opening?: boolean;
}>();

const emit = defineEmits<{ edit: []; delete: [] }>();

const formattedEditedAt = computed(() =>
  props.editedAt ? formatDateFull(props.editedAt) : "",
);
</script>

<template>
  <div class="post-edit-line">
    <CaptionLine>
      <span v-if="isEdited && editedAt"
        >Отредактировано {{ formattedEditedAt }}</span
      >
      <span v-else-if="isEdited">Отредактировано</span>
      <CaptionAction
        v-if="canEdit"
        :disabled="opening"
        :aria-busy="opening || undefined"
        @click="emit('edit')"
        >Редактировать</CaptionAction
      >
      <CaptionAction v-if="canDelete" @click="emit('delete')"
        >Удалить</CaptionAction
      >
    </CaptionLine>
  </div>
</template>

<style scoped lang="sass">
// DM2's div.p.comment: 12px grey at the text's left edge, 22px under the last block.
.post-edit-line
  display: block
  margin: 11px 5px 0 10px
  font-size: $tertiary-font-size
  line-height: 15px
  text-align: left
</style>
