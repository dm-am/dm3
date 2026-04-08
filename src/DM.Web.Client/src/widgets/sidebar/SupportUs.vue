<template>
  <SidebarBlock token="SupportUs">
    <template #title>Поддержка проекта</template>
    <ProgressBar :current="current" :goal="goal">
      {{ current }} / {{ goal }} р.
    </ProgressBar>
    dm.am &ndash; некоммерческий проект.<br />
    <router-link :to="{ name: 'support' }">Помогите нам</router-link> хотя бы не
    испортить его!
  </SidebarBlock>
</template>

<script setup lang="ts">
import { ref } from "vue";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import SidebarBlock from "./SidebarBlock.vue";

const goal = ref(50000);
const current = ref(17000);
</script>
