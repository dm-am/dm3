import { defineStore } from "pinia";
import { ref } from "vue";
import { ColorSchema } from "@/api/models/community";

export const useUiStore = defineStore("ui", () => {
  const theme = ref(ColorSchema.Light);

  const updateTheme = (newTheme: ColorSchema) => (theme.value = newTheme);
  const toggleTheme = () => {
    updateTheme(theme.value === ColorSchema.Dark ? ColorSchema.Light : ColorSchema.Dark);
  };

  return { theme, updateTheme, toggleTheme };
});
