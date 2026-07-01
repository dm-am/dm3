<script setup lang="ts">
import { ref, watch, nextTick, onMounted, computed } from "vue";
import type { User } from "@/shared/api/models/community";
import { SecondaryText } from "@/shared/ui/Layout";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
}>();

const emit = defineEmits<{
  updateField: [field: string, value: string];
}>();

// API contract: `user.info` is a string serialised by the server's BbConverter.
// In Display audience (default) it's pre-rendered HTML; in AuthorEdit audience
// (used by `getUserForUpdate`) it's the raw BBCode source. The frontend never
// renders BBCode itself — it just hands display HTML to v-html and edit source
// to the textarea.
const infoText = computed<string>(() => {
  const raw = props.user.info as unknown;
  if (typeof raw === "string") return raw;
  // Defensive fallback for older API shapes ({source, html, value}).
  if (raw && typeof raw === "object") {
    const obj = raw as { html?: string; source?: string; value?: string };
    return (props.isEditMode ? obj.source : obj.html) ?? obj.value ?? "";
  }
  return "";
});

const contentRef = ref<HTMLElement | null>(null);

onMounted(() => nextTick(() => initBbcodeInteractive(contentRef.value)));

watch(
  () => props.user.info,
  () => nextTick(() => initBbcodeInteractive(contentRef.value)),
);
</script>

<template>
  <section class="profile-about">
    <template v-if="isEditMode">
      <textarea
        :value="infoText"
        class="about-textarea"
        placeholder="Расскажите о себе (поддерживается BBCode)…"
        rows="8"
        @input="
          emit(
            'updateField',
            'info',
            ($event.target as HTMLTextAreaElement).value,
          )
        "
      />
    </template>

    <template v-else>
      <div
        v-if="infoText"
        ref="contentRef"
        class="about-content bbcode-content"
        v-html="infoText"
      />
      <SecondaryText v-else class="about-empty">
        Пользователь ничего о себе не написал
      </SecondaryText>
    </template>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"
@import "src/assets/styles/BbcodeContent"

.profile-about
  // Flat — no boxes, no rounded corners. Spacing comes from BlockTitle margins.

.about-content
  color: $text
  line-height: 1.6

// Empty-section placeholder: muted gray, distinct from the user's actual
// content. (Stat values like "не указан" stay in default $text color
// because they're primary data; section-level "no content yet" copy is
// the de-emphasized exception.)
.about-empty
  color: $text-muted

.about-textarea
  width: 100%
  padding: $small
  font-family: inherit
  font-size: inherit
  resize: vertical
  min-height: 200px
</style>
