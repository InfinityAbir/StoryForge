import { DecimalPipe } from '@angular/common';
import { Component, computed, ElementRef, input, model, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OutputLanguage, StoryLength } from '../../models/story.models';
import { STORY_MAX_CHARS, STORY_MIN_CHARS } from '../../constants';

const MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB

@Component({
  selector: 'app-story-input',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './story-input.html',
  styleUrl: './story-input.css'
})
export class StoryInput {
  readonly disabled = input(false);

  readonly storyText = model('');
  readonly outputLanguage = model<OutputLanguage>('same');
  readonly length = model<StoryLength>('medium');

  readonly generate = output<void>();

  private readonly textarea = viewChild<ElementRef<HTMLTextAreaElement>>('storyTextarea');
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');

  readonly uploadError = signal<string | null>(null);

  readonly charCount = computed(() => this.storyText().length);

  readonly isTooShort = computed(() => {
    const len = this.charCount();
    return len > 0 && len < STORY_MIN_CHARS;
  });

  readonly isTooLong = computed(() => this.charCount() > STORY_MAX_CHARS);

  readonly canGenerate = computed(() => {
    const len = this.charCount();
    return len >= STORY_MIN_CHARS && len <= STORY_MAX_CHARS && !this.disabled();
  });

  readonly minChars = STORY_MIN_CHARS;
  readonly maxChars = STORY_MAX_CHARS;

  onInput(event: Event): void {
    const target = event.target as HTMLTextAreaElement;
    this.storyText.set(target.value);
    this.autoGrow(target);
  }

  private autoGrow(el: HTMLTextAreaElement): void {
    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, 560)}px`;
  }

  clear(): void {
    this.storyText.set('');
    const el = this.textarea()?.nativeElement;
    if (el) {
      el.style.height = 'auto';
      el.focus();
    }
  }

  onGenerateClick(): void {
    if (this.canGenerate()) {
      this.generate.emit();
    }
  }

  triggerFileUpload(): void {
    this.fileInput()?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file later

    if (!file) return;

    this.uploadError.set(null);

    const isTxt = file.name.toLowerCase().endsWith('.txt') || file.type === 'text/plain';
    if (!isTxt) {
      this.uploadError.set('Please choose a .txt file.');
      return;
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      this.uploadError.set('That file is too large. Please choose a smaller .txt file.');
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const text = typeof reader.result === 'string' ? reader.result : '';
      this.storyText.set(text);
      const el = this.textarea()?.nativeElement;
      if (el) {
        el.value = text;
        this.autoGrow(el);
      }
    };
    reader.onerror = () => {
      this.uploadError.set("Couldn't read that file. Please try again.");
    };
    reader.readAsText(file, 'utf-8');
  }
}
