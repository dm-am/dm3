<script setup lang="ts">
defineProps<{
  accept?: string;
}>();
const emits = defineEmits<{ uploading: [formData: FormData] }>();

const upload = (event: Event) => {
  const target = event.target as HTMLInputElement;
  const files = target.files!;

  if (files.length === 0) return;

  const formData = new FormData();
  const name = files.length === 1 ? "file" : "files";

  for (const file of files) formData.append(name, file);

  emits("uploading", formData);
};
</script>

<template>
  <input
    type="file"
    class="upload-input"
    :accept="accept"
    aria-label="Выбрать файл"
    @change="upload"
  />
</template>

<style scoped lang="sass">
.upload-input
  position: absolute
  bottom: 0
  top: 0
  left: 0
  right: 0
  width: 100%
  padding: 0
  margin: 0
  opacity: 0
  border: none
  outline: none
  cursor: pointer
</style>
