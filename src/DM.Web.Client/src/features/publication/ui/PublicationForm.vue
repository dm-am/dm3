<script setup lang="ts">
/**
 * PublicationForm — the shared create/edit form for blog publications
 * (title, rubric, BBCode content). The pages own loading, validation and the
 * submit/cancel handlers; this component only renders the fields and relays
 * the two actions. `draftKey` is passed on create (autosave the new-post
 * draft) and omitted on edit.
 */
import { ref } from "vue";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";

defineProps<{
  rubricOptions: { value: string; label: string }[];
  valid: boolean;
  loading: boolean;
  action: string;
  cancelLabel: string;
  draftKey?: string;
}>();

defineEmits<{ submit: []; cancel: [] }>();

const title = defineModel<string>("title", { required: true });
const rubricId = defineModel<string>("rubricId", { required: true });
const content = defineModel<string>("content", { required: true });

const editor = ref<InstanceType<typeof BBCodeEditor> | null>(null);

// The draft belongs to the editor, and the page is the one that knows the text
// has been published. Every other composer in the app clears its draft on
// success through the same call.
defineExpose({ clearDraft: () => editor.value?.clearDraft() });
</script>

<template>
  <Form
    :valid="valid"
    :loading="loading"
    :action="action"
    :cancel="cancelLabel"
    @submit="$emit('submit')"
    @cancel="$emit('cancel')"
  >
    <FormField label="Название публикации" name="publication-title">
      <input
        id="publication-title"
        v-model="title"
        type="text"
        maxlength="300"
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
        :min-height="240"
        :resizable="true"
      />
    </FormField>
  </Form>
</template>

<style scoped lang="sass">
:deep(.bbcode-editor-wrapper)
  width: 100%
</style>
