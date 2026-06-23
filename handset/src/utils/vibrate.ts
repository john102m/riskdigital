export function tap() {
  navigator.vibrate?.(40);
}

export function heavyTap() {
  navigator.vibrate?.(100);
}
