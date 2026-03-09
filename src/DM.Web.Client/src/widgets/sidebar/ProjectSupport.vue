<template>
  <menu-block token="ProjectSupport">
    <template #title>Поддержка проекта</template>
    <progress-bar :current="current" :goal="goal"
      >{{ current }} / {{ goal }} р.</progress-bar
    >
    dm.am &ndash; некоммерческий проект.<br />
    <router-link :to="{ name: 'donate' }">Помогите нам</router-link> хотя бы не
    испортить его!
  </menu-block>
</template>

<script setup lang="ts">
import { ref } from "vue";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import MenuBlock from "@/widgets/menu/MenuBlock.vue";

const goal = ref(50000);
const current = ref(17000);
</script>
