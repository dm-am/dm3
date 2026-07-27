<script setup lang="ts">
/**
 * CharacterFormPreview — the character-creation form rendered in no-submit
 * mode. It shows how a player will fill the sheet for the current schema:
 * a name field plus one control per specification, rendered by the shared
 * CharacterSheetFields (same controls the real form uses). Purely a preview —
 * it holds throwaway local state and has no submit action.
 */
import { computed, reactive, ref } from "vue";
import type { AttributeSchema } from "@/entities/game";
import { CharacterSheetFields } from "@/entities/game";
import FormField from "@/shared/ui/Form/FormField.vue";

const props = defineProps<{
  schema: AttributeSchema;
}>();

// Throwaway preview state (never submitted anywhere).
const name = ref("");
const draftValues = reactive<Record<string, string>>({});

const orderedSpecs = computed(() =>
  [...props.schema.specifications].sort((a, b) => a.order - b.order),
);
</script>

<template>
  <div class="character-preview">
    <div class="preview-header">Превью анкеты персонажа</div>

    <form-field label="Имя персонажа *" name="preview-name">
      <input v-model="name" type="text" placeholder="Имя персонажа" />
    </form-field>

    <CharacterSheetFields
      :specs="orderedSpecs"
      :values="draftValues"
      @update="(id, val) => (draftValues[id] = val)"
    />
  </div>
</template>

<style scoped lang="sass">
.character-preview
  padding: $medium
  background-color: $bg-element
  border: 1px dashed $border
  border-radius: $border-radius

.preview-header
  font-weight: bold
  color: $text-muted
  margin-bottom: $small
</style>
