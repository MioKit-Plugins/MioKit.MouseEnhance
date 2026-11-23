<script setup>
import { Switch } from '@/components/ui/switch';

defineProps({
  id: { type: String, required: true },
  label: { type: String, required: true },
  description: { type: String, default: '' },
  modelValue: Boolean,
  disabled: Boolean,
});
defineEmits(['update:modelValue']);
</script>

<template>
  <div class="setting-row">
    <div>
      <label :id="`${id}-label`" :for="id" class="control-label">{{ label }}</label>
      <p v-if="description" :id="`${id}-hint`" class="control-hint">{{ description }}</p>
    </div>
    <Switch
      :id="id"
      :model-value="modelValue"
      :aria-labelledby="`${id}-label`"
      :aria-describedby="description ? `${id}-hint` : undefined"
      :disabled="disabled"
      @update:model-value="$emit('update:modelValue', $event)"
    />
  </div>
</template>
