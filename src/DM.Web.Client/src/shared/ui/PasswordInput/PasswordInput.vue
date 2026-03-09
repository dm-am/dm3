<template>
  <div class="password-input">
    <input
      ref="inputRef"
      :type="visible ? 'text' : 'password'"
      :value="modelValue"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      @blur="$emit('blur', $event)"
      v-bind="$attrs"
    />
    <button
      type="button"
      class="password-toggle"
      @click="visible = !visible"
      tabindex="-1"
      aria-label="Показать/скрыть пароль"
    >
      <svg
        :viewBox="visible ? icons.eyeOpen.viewBox : icons.eyeClosed.viewBox"
        fill="none"
        v-html="visible ? icons.eyeOpen.path : icons.eyeClosed.path"
      />
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { icons } from "@/shared/lib/utils/icons";

defineProps<{
  modelValue: string;
}>();

defineEmits<{
  "update:modelValue": [value: string];
  blur: [event: FocusEvent];
}>();

defineOptions({
  inheritAttrs: false,
});

const visible = ref(false);
const inputRef = ref<HTMLInputElement | null>(null);

defineExpose({
  focus: () => inputRef.value?.focus(),
});
</script>

<style scoped lang="sass">
@import "@/assets/styles/Variables"

.password-input
  position: relative
  display: block
  width: 100%

  input
    width: 100%
    padding-right: 36px
    box-sizing: border-box

.password-toggle
  position: absolute
  right: 4px
  top: 50%
  transform: translateY(-50%)
  background: none
  border: none
  cursor: pointer
  padding: 4px
  color: $text-muted
  display: flex
  align-items: center
  justify-content: center

  svg
    width: 18px
    height: 18px

  &:hover
    color: $text
</style>
