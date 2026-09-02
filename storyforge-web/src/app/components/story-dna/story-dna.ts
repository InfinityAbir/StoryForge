import { Component, input, signal } from '@angular/core';
import { StoryDna } from '../../models/story.models';

@Component({
  selector: 'app-story-dna',
  templateUrl: './story-dna.html',
  styleUrl: './story-dna.css'
})
export class StoryDnaPanel {
  readonly dna = input.required<StoryDna>();

  readonly expanded = signal(true);

  toggle(): void {
    this.expanded.update((v) => !v);
  }
}
