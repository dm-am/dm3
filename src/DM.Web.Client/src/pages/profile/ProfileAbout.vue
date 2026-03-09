<script setup lang="ts">
import { ref, watch, nextTick, onMounted } from "vue";
import type { User } from "@/shared/api/models/community";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
}>();

const emit = defineEmits<{
  (e: "updateField", field: string, value: string): void;
}>();

const contentRef = ref<HTMLElement | null>(null);

onMounted(() => {
  nextTick(() => {
    initBbcodeInteractive(contentRef.value);
  });
});

watch(
  () => props.user.info,
  () => {
    nextTick(() => {
      initBbcodeInteractive(contentRef.value);
    });
  },
);
</script>

<template>
  <section class="profile-about">
    <h3 class="section-title">О себе</h3>

    <template v-if="isEditMode">
      <textarea
        :value="user.info"
        class="about-textarea"
        placeholder="Расскажите о себе (поддерживается BBCode)..."
        rows="8"
        @input="emit('updateField', 'info', ($event.target as HTMLTextAreaElement).value)"
      />
    </template>

    <template v-else>
      <div
        v-if="user.info"
        ref="contentRef"
        class="about-content"
        v-html="user.info"
      />
      <secondary-text v-else class="about-empty">
        Пользователь ничего о себе не написал...
      </secondary-text>
    </template>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"

.profile-about
  background: $bg-element
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium

.section-title
  color: $text
  margin: 0 0 $medium
  font-size: 1rem

.about-content
  +bbcode-content

.about-empty
  font-style: italic

.about-textarea
  width: 100%
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius
  background: $bg-element-overlay
  color: $text
  font-family: inherit
  font-size: inherit
  resize: vertical
  min-height: $grid-step * 40

  &:focus
    outline: none
    border-color: $link
</style>
