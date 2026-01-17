<template>
  <div>
    <textarea
      ref="textareaRef"
      :value="modelValue"
      :disabled="disabled"
      @input="onInput"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, onMounted } from "vue";

const props = defineProps<{
  modelValue: string;
  disabled?: boolean;
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

watch(() => props.modelValue, () => {
  nextTick(adjustHeight);
});

onMounted(adjustHeight);
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

textarea
  display: block
  width: 100%
  min-height: $big * 3
  box-sizing: border-box
  padding: $small
  resize: none
  overflow: hidden
  border-radius: $border-radius
  +theme(background, $input-background)
  +theme(color, $text)
  +theme(border, $border, 1px solid)
</style>
