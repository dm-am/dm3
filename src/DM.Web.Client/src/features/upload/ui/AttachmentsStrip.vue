<script setup lang="ts">
import type { PostAttachment } from "@/entities/game";
import AttachmentRow from "./AttachmentRow.vue";

defineProps<{
  attachments: PostAttachment[];
  open: boolean;
  id: string;
}>();
</script>

<template>
  <div class="expand-fold attachments-fold" :class="{ open }">
    <div class="expand-fold-clip" :inert="!open">
      <ul :id="id" class="attachments-strip">
        <AttachmentRow
          v-for="file in attachments"
          :key="file.id"
          :file="file"
        />
      </ul>
    </div>
  </div>
</template>

<style scoped lang="sass">
.attachments-strip
  list-style: none
  padding: $tiny $small
  background-color: $bg-element-accent
  color: $text
  font-size: $font-size
  text-align: left
  hyphens: manual
</style>
