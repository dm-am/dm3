<script setup lang="ts">
import { computed } from "vue";
import type { User } from "@/shared/api/models/community";
import { SecondaryText } from "@/shared/ui/Layout";
import { ContentText } from "@/shared/ui/Content";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
  /** The BBCode the editor opens on: `user.info` is rendered for reading. */
  source: string;
}>();

const emit = defineEmits<{
  updateField: [field: string, value: string];
}>();

const infoText = computed(() => props.user.info ?? "");
</script>

<template>
  <section class="profile-about">
    <template v-if="isEditMode">
      <BBCodeEditor
        :model-value="source"
        context="info"
        placeholder="Расскажите о себе (поддерживается BBCode)..."
        :min-height="160"
        @update:model-value="emit('updateField', 'info', $event)"
      />
    </template>

    <template v-else>
      <ContentText v-if="infoText" class="about-content" :html="infoText" />
      <SecondaryText v-else class="about-empty">
        Пользователь ничего о себе не написал
      </SecondaryText>
    </template>
  </section>
</template>

<style scoped lang="sass">
// The empty section in muted grey, apart from the owner's own words.
.about-empty
  color: $text-muted
</style>
