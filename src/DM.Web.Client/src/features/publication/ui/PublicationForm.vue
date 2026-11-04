<script setup lang="ts">
// The pages own loading, validation and submit; `draftKey` only on create.
import { ref } from "vue";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { PUBLICATION_TITLE_MAX_LENGTH } from "@/shared/lib/constants/blog";
import { PUBLICATION_EDITOR_HEIGHT } from "./layout";

defineProps<{
  rubricOptions: { value: string; label: string }[];
  valid: boolean;
  loading: boolean;
  action: string;
  cancelLabel: string;
  draftKey?: string;
  refusal?: string | null;
}>();

defineEmits<{ submit: []; cancel: [] }>();

const title = defineModel<string>("title", { required: true });
const rubricId = defineModel<string>("rubricId", { required: true });
const content = defineModel<string>("content", { required: true });

const editor = ref<InstanceType<typeof BBCodeEditor> | null>(null);

// The page knows when the text is published, so it clears the draft.
// success through the same call.
defineExpose({ clearDraft: () => editor.value?.clearDraft() });
</script>

<template>
  <Form
    :valid="valid"
    :loading="loading"
    :action="action"
    :cancel="cancelLabel"
    :refusal="refusal"
    @submit="$emit('submit')"
    @cancel="$emit('cancel')"
  >
    <FormField label="Название публикации" name="publication-title">
      <input
        id="publication-title"
        v-model="title"
        type="text"
        :maxlength="PUBLICATION_TITLE_MAX_LENGTH"
      />
    </FormField>

    <FormField label="Рубрика">
      <Select
        id="publication-rubric"
        :model-value="rubricId"
        :options="rubricOptions"
        @update:model-value="(v) => (rubricId = v as string)"
      />
    </FormField>

    <FormField label="Содержимое">
      <BBCodeEditor
        ref="editor"
        v-model="content"
        context="common"
        placeholder="Текст публикации..."
        :draft-key="draftKey"
        :disabled="loading"
        :min-height="PUBLICATION_EDITOR_HEIGHT"
        :resizable="true"
      />
    </FormField>
  </Form>
</template>

<style scoped lang="sass">
:deep(.bbcode-editor-wrapper)
  width: 100%
</style>
