<script setup lang="ts">
/**
 * UploadsTable — the table of uploaded files: thumbnail or extension, the file
 * itself as a link, when it arrived, how big it is, and the delete action.
 *
 * Two screens draw it — the owner's "Загруженное" and the moderation-wide "Все
 * загруженные файлы" — and they had a byte-identical copy each. What they
 * legitimately disagree on stays with them: the moderation table has a
 * "Загрузил" column (filled through the cell-uploader slot) and a page of 25
 * against the profile's 20, so the column set comes in as a prop.
 *
 * Deleting is the caller's to confirm and to carry out (useUploadDelete): this
 * only reports which row the button was pressed on.
 */
import type { Upload } from "@/shared/api/models/common/upload";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { formatDate } from "@/shared/lib/utils/datetime";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import { isImage, fileExt, uploadHref } from "@/shared/lib/utils/upload";

defineProps<{
  /** Rows of the current page, newest first. */
  uploads: Upload[];
  /** Column set, including any the caller adds to the shared ones. */
  columns: Column[];
  loading: boolean;
}>();

defineEmits<{
  /** The delete button of a row was pressed. */
  (e: "remove", row: Upload): void;
}>();

defineSlots<{
  /** Cell of the moderation-only "Загрузил" column. */
  "cell-uploader"?: (props: { row: Upload; index: number }) => unknown;
}>();
</script>

<template>
  <DataTable
    :columns="columns"
    :data="uploads"
    :loading="loading"
    empty-text="Загруженных файлов пока нет"
    aria-label="Загруженные файлы"
  >
    <template #cell-preview="{ row }">
      <img
        v-if="isImage(row)"
        :src="uploadHref(row)"
        :alt="row.originalFileName"
        class="upload-thumb"
        loading="lazy"
      />
      <span v-else class="upload-ext">{{ fileExt(row) }}</span>
    </template>
    <template #cell-file="{ row }">
      <a
        :href="uploadHref(row)"
        target="_blank"
        rel="noopener"
        class="upload-name"
      >
        {{ row.originalFileName }}
      </a>
    </template>
    <template #cell-uploader="slotProps">
      <slot name="cell-uploader" v-bind="slotProps" />
    </template>
    <template #cell-date="{ row }">
      {{ formatDate(row.createdUtc) }}
    </template>
    <template #cell-size="{ row }">
      {{ formatFileSize(row.sizeBytes) }}
    </template>
    <template #cell-actions="{ row }">
      <button type="button" class="delete-button" @click="$emit('remove', row)">
        Удалить
      </button>
    </template>
  </DataTable>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.upload-thumb
  display: block
  width: 48px
  height: 48px
  object-fit: cover
  border-radius: $border-radius
  margin: 0 auto

.upload-ext
  color: $text-muted
  font-size: $tertiary-font-size
  font-weight: bold

.upload-name
  overflow-wrap: anywhere

.delete-button
  +inline-link-button

  // Destructive action stays red at rest and on hover
  &,
  &:hover:not(:disabled)
    color: $accent-red
</style>
