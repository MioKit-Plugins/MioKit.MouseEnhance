<script setup>
import { computed, onBeforeUnmount, onMounted, ref, shallowRef } from 'vue';

const props = defineProps({
  settings: { type: Object, required: true },
  interactive: Boolean,
});
const emit = defineEmits(['playing']);

const scene = ref(null);
const resting = { x: 0.52, y: 0.51 };
const pointer = shallowRef(resting);
const opacity = ref(1);
const dimensions = shallowRef({ width: 0, height: 0, screens: [] });
const cursor = computed(() => ({
  x: pointer.value.x * dimensions.value.width,
  y: pointer.value.y * dimensions.value.height,
}));
const sceneStyle = computed(() => ({
  '--cursor-x': cursor.value.x + 'px',
  '--cursor-y': cursor.value.y + 'px',
  '--shade': props.settings.maskOpacity * opacity.value / 100,
  '--glow': props.settings.glowOpacity * opacity.value / 100,
  '--glow-color': props.settings.spotlightColor,
  '--radius': Math.max(3, props.settings.spotlightDiameter * dimensions.value.width / 3000) + 'px',
}));
const shadeStyles = computed(() => dimensions.value.screens.map(screen => ({
  '--center-x': cursor.value.x - screen.left + 'px',
  '--center-y': cursor.value.y - screen.top + 'px',
})));
let resizeObserver;
let frame = 0;

function measure() {
  const bounds = scene.value.getBoundingClientRect();
  const screens = Array.from(scene.value.querySelectorAll('.shade'), (shade) => {
    const rect = shade.getBoundingClientRect();
    return { left: rect.left - bounds.left, top: rect.top - bounds.top };
  });
  dimensions.value = { width: bounds.width, height: bounds.height, screens };
}

function reset() {
  if (frame) {
    cancelAnimationFrame(frame);
    frame = 0;
    emit('playing', false);
  }
  pointer.value = resting;
  opacity.value = 1;
}

function play() {
  reset();
  const duration = props.settings.animationDurationMs;
  const hold = props.settings.holdDurationMs;
  const total = duration * 2 + hold;
  const start = performance.now();
  const animatePointer = !window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  function render(timestamp) {
    const elapsed = Math.max(0, timestamp - start);
    if (elapsed >= total) {
      reset();
      return;
    }
    opacity.value = duration === 0
      ? 1
      : Math.min(1, elapsed / duration, (total - elapsed) / duration);
    if (animatePointer) {
      const progress = Math.min(1, Math.max(0, (elapsed - duration) / hold));
      pointer.value = { x: 0.30 + progress * 0.40, y: 0.60 - progress * 0.15 };
    }
    frame = requestAnimationFrame(render);
  }

  emit('playing', true);
  frame = requestAnimationFrame(render);
}

function hover(event) {
  if (!props.interactive || frame) return;
  const bounds = scene.value.getBoundingClientRect();
  pointer.value = {
    x: (event.clientX - bounds.left) / bounds.width,
    y: (event.clientY - bounds.top) / bounds.height,
  };
}

function stopOnEscape(event) {
  if (event.key === 'Escape' && frame) reset();
}

onMounted(() => {
  measure();
  resizeObserver = new ResizeObserver(measure);
  resizeObserver.observe(scene.value);
  if (props.interactive) window.addEventListener('keydown', stopOnEscape);
});
onBeforeUnmount(() => {
  resizeObserver?.disconnect();
  window.removeEventListener('keydown', stopOnEscape);
  reset();
});

defineExpose({ play });
</script>

<template>
  <div
    ref="scene"
    class="scene"
    aria-hidden="true"
    :style="sceneStyle"
    @pointermove="hover"
    @pointerleave="!frame && reset()"
    @click="interactive && reset()"
  >
    <div v-for="(side, index) in ['left', 'right']" :key="side" class="monitor" :class="'monitor-' + side">
      <div class="desktop">
        <div class="mock-window" :class="'mock-window-' + side">
          <span /><span /><span />
        </div>
        <div class="mock-taskbar" />
        <div class="shade" :style="shadeStyles[index]" />
      </div>
    </div>
    <div class="sample-cursor" />
  </div>
</template>
<style scoped>
.scene {
  --radius: 28px;
  position: relative;
  display: grid;
  grid-template-columns: 1.06fr 1fr;
  align-items: end;
  gap: 5px;
  width: min(100%, 465px);
  margin: 9px auto;
}
.monitor {
  height: clamp(70px, 15vw, 100px);
  min-width: 0;
  padding: 3px;
  border-radius: 7px;
  background: #39334b;
  box-shadow: inset 0 0 0 1px #ffffff26, 0 2px 4px #190f2c25;
}
.monitor-right { height: clamp(63px, 14vw, 92px); }
.desktop {
  position: relative;
  height: 100%;
  overflow: hidden;
  isolation: isolate;
  border-radius: 4px;
  background: linear-gradient(140deg, #5240a2, #947fe9 58%, #d593c0);
}
.monitor-right .desktop { background: linear-gradient(135deg, #2b498b, #536fc3 55%, #818ed3); }
.mock-window {
  position: absolute;
  inset: 15% auto auto 14%;
  width: 54%;
  height: 70%;
  display: grid;
  grid-auto-rows: 3px;
  align-content: center;
  gap: 6px;
  padding: 9px;
  border: 1px solid #ede4ffbf;
  border-radius: 4px;
  background: #f4edffe3;
}
.mock-window-right { inset: 26% 10% auto auto; width: 44%; height: 59%; background: #dceaffdc; }
.mock-window span { border-radius: 3px; background: #9182b6; }
.mock-window span:nth-child(2) { width: 76%; }
.mock-window span:nth-child(3) { width: 49%; }
.mock-taskbar {
  position: absolute;
  inset: auto 35% 4% 35%;
  height: 5px;
  border-radius: 3px;
  background: #ded4fa9c;
}
.shade, .shade::after { position: absolute; inset: 0; }
.shade {
  --center-x: 50%;
  --center-y: 50%;
  background: radial-gradient(circle at var(--center-x) var(--center-y),
    transparent calc(var(--radius) - 1px), rgb(12 7 25 / var(--shade)) calc(var(--radius) + 1px));
}
.shade::after {
  content: '';
  opacity: var(--glow);
  background: radial-gradient(circle at var(--center-x) var(--center-y),
    transparent calc(var(--radius) - 2px), var(--glow-color) var(--radius), transparent calc(var(--radius) + 13px));
}
.sample-cursor {
  position: absolute;
  z-index: 1;
  left: var(--cursor-x);
  top: var(--cursor-y);
  width: 13px;
  height: 19px;
  transform: translate(-2px, -1px);
  pointer-events: none;
  filter: drop-shadow(0 1px 1px #000a);
}
.sample-cursor::before, .sample-cursor::after {
  content: '';
  position: absolute;
  inset: 0;
  background: #2d203b;
  clip-path: polygon(8% 0, 100% 66%, 65% 67%, 82% 95%, 61% 100%, 42% 73%, 11% 94%);
}
.sample-cursor::after { inset: 1.5px 2px 2.5px 1.5px; background: #fff; }
</style>
