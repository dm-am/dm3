<template>
  <menu-block token="Donation">
    <template #title><the-icon :font="IconType.Ruble" /> Помочь проекту</template>
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
import ProgressBar from "@/components/ProgressBar.vue";
import MenuBlock from "@/views/layout/MenuBlock.vue";
import { IconType } from "@/components/icons/iconType";

const goal = ref(50000);
const current = ref(17000);
</script>

