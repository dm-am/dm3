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
    <!--
      Reachable by keyboard, and the label names the action rather than both
      halves of it. Out of the tab order the control existed for the mouse
      alone, which is the one input method a person checking a typo-prone
      password is least likely to be on; a label reading "показать/скрыть"
      also left a screen reader unable to say which of the two a press does.
    -->
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

<script setup lang="ts">
import { ref } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";

defineProps<{
  modelValue: string;
  /**
   * Id for the <input>. `$attrs` already carried a caller's id onto it, but
   * only a binding written in the template can be read by lint, and the
   * association is a rule of the build now.
   */
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

<style scoped lang="sass">
.password-input
  position: relative
  display: block
  width: 100%

  input
    width: 100%
    // The eye stands over the right edge of the field, so the text has to stop
    // before it — and how far before depends on how far in the eye is. The two
    // numbers are one pair, and they are variables because the control comes in
    // two sizes: the site's own field, and the taller guest field of
    // AuthSurface.sass, which sets both on the wrapper around this component.
    // Written as literals here, the guest field could only move the eye by
    // outweighing this rule, and the padding would silently stay behind it.
    padding-right: var(--password-toggle-space, 36px)
    box-sizing: border-box

.password-toggle
  position: absolute
  right: var(--password-toggle-inset, 4px)
  top: 50%
  transform: translateY(-50%)
  background: none
  border: none
  cursor: pointer
  padding: 4px
  // Full-strength ink, and not the muted one it used to rest in: the eye is the
  // only control on a password field, the field it stands in shows nothing, and
  // a control dimmed until the pointer finds it reads as unavailable.
  color: $text
  display: flex
  align-items: center
  justify-content: center

  svg
    width: 19px
    height: 19px
</style>
