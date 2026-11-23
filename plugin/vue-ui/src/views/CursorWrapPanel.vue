<script setup>
import { ref } from 'vue';
import { Button } from '@/components/ui/button';
import SettingsToggle from '@/components/SettingsToggle.vue';

const props = defineProps({
  settings: { type: Object, required: true },
  ready: Boolean,
  toggleActive: { type: Function, required: true },
});
const emit = defineEmits(['save', 'error']);
const changingState = ref(false);

async function toggleActivation() {
  if (changingState.value) return;

  changingState.value = true;
  try {
    await props.toggleActive();
  } catch (error) {
    emit('error', error.message || '无法切换光标环绕状态。');
  } finally {
    changingState.value = false;
  }
}
</script>

<template>
  <header class="settings-heading">
    <h1>光标环绕</h1>
    <p>让鼠标指针从屏幕的一侧移到对侧。</p>
  </header>

  <div class="settings-panel">
    <SettingsToggle
      id="cursor-wrap-enabled"
      label="启用光标环绕"
      :model-value="settings.enabled"
      :disabled="!ready"
      @update:model-value="emit('save', 'enabled', $event)"
    />
    <div v-if="settings.enabled" class="setting-row">
      <p class="control-label" aria-live="polite">{{ settings.isActive ? '已激活' : '待激活' }}</p>
      <Button type="button" variant="outline" size="sm" :disabled="!ready || changingState" @click="toggleActivation">
        {{ settings.isActive ? '暂停环绕' : '激活环绕' }}
      </Button>
    </div>
  </div>

  <section class="settings-panel settings-group" aria-labelledby="cursor-wrap-behavior">
    <h2 id="cursor-wrap-behavior">激活与行为</h2>
    <SettingsToggle
      id="cursor-wrap-auto-activate"
      label="启动时自动激活"
      :model-value="settings.autoActivate"
      :disabled="!ready || !settings.enabled"
      @update:model-value="emit('save', 'autoActivate', $event)"
    />
    <SettingsToggle
      id="cursor-wrap-disable-dragging"
      label="拖动时禁用环绕"
      :model-value="settings.disableWhileDragging"
      :disabled="!ready || !settings.enabled"
      @update:model-value="emit('save', 'disableWhileDragging', $event)"
    />
    <div class="setting-row">
      <label class="control-label" for="cursor-wrap-mode">环绕方向</label>
      <select
        id="cursor-wrap-mode"
        class="cursor-wrap-select"
        :value="settings.mode"
        :disabled="!ready || !settings.enabled"
        @change="emit('save', 'mode', $event.target.value)"
      >
        <option value="both">水平和垂直</option>
        <option value="horizontal">仅水平</option>
        <option value="vertical">仅垂直</option>
      </select>
    </div>
    <div class="setting-row">
      <label class="control-label" for="cursor-wrap-activation">环绕条件</label>
      <select
        id="cursor-wrap-activation"
        class="cursor-wrap-select"
        :value="settings.activation"
        :disabled="!ready || !settings.enabled"
        @change="emit('save', 'activation', $event.target.value)"
      >
        <option value="always">始终</option>
        <option value="control">按住 Ctrl</option>
        <option value="shift">按住 Shift</option>
      </select>
    </div>
    <SettingsToggle
      id="cursor-wrap-disable-single-monitor"
      label="仅使用一个显示器时禁用环绕"
      :model-value="settings.disableForSingleMonitor"
      :disabled="!ready || !settings.enabled"
      @update:model-value="emit('save', 'disableForSingleMonitor', $event)"
    />
  </section>
</template>
