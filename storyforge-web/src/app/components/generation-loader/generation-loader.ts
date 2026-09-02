import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { LOADING_MESSAGES } from '../../constants';

@Component({
  selector: 'app-generation-loader',
  templateUrl: './generation-loader.html',
  styleUrl: './generation-loader.css'
})
export class GenerationLoader {
  readonly active = input(false);

  readonly messageIndex = signal(0);
  readonly messages = LOADING_MESSAGES;

  private readonly destroyRef = inject(DestroyRef);
  private intervalId: ReturnType<typeof setInterval> | undefined;

  constructor() {
    effect(() => {
      if (this.active()) {
        this.messageIndex.set(0);
        this.intervalId = setInterval(() => {
          this.messageIndex.update((i) => (i + 1) % this.messages.length);
        }, 2200);
      } else if (this.intervalId) {
        clearInterval(this.intervalId);
        this.intervalId = undefined;
      }
    });

    this.destroyRef.onDestroy(() => {
      if (this.intervalId) {
        clearInterval(this.intervalId);
      }
    });
  }
}
