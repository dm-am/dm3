<template>
  <div>
    <textarea
      :id="id"
      ref="textareaRef"
      :value="modelValue"
      :disabled="disabled"
      :placeholder="placeholder"
      :maxlength="maxLength"
      @input="onInput"
    />
    <div v-if="maxLength" class="textarea-counter">
      <CounterPair
        :first-value="modelValue.length"
        :second-value="maxLength"
        :class="{ 'over-limit': modelValue.length > maxLength }"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, onMounted } from "vue";
import { CounterPair } from "@/shared/ui/CounterPair";

const props = defineProps<{
  modelValue: string;
  disabled?: boolean;
  placeholder?: string;
  /** Native maxlength; when set, shows a remaining-chars counter below the field */
  maxLength?: number;
  /** Id for the <textarea>, so a caller's <label for> reaches it. */
  id?: string;
}>();
const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const textareaRef = ref<HTMLTextAreaElement | null>(null);

function adjustHeight() {
  const textarea = textareaRef.value;
  if (textarea) {
    textarea.style.height = "auto";
    textarea.style.height = textarea.scrollHeight + "px";
  }
}

const onInput = (event: Event) => {
  const target = event.target as HTMLTextAreaElement;
  emit("update:modelValue", target.value);
  adjustHeight();
};

watch(
  () => props.modelValue,
  () => {
    nextTick(adjustHeight);
  },
);

onMounted(adjustHeight);
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

textarea
  +input()
  &
    display: block
    width: 100%
    min-height: $big * 3
    box-sizing: border-box
    resize: none
    overflow: hidden

  &::placeholder
    color: $text-muted
    opacity: 0.6

.textarea-counter
  display: flex
  justify-content: flex-end
  margin-top: $minor
  font-size: $secondary-font-size
  color: $text-muted

  :deep(.over-limit)
    color: $accent-red
</style>
