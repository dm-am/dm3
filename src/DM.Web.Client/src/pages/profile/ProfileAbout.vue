<script setup lang="ts">
import { ref, watch, nextTick, onMounted, computed } from "vue";
import type { User } from "@/shared/api/models/community";
import { SecondaryText } from "@/shared/ui/Layout";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
}>();

const emit = defineEmits<{
  updateField: [field: string, value: string];
}>();

// API contract: `user.info` is a string serialised by the server's BbConverter.
// In Display audience (default) it's pre-rendered HTML; in AuthorEdit audience
// it's the raw BBCode source. The frontend never renders BBCode itself — it
// just hands display HTML to v-html and edit source to the textarea.
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
      <BBCodeEditor
        :model-value="infoText"
        context="info"
        placeholder="Расскажите о себе (поддерживается BBCode)..."
        :min-height="160"
        @update:model-value="emit('updateField', 'info', $event)"
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
@import "@/assets/styles/BbcodeContent"

.profile-about
  // Flat — no boxes, no rounded corners. Spacing comes from BlockTitle margins.

// Line-height and BBCode typography come from the global .bbcode-content
// (SSOT in _BbcodeContent.sass) — no local overrides.
.about-content
  color: $text

// Empty-section placeholder: muted gray, distinct from the user's actual
// content. (Stat values like "не указан" stay in default $text color
// because they're primary data; section-level "no content yet" copy is
// the de-emphasized exception.)
.about-empty
  color: $text-muted
</style>
