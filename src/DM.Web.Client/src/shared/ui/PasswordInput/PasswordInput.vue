<script setup lang="ts">
import { ref } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";

defineProps<{
  modelValue: string;
  /** Bound in the template so lint can check the label association. */
  id?: string;
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

<template>
  <div class="password-input">
    <input
      :id="id"
      ref="inputRef"
      :type="visible ? 'text' : 'password'"
      :value="modelValue"
      @input="
        $emit('update:modelValue', ($event.target as HTMLInputElement).value)
      "
      @blur="$emit('blur', $event)"
      v-bind="$attrs"
    />
    <!-- In the tab order, and named by the action a press takes. -->
    <button
      type="button"
      class="password-toggle"
      :aria-label="visible ? 'Скрыть пароль' : 'Показать пароль'"
      :aria-pressed="visible"
      @click="visible = !visible"
    >
      <SvgIcon :name="visible ? 'eyeOpen' : 'eyeClosed'" />
    </button>
  </div>
</template>

<style scoped lang="sass">
.password-input
  position: relative
  display: block
  width: 100%

  input
    width: 100%
    // A pair with the eye's inset: the guest field (_AuthField.sass) sets both on the wrapper.
    padding-right: var(--password-toggle-space, 36px)
    box-sizing: border-box

.password-toggle
  position: absolute
  right: var(--password-toggle-inset, #{$minor})
  top: 50%
  transform: translateY(-50%)
  background: none
  border: none
  cursor: pointer
  padding: $minor
  // Full-strength ink: a control dimmed until the pointer finds it reads as unavailable.
  color: $text
  display: flex
  align-items: center
  justify-content: center

  svg
    width: 19px
    height: 19px
</style>
