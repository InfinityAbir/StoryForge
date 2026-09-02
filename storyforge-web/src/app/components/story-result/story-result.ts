import { Component, computed, input, output, signal } from '@angular/core';

@Component({
  selector: 'app-story-result',
  templateUrl: './story-result.html',
  styleUrl: './story-result.css'
})
export class StoryResult {
  readonly title = input.required<string>();
  readonly story = input.required<string>();
  readonly disabled = input(false);

  readonly regenerate = output<void>();
  readonly openFeedback = output<void>();

  readonly copied = signal(false);

  readonly paragraphs = computed(() =>
    this.story()
      .split(/\n{2,}/)
      .map((p) => p.trim())
      .filter((p) => p.length > 0)
  );

  async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.story());
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2200);
    } catch {
      // Clipboard API unavailable — silently ignore, button state simply won't confirm.
    }
  }
}
