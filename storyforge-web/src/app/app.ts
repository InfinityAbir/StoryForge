import { Component, computed, inject, signal } from '@angular/core';
import { StoryInput } from './components/story-input/story-input';
import { GenerationLoader } from './components/generation-loader/generation-loader';
import { StoryDnaPanel } from './components/story-dna/story-dna';
import { StoryResult } from './components/story-result/story-result';
import { FeedbackBox } from './components/feedback-box/feedback-box';
import { VersionHistory } from './components/version-history/version-history';
import { StoryService } from './services/story.service';
import { ThemeService } from './services/theme.service';
import { GenerateStoryRequest, OutputLanguage, StoryLength, StoryVersion } from './models/story.models';

@Component({
  selector: 'app-root',
  imports: [StoryInput, GenerationLoader, StoryDnaPanel, StoryResult, FeedbackBox, VersionHistory],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly storyService = inject(StoryService);
  protected readonly themeService = inject(ThemeService);

  protected readonly sourceStory = signal('');
  protected readonly outputLanguage = signal<OutputLanguage>('same');
  protected readonly length = signal<StoryLength>('medium');

  protected readonly isGenerating = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly feedbackOpen = signal(false);

  protected readonly versions = signal<StoryVersion[]>([]);
  protected readonly activeVersionId = signal<string | undefined>(undefined);

  protected readonly activeVersion = computed(() =>
    this.versions().find((v) => v.id === this.activeVersionId())
  );

  protected readonly hasResult = computed(() => !!this.activeVersion());

  onGenerate(): void {
    const story = this.sourceStory().trim();
    if (!story) {
      this.error.set('Please enter a story first.');
      return;
    }

    this.run({
      mode: 'initial',
      story,
      inputLanguage: 'auto',
      outputLanguage: this.outputLanguage(),
      length: this.length()
    });
  }

  onRegenerate(): void {
    const current = this.activeVersion();
    if (!current) return;

    this.run({
      mode: 'regenerate',
      storyDna: current.storyDna,
      inputLanguage: 'auto',
      outputLanguage: this.outputLanguage(),
      length: this.length()
    });
  }

  onSubmitFeedback(feedback: string): void {
    const current = this.activeVersion();
    if (!current) return;

    this.run(
      {
        mode: 'feedback',
        storyDna: current.storyDna,
        previousStory: current.story,
        feedback,
        inputLanguage: 'auto',
        outputLanguage: this.outputLanguage(),
        length: this.length()
      },
      feedback
    );
  }

  onSelectVersion(id: string): void {
    this.activeVersionId.set(id);
  }

  onCloseFeedback(): void {
    this.feedbackOpen.set(false);
  }

  dismissError(): void {
    this.error.set(null);
  }

  private run(request: GenerateStoryRequest, feedback?: string): void {
    this.isGenerating.set(true);
    this.error.set(null);

    this.storyService.generate(request).subscribe({
      next: (response) => {
        const version: StoryVersion = {
          id: this.makeId(),
          title: response.title,
          story: response.story,
          storyDna: response.storyDna,
          feedback,
          createdAt: new Date().toISOString()
        };
        this.versions.update((list) => [...list, version]);
        this.activeVersionId.set(version.id);
        this.isGenerating.set(false);
        this.feedbackOpen.set(false);
      },
      error: (err: Error) => {
        this.error.set(err.message);
        this.isGenerating.set(false);
      }
    });
  }

  private makeId(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `v-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
