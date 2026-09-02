import { Component, computed, ElementRef, HostListener, input, output, signal, viewChild } from '@angular/core';
import { FEEDBACK_MAX_CHARS } from '../../constants';

@Component({
  selector: 'app-feedback-box',
  templateUrl: './feedback-box.html',
  styleUrl: './feedback-box.css'
})
export class FeedbackBox {
  readonly open = input(false);
  readonly disabled = input(false);

  readonly submitFeedback = output<string>();
  readonly close = output<void>();

  readonly text = signal('');
  readonly maxChars = FEEDBACK_MAX_CHARS;

  private readonly textarea = viewChild<ElementRef<HTMLTextAreaElement>>('feedbackTextarea');

  readonly charCount = computed(() => this.text().length);
  readonly isTooLong = computed(() => this.charCount() > this.maxChars);
  readonly canSubmit = computed(() => this.text().trim().length > 0 && !this.isTooLong() && !this.disabled());

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.close.emit();
    }
  }

  onInput(event: Event): void {
    this.text.set((event.target as HTMLTextAreaElement).value);
  }

  onSubmit(): void {
    if (this.canSubmit()) {
      this.submitFeedback.emit(this.text().trim());
      this.text.set('');
    }
  }

  onClose(): void {
    this.close.emit();
  }

  onBackdropClick(): void {
    if (!this.disabled()) {
      this.close.emit();
    }
  }
}
