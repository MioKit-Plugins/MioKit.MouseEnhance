<script setup>
import { computed, reactive, ref } from 'vue';
import { Button } from '@/components/ui/button';
import SettingsToggle from '@/components/SettingsToggle.vue';
import SpotlightSample from '@/components/SpotlightSample.vue';

const fields = [
  { field: 'spotlightColor', label: '聚光灯颜色', type: 'color' },
  { field: 'spotlightDiameter', label: '聚光灯直径', min: 48, max: 640, unit: ' px' },
  { field: 'maskOpacity', label: '遮罩强度', min: 0, max: 100, unit: '%' },
  { field: 'glowOpacity', label: '光晕强度', min: 0, max: 100, unit: '%' },
  { field: 'animationDurationMs', label: '淡入与淡出', min: 0, max: 2000, unit: ' ms' },
  { field: 'holdDurationMs', label: '停留时长', min: 100, max: 5000, unit: ' ms' },
];

const props = defineProps({
  settings: { type: Object, required: true },
  ready: Boolean,
  browser: Boolean,
});
const emit = defineEmits(['save', 'preview']);
const sample = ref(null);
const playing = ref(false);
const drafts = reactive({});
const displaySettings = computed(() => ({ ...props.settings, ...drafts }));

function sliderInput(field, event) {
  const value = event.target.type === 'range' ? event.target.valueAsNumber : event.target.value;
  drafts[field] = value;
}

function sliderChange(field, event) {
  sliderInput(field, event);
  emit('save', field, drafts[field]);
  delete drafts[field];
}

function preview() {
  if (props.browser) {
    sample.value.play();
  } else {
    emit('preview');
  }
}

function format(field) {
  const value = displaySettings.value[field.field];
  return field.type === 'color' ? value.toUpperCase() : value + field.unit;
}
</script>

<template>
  <header class="settings-heading">
    <h1>寻找鼠标</h1>
    <p>使用全局手势显示跟随指针的聚光灯。</p>
  </header>

  <section class="spotlight-preview settings-panel" aria-labelledby="spotlight-title">
    <div class="spotlight-preview-heading">
      <div>
        <h2 id="spotlight-title">聚光灯预览</h2>
        <p class="control-hint" role="status">
          {{ browser
            ? (playing ? '正在播放浏览器预览。' : '浏览器中模拟双屏聚光灯。')
            : '功能关闭时也可以预览。' }}
        </p>
      </div>
      <Button type="button" :disabled="!ready" @click="preview">预览效果</Button>
    </div>
    <SpotlightSample ref="sample" :settings="displaySettings" :interactive="browser" @playing="playing = $event" />
  </section>

  <div class="find-mouse-groups">
    <section class="settings-panel settings-group" aria-labelledby="find-trigger">
      <h2 id="find-trigger">触发方式</h2>
      <SettingsToggle
        id="enabled"
        label="启用寻找鼠标"
        :model-value="settings.enabled"
        :disabled="!ready"
        @update:model-value="$emit('save', 'enabled', $event)"
      />
      <SettingsToggle
        id="doubleCtrlEnabled"
        label="双击 Ctrl"
        description="快速按下并松开同一侧 Ctrl 两次。"
        :model-value="settings.doubleCtrlEnabled"
        :disabled="!ready"
        @update:model-value="$emit('save', 'doubleCtrlEnabled', $event)"
      />
      <SettingsToggle
        id="shakeEnabled"
        label="摇晃鼠标"
        description="快速来回移动，拖动期间暂停识别。"
        :model-value="settings.shakeEnabled"
        :disabled="!ready"
        @update:model-value="$emit('save', 'shakeEnabled', $event)"
      />
      <div class="range-setting">
        <label for="shakeThreshold" class="control-label">摇晃阈值</label>
        <span class="control-hint">{{ displaySettings.shakeThreshold }} 次</span>
        <input
          id="shakeThreshold"
          type="range"
          min="3"
          max="12"
          :value="displaySettings.shakeThreshold"
          :aria-valuetext="displaySettings.shakeThreshold + ' 次'"
          aria-describedby="shake-threshold-hint"
          :disabled="!ready"
          @input="sliderInput('shakeThreshold', $event)"
          @change="sliderChange('shakeThreshold', $event)"
        >
        <p id="shake-threshold-hint" class="control-hint">阈值越低，越容易触发。</p>
      </div>
    </section>
    <section class="settings-panel settings-group" aria-labelledby="find-appearance">
      <h2 id="find-appearance">外观与时间</h2>
      <div class="appearance-grid">
        <div v-for="entry in fields" :key="entry.field" class="range-setting">
          <label :for="entry.field" class="control-label">{{ entry.label }}</label>
          <span class="control-hint">{{ format(entry) }}</span>
          <input
            :id="entry.field"
            :type="entry.type || 'range'"
            :min="entry.min"
            :max="entry.max"
            :value="displaySettings[entry.field]"
            :aria-valuetext="entry.type ? undefined : format(entry)"
            :disabled="!ready"
            @input="sliderInput(entry.field, $event)"
            @change="sliderChange(entry.field, $event)"
          >
        </div>
      </div>
    </section>
  </div>
</template>
